"use strict";

const folderInput = document.getElementById("folderInput");
const clearButton = document.getElementById("clearButton");
const sessionDialog = document.getElementById("sessionDialog");
const numberFormatter = new Intl.NumberFormat();

folderInput.addEventListener("change", () => loadLogs([...folderInput.files]));
clearButton.addEventListener("click", resetDashboard);
document.getElementById("closeDialog").addEventListener("click", () => sessionDialog.close());
sessionDialog.addEventListener("click", event => {
    if (event.target === sessionDialog) sessionDialog.close();
});

function newState() {
    return {
        sessions: new Map(),
        users: new Set(),
        machines: new Set(),
        graphs: new Map(),
        packages: new Map(),
        dailySessions: new Map(),
        dailyRuns: new Map(),
        files: 0,
        valid: 0,
        invalid: 0,
        graphOpens: 0,
        runs: 0,
        skippedRuns: 0,
        successfulRuns: 0,
        lastActivity: null
    };
}

async function loadLogs(selectedFiles) {
    const files = Array.isArray(selectedFiles)
        ? selectedFiles.filter(file => file && file.name && file.name.toLowerCase().endsWith(".jsonl"))
        : [];
    const status = document.getElementById("loadStatus");
    status.className = "status";
    status.textContent = files.length ? `Reading ${files.length} file${files.length === 1 ? "" : "s"}…` : "No .jsonl files selected";

    if (!files.length) {
        render(newState());
        return;
    }

    const state = newState();
    state.files = files.length;

    for (const file of files.sort((a, b) => a.name.localeCompare(b.name))) {
        let text;
        try {
            text = await file.text();
        } catch {
            state.invalid++;
            continue;
        }

        for (const line of text.split(/\r?\n/)) {
            if (!line.trim()) continue;
            try {
                const record = JSON.parse(line);
                processRecord(record, state);
                state.valid++;
            } catch {
                state.invalid++;
            }
        }
    }

    render(state);
    status.className = `status ${state.valid ? "good" : ""}`;
    status.textContent = state.valid ? `${state.valid.toLocaleString()} records loaded` : "No valid records found";
}

function processRecord(record, state) {
    if (!record || typeof record.event !== "string" || typeof record.session_id !== "string" || !record.occurred_utc) {
        throw new Error("Invalid telemetry envelope");
    }

    const graphData = record.data?.graph || record.data || {};
    if (isTemporaryHomeGraph(graphData, record.data)) {
        return;
    }

    const occurred = new Date(record.occurred_utc);
    if (Number.isNaN(occurred.getTime())) {
        throw new Error("Invalid event timestamp");
    }

    let session = state.sessions.get(record.session_id);
    if (!session) {
        session = {
            id: record.session_id,
            start: null,
            end: null,
            last: occurred,
            user: null,
            machine: null,
            host: "Unknown",
            dynamo: "Unknown",
            extension: "Unknown",
            graphs: 0,
            runs: 0,
            skippedRuns: 0,
            graphDetails: new Map(),
            packages: new Map(),
            events: [],
            rawRecords: [],
            summary: {
                location: null,
                fileSize: null,
                startNodeCount: null,
                finalNodeCount: null,
                nodeTypeCounts: { packages: null, custom: null, zeroTouch: null }
            }
        };
        state.sessions.set(record.session_id, session);
    }

    session.rawRecords.push(record);

    if (occurred > session.last) session.last = occurred;
    if (!state.lastActivity || occurred > state.lastActivity) state.lastActivity = occurred;

    const graph = record.data?.graph || (record.data?.graph_id ? record.data : null);
    const graphName = safeGraphName(graph?.name);
    session.events.push({ type: record.event, occurred, summary: eventSummary(record, graphName) });
    collectSessionSummary(session, record, graph);

    if (graph) {
        const graphKey = graph.graph_id || graph.file_path || graph.name || "unknown";
        session.graphDetails.set(graphKey, {
            name: graphName,
            path: graph.file_path || "Not saved",
            nodes: Number.isFinite(graph.node_count) ? graph.node_count : 0,
            saved: graph.is_saved === true
        });

        for (const packageInfo of normalizePackages(graph.packages)) {
            session.packages.set(packageKey(packageInfo), packageInfo);
        }
    }

    switch (record.event) {
        case "session.started": {
            const identity = record.data?.identity;
            const app = record.data?.application;
            if (!identity || !app) throw new Error("Invalid session data");

            session.start = occurred;
            session.user = identity.user_id;
            session.machine = identity.machine_id;
            session.host = [app.host_name, app.host_version].filter(Boolean).join(" ") || "Unknown";
            session.dynamo = app.dynamo_version || "Unknown";
            session.extension = app.extension_version || "Unknown";

            if (identity.user_id) state.users.add(identity.user_id);
            if (identity.machine_id) state.machines.add(identity.machine_id);
            increment(state.dailySessions, dateKey(occurred));
            break;
        }
        case "session.ended":
            session.end = occurred;
            break;
        case "graph.opened": {
            session.graphs++;
            state.graphOpens++;
            increment(state.graphs, graphName);

            for (const packageInfo of normalizePackages(graph?.packages)) {
                increment(state.packages, packageKey(packageInfo));
            }
            break;
        }
        case "execution.ended":
            if (record.data?.evaluation_took_place === false) {
                session.skippedRuns++;
                state.skippedRuns++;
                break;
            }

            session.runs++;
            state.runs++;
            if (record.data?.successful === true) state.successfulRuns++;
            increment(state.dailyRuns, dateKey(occurred));
            break;
    }
}

