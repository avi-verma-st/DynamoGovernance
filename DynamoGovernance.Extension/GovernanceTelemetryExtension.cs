using Dynamo.Extensions;
using DynamoGovernance.Core.Services;
using System.Diagnostics;

namespace DynamoGovernance.Extension;

public class GovernanceTelemetryExtension : IExtension
{
    private GovernanceService? _governanceService;

    public string UniqueId => "F2BA577E-4C5C-4A37-8BFC-2A5C11FAC698";
    public string Name => "Dynamo Governance Telemetry";

    public void Startup(StartupParams sp)
    {
        try
        {
            _governanceService = new GovernanceService();
            (string hostName, string? hostVersion) = GetHostApplication();
            string dynamoVersion = sp.DynamoVersion?.ToString() ?? "unknown";
            string extensionVersion = typeof(GovernanceTelemetryExtension)
                .Assembly
                .GetName()
                .Version?
                .ToString(3) ?? "unknown";

            _governanceService.StartSession(
                dynamoVersion,
                hostName,
                hostVersion,
                extensionVersion);
        }
        catch
        {
            _governanceService = null;
        }
    }

    public void Ready(ReadyParams sp)
    {
        _governanceService?.LogExtensionReady();
    }

    public void Shutdown()
    {
        _governanceService?.EndSession();
    }

    public void Dispose()
    {
        _governanceService?.Dispose();
        _governanceService = null;
    }

    private static (string HostName, string? HostVersion) GetHostApplication()
    {
        try
        {
            using Process process = Process.GetCurrentProcess();
            string hostName = process.ProcessName;
            string? hostVersion = process.MainModule?.FileVersionInfo.ProductVersion;
            return (hostName, hostVersion);
        }
        catch
        {
            return ("unknown", null);
        }
    }
}