function render(state) {
    setText("sessionCount", numberFormatter.format([...state.sessions.values()].filter(item => item.start).length));
    setText("userCount", numberFormatter.format(state.users.size));
    setText("machineCount", numberFormatter.format(state.machines.size));
    setText("graphCount", numberFormatter.format(state.graphOpens));
    setText("runCount", numberFormatter.format(state.runs));
    setText("successRate", `${state.runs ? (state.successfulRuns * 100 / state.runs).toFixed(1).replace(".0", "") : 0}%`);
    setText("skippedRunCount", numberFormatter.format(state.skippedRuns));
    setText("packageCount", numberFormatter.format(state.packages.size));
    setText("fileCount", numberFormatter.format(state.files));
    setText("validCount", numberFormatter.format(state.valid));
    setText("invalidCount", numberFormatter.format(state.invalid));

    const incomplete = [...state.sessions.values()].filter(item => !item.start || !item.end).length;
    setText("incompleteCount", numberFormatter.format(incomplete));
    document.getElementById("invalidCount").classList.toggle("warning", state.invalid > 0);
    document.getElementById("incompleteCount").classList.toggle("warning", incomplete > 0);

    if (state.lastActivity) {
        setText("lastDate", state.lastActivity.toLocaleDateString(undefined, { day: "2-digit", month: "short" }));
        setText("lastTime", `${state.lastActivity.toLocaleTimeString()} local time`);
    } else {
        setText("lastDate", "—");
        setText("lastTime", "No activity");
    }

    renderChart(state);
    renderGraphs(state);
    renderPackages(state);
    renderSessions(state);
}

function renderChart(state) {
    const chart = document.getElementById("chart");
    chart.replaceChildren();

    const days = [];
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    for (let offset = 13; offset >= 0; offset--) {
        const date = new Date(today);
        date.setDate(today.getDate() - offset);
        const key = dateKey(date);
        days.push({ date, sessions: state.dailySessions.get(key) || 0, runs: state.dailyRuns.get(key) || 0 });
    }

    const maxSessions = Math.max(1, ...days.map(day => day.sessions));
    const maxRuns = Math.max(1, ...days.map(day => day.runs));

    for (const day of days) {
        const column = element("div", "chart-day");
        column.title = `${day.date.toLocaleDateString()}: ${day.sessions} sessions, ${day.runs} executions`;

        const bars = element("div", "bars");
        const sessionBar = element("span", "bar");
        const runBar = element("span", "bar run");

        sessionBar.style.height = `${day.sessions ? Math.max(4, day.sessions * 100 / maxSessions) : 0}%`;
        runBar.style.height = `${day.runs ? Math.max(4, day.runs * 100 / maxRuns) : 0}%`;

        bars.append(sessionBar, runBar);

        const label = element("small");
        label.textContent = String(day.date.getDate()).padStart(2, "0");
        column.append(bars, label);
        chart.append(column);
    }
}

function renderPackages(state) {
    const list = document.getElementById("topPackages");
    list.replaceChildren();

    const packages = [...state.packages].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])).slice(0, 5);
    document.getElementById("noPackages").hidden = packages.length > 0;

    for (const [name, count] of packages) {
        const item = element("li");
        const packageName = element("span");
        packageName.textContent = name;
        packageName.title = name;

        const usageCount = element("strong");
        usageCount.textContent = count.toLocaleString();

        item.append(packageName, usageCount);
        list.append(item);
    }
}

function renderGraphs(state) {
    const list = document.getElementById("topGraphs");
    list.replaceChildren();

    const graphs = [...state.graphs].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])).slice(0, 5);
    document.getElementById("noGraphs").hidden = graphs.length > 0;

    for (const [name, count] of graphs) {
        const item = element("li");
        const graphName = element("span");
        graphName.textContent = name;
        graphName.title = name;

        const graphCount = element("strong");
        graphCount.textContent = count.toLocaleString();

        item.append(graphName, graphCount);
        list.append(item);
    }
}

function renderSessions(state) {
    const body = document.getElementById("sessionRows");
    body.replaceChildren();

    const sessions = [...state.sessions.values()].sort((a, b) => (b.start || b.last) - (a.start || a.last)).slice(0, 50);
    if (!sessions.length) {
        const row = element("tr");
        const cell = element("td", "muted");
        cell.colSpan = 9;
        cell.textContent = "No sessions loaded.";
        row.append(cell);
        body.append(row);
        return;
    }

    for (const session of sessions) {
        const row = element("tr");
        row.dataset.sessionId = session.id;
        row.tabIndex = 0;
        row.title = "Open session details";
        row.addEventListener("click", () => openSessionDialog(session));
        row.addEventListener("keydown", event => {
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                openSessionDialog(session);
            }
        });

        addCell(row, session.start ? session.start.toLocaleString() : "Unknown");

        const statusCell = element("td");
        const badge = element("span", `session-status ${session.start && session.end ? "" : "incomplete"}`.trim());
        badge.textContent = session.start && session.end ? "Complete" : "Incomplete";
        statusCell.append(badge);
        row.append(statusCell);

        const identityCell = element("td");
        const user = element("strong");
        user.textContent = session.user || "Unknown";
        const machine = element("small");
        machine.textContent = session.machine || "Unknown";
        identityCell.append(user, machine);
        row.append(identityCell);

        addCell(row, session.host);
        addCell(row, session.dynamo);
        addCell(row, session.extension);
        addCell(row, session.graphs.toLocaleString());
        addCell(row, session.runs.toLocaleString());

        const duration = session.start && session.end ? `${((session.end - session.start) / 60000).toFixed(1).replace(".0", "")} min` : "—";
        addCell(row, duration);
        body.append(row);
    }
}

function aggregateSessionData(sessionEvents) {
    const events = [...(Array.isArray(sessionEvents) ? sessionEvents : [])].sort((a, b) => {
        const left = a && a.occurred_utc ? new Date(a.occurred_utc).getTime() : 0;
        const right = b && b.occurred_utc ? new Date(b.occurred_utc).getTime() : 0;
        return left - right;
    });

    const graphs = new Map();
    const packages = new Map();
    const nodeTypeTotals = { packages: 0, custom: 0, zeroTouch: 0 };
    const recentGraphKeys = new Map();

    let graphsOpened = 0;
    let executions = 0;
    let skippedExecutions = 0;
    let startTime = null;
    let endTime = null;
    let summaryLocation = null;
    let summaryFileSize = null;
    let summaryStartNodeCount = null;
    let summaryFinalNodeCount = null;

    for (const record of events) {
        if (!record || typeof record.event !== "string") continue;

        const occurred = record.occurred_utc ? new Date(record.occurred_utc) : null;
        if (record.event === "session.started") {
            if (occurred && (!startTime || occurred < startTime)) startTime = occurred;
        }
        if (record.event === "session.ended") {
            if (occurred && (!endTime || occurred > endTime)) endTime = occurred;
        }

        const graphData = record.data?.graph || record.data || {};
        if (isTemporaryHomeGraph(graphData, record.data)) {
            continue;
        }

        if (record.event === "graph.opened") graphsOpened++;
        if (record.event === "execution.ended" || record.event === "graph.execution.completed") {
            if (record.data?.evaluation_took_place === false) {
                skippedExecutions++;
            } else {
                executions++;
            }
        }

        const graphIdentity = resolveGraphIdentity(record, graphData) || deriveGraphFallbackKey(record, graphData, recentGraphKeys);
        if (graphIdentity) {
            const graphKey = graphIdentity;
            const existing = graphs.get(graphKey) || {
                key: graphKey,
                name: null,
                path: null,
                nodes: null,
                saved: false,
                fileSize: null,
                location: null,
                startedAt: null,
                endedAt: null,
                executions: 0,
                packages: new Map()
            };

            if (record.event === "graph.opened" || record.event === "graph.saved") {
                const signature = deriveGraphSignature(record, graphData);
                if (signature) recentGraphKeys.set(signature, graphKey);
            }

            const candidateName = resolveGraphName(graphData, record.data, record);
            const candidatePath = resolveGraphPath(graphData, record.data);
            const candidateNodeCount = resolveGraphNodeCount(graphData, record.data);
            const explicitSaved = resolveGraphSaved(graphData, record.data);
            const candidateSaved = record.event === "graph.saved" || explicitSaved === true || (explicitSaved == null && candidatePath !== "Not saved");
            const meaningfulSnapshot = hasMeaningfulGraphSnapshot(graphData);
            const candidateLocation = firstDefinedString(record.data?.file_path, graphData.file_path, record.data?.location, graphData.location, record.data?.path, graphData.path);
            const candidateFileSize = firstDefinedNumber(record.data?.file_size_bytes, graphData.file_size_bytes, record.data?.file_size, graphData.file_size, record.data?.size_bytes, graphData.size_bytes);

            if (candidatePath && candidatePath !== "Not saved") {
                existing.path = candidatePath;
            }
            if (candidateName && candidateName !== "Untitled graph" && candidateName !== "Untitled") {
                existing.name = candidateName;
            } else if (!existing.name || existing.name === "Untitled graph" || existing.name === "Untitled") {
                existing.name = candidateName || existing.name || "Untitled graph";
            }
            if (candidateNodeCount != null) {
                const shouldKeepExistingNodeCount = candidateNodeCount === 0 && !meaningfulSnapshot && existing.nodes != null;
                if (!shouldKeepExistingNodeCount) {
                    existing.nodes = candidateNodeCount;
                }
            }
            if (candidateSaved) existing.saved = true;
            if (candidateLocation) existing.location = candidateLocation;
            if (candidateFileSize != null) existing.fileSize = candidateFileSize;
            if (occurred) {
                if (!existing.startedAt || occurred < existing.startedAt) existing.startedAt = occurred;
                if (!existing.endedAt || occurred > existing.endedAt) existing.endedAt = occurred;
            }

            if ((record.event === "execution.ended" || record.event === "graph.execution.completed") && record.data?.evaluation_took_place !== false) {
                existing.executions += 1;
            }

            const currentPackageEntries = normalizePackages(graphData.packages || record.data?.packages || []);
            if (currentPackageEntries.length) {
                const refreshedPackages = new Map();
                for (const packageInfo of currentPackageEntries) {
                    const packageKey = `${packageInfo.name}::${packageInfo.version || ""}`;
                    refreshedPackages.set(packageKey, {
                        name: packageInfo.name,
                        version: packageInfo.version || null,
                        nodeCount: Number.isFinite(packageInfo.nodeCount) ? packageInfo.nodeCount : 0,
                        description: packageInfo.description || null,
                        license: packageInfo.license || null,
                        repositoryUrl: packageInfo.repositoryUrl || null,
                        siteUrl: packageInfo.siteUrl || null
                    });
                }
                existing.packages = refreshedPackages;
            }

            graphs.set(graphKey, existing);
        }

        const location = firstDefinedString(record.data?.file_path, graphData.file_path, record.data?.location, graphData.location, record.data?.path, graphData.path);
        if (location) summaryLocation = location;

        const fileSize = firstDefinedNumber(record.data?.file_size_bytes, graphData.file_size_bytes, record.data?.file_size, graphData.file_size, record.data?.size_bytes, graphData.size_bytes);
        if (fileSize != null) summaryFileSize = fileSize;

        const startNodeCount = firstDefinedNumber(record.data?.start_node_count, graphData.start_node_count, record.data?.startNodeCount, graphData.startNodeCount);
        if (startNodeCount != null) summaryStartNodeCount = startNodeCount;

        const finalNodeCount = firstDefinedNumber(record.data?.final_node_count, graphData.final_node_count, record.data?.finalNodeCount, graphData.finalNodeCount, graphData.node_count, record.data?.node_count);
        if (finalNodeCount != null && (finalNodeCount !== 0 || hasMeaningfulGraphSnapshot(graphData) || summaryFinalNodeCount == null)) {
            summaryFinalNodeCount = finalNodeCount;
        }

        const counts = parseNodeTypeCounts(record.data || {}, graphData);
        if (counts && (counts.packages != null || counts.custom != null || counts.zeroTouch != null)) {
            if (counts.packages != null) nodeTypeTotals.packages += counts.packages;
            if (counts.custom != null) nodeTypeTotals.custom += counts.custom;
            if (counts.zeroTouch != null) nodeTypeTotals.zeroTouch += counts.zeroTouch;
        }
    }

    const orderedGraphs = [...graphs.values()].sort((a, b) => (a.name || "").localeCompare(b.name || "")).map(graph => ({
        ...graph,
        packageSummary: summarizeGraphPackages(graph.packages),
        packageDetails: summarizeGraphPackageDetails(graph.packages),
        duration: graph.startedAt && graph.endedAt ? `${((graph.endedAt - graph.startedAt) / 60000).toFixed(1).replace(".0", "")} min` : "—",
        fileSize: graph.fileSize,
        location: graph.location || "Not available"
    }));

    const packageSummaryMap = new Map();
    for (const graph of graphs.values()) {
        for (const item of graph.packages.values()) {
            const key = `${item.name}::${item.version || ""}`;
            const existingPackage = packageSummaryMap.get(key) || {
                name: item.name,
                version: item.version || null,
                description: item.description || null,
                license: item.license || null,
                repositoryUrl: item.repositoryUrl || null,
                siteUrl: item.siteUrl || null,
                nodeCount: 0
            };
            existingPackage.nodeCount += Number.isFinite(item.nodeCount) ? item.nodeCount : 0;
            packageSummaryMap.set(key, existingPackage);
        }
    }
    const orderedPackages = [...packageSummaryMap.values()].sort((a, b) => a.name.localeCompare(b.name) || (a.version || "").localeCompare(b.version || ""));

    const duration = startTime && endTime ? `${((endTime - startTime) / 60000).toFixed(1).replace(".0", "")} min` : null;

    return {
        duration,
        graphsOpened,
        executions,
        skippedExecutions,
        graphs: orderedGraphs,
        packages: orderedPackages,
        summary: {
            location: summaryLocation,
            fileSize: summaryFileSize,
            startNodeCount: summaryStartNodeCount,
            finalNodeCount: summaryFinalNodeCount,
            nodeTypeCounts: {
                packages: nodeTypeTotals.packages || null,
                custom: nodeTypeTotals.custom || null,
                zeroTouch: nodeTypeTotals.zeroTouch || null
            }
        }
    };
}

function resolveGraphIdentity(record, graphData) {
    const data = record.data || {};
    const graph = graphData || {};
    const nested = graphData && typeof graphData === "object" ? graphData : {};

    const candidates = [
        nested.graph_id,
        data.graph_id,
        nested.workspace_id,
        data.workspace_id,
        nested.file_path,
        data.file_path,
        nested.path,
        data.path,
        nested.name,
        data.name,
        nested.id,
        data.id,
        record?.data?.graph?.graph_id,
        record?.data?.graph_id
    ];

    for (const candidate of candidates) {
        if (candidate != null && String(candidate).trim()) return String(candidate).trim();
    }

    return null;
}

function deriveGraphFallbackKey(record, graphData, recentGraphKeys) {
    const signature = deriveGraphSignature(record, graphData);
    if (!signature) return null;
    return recentGraphKeys.get(signature) || signature;
}

function deriveGraphSignature(record, graphData) {
    const graph = graphData || {};
    const data = record.data || {};
    const nested = record.data?.graph || {};

    const candidates = [
        graph.file_path,
        data.file_path,
        nested.file_path,
        graph.path,
        data.path,
        nested.path,
        graph.workspace_id,
        data.workspace_id,
        nested.workspace_id,
        graph.name,
        data.name,
        nested.name,
        graph.graph_id,
        data.graph_id,
        nested.graph_id
    ];

    const first = candidates.find(value => value != null && String(value).trim());
    return first == null ? null : String(first).trim();
}

function resolveGraphName(graphData, data, record) {
    const graph = graphData || {};
    const fallback = data || {};
    const nested = record?.data?.graph || {};
    const pathCandidates = [
        graph.file_path,
        fallback.file_path,
        nested.file_path,
        graph.path,
        fallback.path,
        nested.path
    ];

    const pathName = pathCandidates.find(value => typeof value === "string" && value.trim());
    if (typeof pathName === "string" && pathName.trim()) {
        const normalized = safeGraphName(pathName);
        if (normalized && normalized !== "Untitled graph") return normalized;
    }

    const nameCandidates = [
        graph.name,
        fallback.name,
        nested.name,
        record?.data?.name
    ];

    for (const candidate of nameCandidates) {
        if (typeof candidate === "string" && candidate.trim()) {
            const normalized = safeGraphName(candidate);
            if (normalized && normalized !== "Untitled graph" && normalized !== "Untitled") return normalized;
        }
    }

    return "Untitled graph";
}

function resolveGraphPath(graphData, data) {
    const graph = graphData || {};
    const fallback = data || {};
    const candidates = [graph.file_path, fallback.file_path, graph.path, fallback.path, "Not saved"];

    for (const candidate of candidates) {
        if (typeof candidate === "string" && candidate.trim()) return candidate.trim();
    }

    return "Not saved";
}

function resolveGraphNodeCount(graphData, data) {
    const graph = graphData || {};
    const fallback = data || {};
    const nested = graphData && graphData.graph ? graphData.graph : {};

    const direct = firstDefinedNumber(
        graph.node_count,
        fallback.node_count,
        nested.node_count,
        graph.final_node_count,
        fallback.final_node_count,
        nested.final_node_count,
        graph.start_node_count,
        fallback.start_node_count,
        nested.start_node_count
    );

    if (direct != null && direct > 0) return direct;

    const packageTotal = sumPackageNodeCounts(graph.packages, fallback.packages, nested.packages);
    if (packageTotal > 0) return packageTotal;

    return direct ?? packageTotal ?? null
}

function hasMeaningfulGraphSnapshot(graphData) {
    const graph = graphData || {};
    const name = firstDefinedString(graph.name);
    const path = firstDefinedString(graph.file_path, graph.path, graph.location);
    const fileSize = firstDefinedNumber(graph.file_size_bytes, graph.file_size, graph.size_bytes);
    const nodeCount = firstDefinedNumber(graph.node_count, graph.final_node_count, graph.start_node_count);
    const packages = Array.isArray(graph.packages) ? graph.packages : [];

    return Boolean(name || path || fileSize != null || (nodeCount != null && nodeCount > 0) || packages.length);
}

function sumPackageNodeCounts(...packageCollections) {
    let total = 0;

    for (const collection of packageCollections) {
        if (!Array.isArray(collection)) continue;
        for (const item of collection) {
            if (!item || typeof item !== "object") continue;
            const nodeCount = Number(item.node_count ?? item.nodeCount);
            if (Number.isFinite(nodeCount)) total += nodeCount;
        }
    }

    return total;
}

function resolveGraphSaved(graphData, data) {
    const graph = graphData || {};
    const fallback = data || {};
    const saved = graph.is_saved ?? fallback.is_saved ?? graph.saved ?? fallback.saved;
    if (saved == null) return null;
    return Boolean(saved);
}

function isTemporaryHomeGraph(graphData, data) {
    const graph = graphData || {};
    const fallback = data || {};
    const name = String(graph.name ?? fallback.name ?? "").trim();
    const filePath = firstDefinedString(
        graph.file_path,
        fallback.file_path,
        graph.path,
        fallback.path,
        graph.location,
        fallback.location
    );
    const saved = resolveGraphSaved(graph, fallback);
    const nodeCount = firstDefinedNumber(
        graph.node_count,
        fallback.node_count,
        graph.final_node_count,
        fallback.final_node_count,
        graph.start_node_count,
        fallback.start_node_count
    );
    const isHomeName = name.toLowerCase() === "home";
    const hasMeaningfulPath = typeof filePath === "string" && filePath.trim() && filePath !== "Not saved";
    const isNotSaved = saved !== true;
    const isZeroOrEmpty = nodeCount == null || nodeCount === 0;

    return isHomeName && !hasMeaningfulPath && isNotSaved && isZeroOrEmpty;
}

function openSessionDialog(session) {
    const sessionEvents = Array.isArray(session.rawRecords) ? session.rawRecords : [];
    let sessionAggregate = aggregateSessionData(sessionEvents);

    setText("dialogTitle", session.start ? session.start.toLocaleString() : session.id);

    const details = document.getElementById("sessionDetails");
    details.replaceChildren();

    const duration = sessionAggregate.duration || "In progress or missing end event";
    const values = [
        ["User", session.user || "Unknown"],
        ["Machine", session.machine || "Unknown"],
        ["Host", session.host],
        ["Dynamo", session.dynamo],
        ["Extension", session.extension],
        ["Duration", duration],
        ["Graphs opened", sessionAggregate.graphsOpened.toLocaleString()],
        ["Executions", sessionAggregate.executions.toLocaleString()],
        ["Skipped evaluations", sessionAggregate.skippedExecutions.toLocaleString()]
    ];

    for (const [label, value] of values) {
        const detail = element("div", "detail");
        const labelNode = element("span");
        labelNode.textContent = label;

        const valueNode = element("strong");
        valueNode.textContent = value;

        detail.append(labelNode, valueNode);
        details.append(detail);
    }

    const graphRows = document.getElementById("sessionGraphs");
    graphRows.replaceChildren();
    const graphs = sessionAggregate.graphs;

    if (!graphs.length) {
        const empty = element("div", "muted");
        empty.textContent = "No graph information recorded.";
        graphRows.append(empty);
    }

    for (const graph of graphs) {
        const card = element("article", "graph-card");

        const header = element("div", "graph-card-header");
        const title = element("h4");
        title.textContent = graph.name || "Untitled graph";

        const badge = element("span", "session-status");
        badge.textContent = graph.saved ? "Saved" : "Unsaved";
        header.append(title, badge);

        const meta = element("div", "graph-card-meta");
        const details = [
            ["Location", graph.location || "Not available"],
            ["Final nodes", graph.nodes == null ? "Not available" : graph.nodes.toLocaleString()],
            ["Duration", graph.duration || "—"],
            ["Executions", graph.executions.toLocaleString()],
            ["File size", formatFileSize(graph.fileSize)],
            ["Saved", graph.saved ? "Yes" : "No"]
        ];

        for (const [label, value] of details) {
            const row = element("div");
            const labelNode = element("span");
            labelNode.textContent = label;
            const valueNode = element("strong");
            valueNode.textContent = value;
            row.append(labelNode, valueNode);
            meta.append(row);
        }

        const packageWrap = element("div", "package-table-wrap");
        const table = element("table");
        const thead = element("thead");
        const headRow = element("tr");
        headRow.append(addHeadCell("Package"), addHeadCell("Version"), addHeadCell("Nodes"));
        thead.append(headRow);
        table.append(thead);

        const tbody = element("tbody");
        const packageEntries = graph.packages && graph.packages.size ? [...graph.packages.values()].sort((a, b) => a.name.localeCompare(b.name)) : [];

        if (!packageEntries.length) {
            const row = element("tr");
            const cell = element("td");
            cell.colSpan = 3;
            cell.textContent = "None (0)";
            row.append(cell);
            tbody.append(row);
        }

        for (const item of packageEntries) {
            const row = element("tr");
            addCell(row, item.name);
            addCell(row, item.version || "Unknown");
            addCell(row, item.nodeCount.toLocaleString());
            tbody.append(row);
        }

        table.append(tbody);
        packageWrap.append(table);

        card.append(header, meta, packageWrap);
        graphRows.append(card);
    }

    sessionDialog.showModal();
}

function resetDashboard() {
    if (sessionDialog.open) sessionDialog.close();
    folderInput.value = "";
    render(newState());

    const status = document.getElementById("loadStatus");
    status.className = "status";
    status.textContent = "Waiting for logs";
}

function normalizePackages(packages) {
    if (!Array.isArray(packages)) return [];

    return packages
        .filter(packageInfo => packageInfo && packageInfo.name)
        .map(packageInfo => ({
            name: String(packageInfo.name),
            version: packageInfo.version ? String(packageInfo.version) : null,
            nodeCount: Number.isFinite(packageInfo.node_count) ? packageInfo.node_count : 0,
            description: packageInfo.description ? String(packageInfo.description) : null,
            license: packageInfo.license ? String(packageInfo.license) : null,
            repositoryUrl: packageInfo.repository_url ? String(packageInfo.repository_url) : null,
            siteUrl: packageInfo.site_url ? String(packageInfo.site_url) : null
        }));
}

function packageKey(packageInfo) {
    return packageInfo.version ? `${packageInfo.name} ${packageInfo.version}` : packageInfo.name;
}

function eventSummary(record, graphName) {
    if (record.event === "session.started") {
        const app = record.data?.application;
        return [app?.host_name, app?.host_version, app?.dynamo_version ? `Dynamo ${app.dynamo_version}` : null].filter(Boolean).join(" · ");
    }

    if (record.event === "session.ended") return "Session ended";

    if (record.event === "execution.ended" || record.event === "graph.execution.completed") {
        if (record.data?.evaluation_took_place === false) {
            return `${graphName} · Evaluation skipped`;
        }

        const result = record.data?.successful === true ? "Successful" : "Unsuccessful";
        return `${graphName} · ${result}`;
    }

    if (graphName !== "Untitled graph") {
        const nodeCount = record.data?.graph?.node_count ?? record.data?.node_count;
        return `${graphName}${Number.isFinite(nodeCount) ? ` · ${nodeCount} nodes` : ""}`;
    }

    return "";
}

function collectSessionSummary(session, record, graph) {
    const data = record.data || {};
    const graphData = data.graph || graph || {};
    const summary = session.summary;

    const location = firstDefinedString(data.file_path, graphData.file_path, data.location, graphData.location, data.path, graphData.path);
    if (location) summary.location = location;

    const fileSize = firstDefinedNumber(data.file_size_bytes, graphData.file_size_bytes, data.file_size, graphData.file_size, data.size_bytes, graphData.size_bytes);
    if (fileSize != null) summary.fileSize = fileSize;

    const startNodeCount = firstDefinedNumber(data.start_node_count, graphData.start_node_count, data.startNodeCount, graphData.startNodeCount);
    if (startNodeCount != null) summary.startNodeCount = startNodeCount;

    const finalNodeCount = firstDefinedNumber(data.final_node_count, graphData.final_node_count, data.finalNodeCount, graphData.finalNodeCount, graphData.node_count, data.node_count);
    if (finalNodeCount != null) summary.finalNodeCount = finalNodeCount;

    const nodeTypeCounts = parseNodeTypeCounts(data, graphData);
    if (nodeTypeCounts) {
        summary.nodeTypeCounts = mergeNodeTypeCounts(summary.nodeTypeCounts, nodeTypeCounts);
    }
}

function parseNodeTypeCounts(data, graphData) {
    const candidates = [
        data.node_type_counts,
        graphData.node_type_counts,
        data.node_types,
        graphData.node_types,
        data.nodeTypeCounts,
        graphData.nodeTypeCounts
    ];

    for (const candidate of candidates) {
        if (!candidate || typeof candidate !== "object") continue;

        const counts = {
            packages: normalizeCount(candidate.packages ?? candidate.package ?? candidate.package_nodes ?? candidate.packageNodes),
            custom: normalizeCount(candidate.custom ?? candidate.custom_nodes ?? candidate.customNodes),
            zeroTouch: normalizeCount(candidate.zero_touch ?? candidate.zeroTouch ?? candidate.zero_touch_nodes ?? candidate.zeroTouchNodes)
        };

        if (counts.packages != null || counts.custom != null || counts.zeroTouch != null) {
            return counts;
        }
    }

    return { packages: null, custom: null, zeroTouch: null };
}

function mergeNodeTypeCounts(current, incoming) {
    return {
        packages: incoming.packages ?? current.packages,
        custom: incoming.custom ?? current.custom,
        zeroTouch: incoming.zeroTouch ?? current.zeroTouch
    };
}

function normalizeCount(value) {
    if (value == null || value === "") return null;
    const numeric = Number(value);
    return Number.isFinite(numeric) ? numeric : null;
}

function firstDefinedString(...values) {
    for (const value of values) {
        if (typeof value === "string" && value.trim()) return value.trim();
        if (typeof value === "number") return String(value);
    }
    return null;
}

function firstDefinedNumber(...values) {
    for (const value of values) {
        if (value == null || value === "") continue;
        const number = Number(value);
        if (Number.isFinite(number)) return number;
    }
    return null;
}

function summarizeGraphPackages(packageMap) {
    const packages = [...(packageMap?.values?.() || [])].sort((a, b) => a.name.localeCompare(b.name));
    if (!packages.length) return "None (0)";

    const packageNames = packages.map(item => item.name);
    const countLabel = packageNames.length.toLocaleString();
    return `${countLabel} - ${packageNames.join(", ")}`;
}

function summarizeGraphPackageDetails(packageMap) {
    const packages = [...(packageMap?.values?.() || [])].sort((a, b) => a.name.localeCompare(b.name));
    if (!packages.length) return "None (0)";

    return packages
        .map(item => `${item.name} ${item.version ? `v${item.version}` : "v?"} (${item.nodeCount.toLocaleString()} nodes)`)
        .join("; ");
}

function formatOptionalNumber(value) {
    return value == null ? "Not available" : numberFormatter.format(value);
}

function formatOptionalText(value) {
    return value ? value : "Not available";
}

function formatFileSize(bytes) {
    if (bytes == null || bytes < 0) return "Not available";
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function safeGraphName(value) {
    if (!value) return "Untitled graph";
    return String(value).split(/[\\/]/).pop() || "Untitled graph";
}

function dateKey(date) {
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

function increment(map, key) {
    map.set(key, (map.get(key) || 0) + 1);
}

function setText(id, value) {
    document.getElementById(id).textContent = value;
}

function element(tag, className) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    return node;
}

function addCell(row, value) {
    const cell = element("td");
    cell.textContent = value;
    row.append(cell);
}

function addHeadCell(value) {
    const cell = element("th");
    cell.textContent = value;
    return cell;
}

function addLinkCell(row, value) {
    const cell = element("td");
    if (value && /^https?:\/\//i.test(value)) {
        const link = element("a");
        link.href = value;
        link.target = "_blank";
        link.rel = "noopener noreferrer";
        link.textContent = "Open";
        link.addEventListener("click", event => event.stopPropagation());
        cell.append(link);
    } else {
        cell.textContent = "—";
    }
    row.append(cell);
}

render(newState());
