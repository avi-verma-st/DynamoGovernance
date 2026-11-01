namespace DynamoGovernance.Core.Services.Telemetry;

/// <summary>
/// Minimal, dependency-free loader for ".env" style key=value configuration files.
/// Used so local development/POC testing can supply the Azure Function endpoint
/// URL and function key without requiring real OS/user environment variables to be
/// set (which require a process restart to take effect). Values already present in
/// the real process environment always take precedence and are never overwritten.
/// </summary>
internal static class DotEnvLoader
{
    private static readonly object Gate = new();
    private static bool _loaded;

    /// <summary>
    /// Loads a ".env" file (if present) from the given directory, or - if omitted -
    /// walks upward from the current base directory looking for one. Safe to call
    /// multiple times; only loads once per process.
    /// </summary>
    public static void EnsureLoaded(string? startDirectory = null)
    {
        lock (Gate)
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            string? envFilePath = FindEnvFile(startDirectory ?? GetDefaultStartDirectory());
            if (envFilePath is null)
            {
                return;
            }

            try
            {
                foreach (string rawLine in File.ReadAllLines(envFilePath))
                {
                    string line = rawLine.Trim();

                    if (line.Length == 0 || line.StartsWith('#'))
                    {
                        continue;
                    }

                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    string key = line[..separatorIndex].Trim();
                    string value = line[(separatorIndex + 1)..].Trim();

                    if (value.Length >= 2 &&
                        ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                    {
                        value = value[1..^1];
                    }

                    // Never override a value that is already set in the real
                    // environment (explicit configuration always wins).
                    if (Environment.GetEnvironmentVariable(key) is null)
                    {
                        Environment.SetEnvironmentVariable(key, value);
                    }
                }
            }
            catch
            {
                // Best-effort only: a missing/unreadable .env file should never
                // prevent the application from starting.
            }
        }
    }

    private static string? FindEnvFile(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Returns the directory this assembly (DynamoGovernance.Core.dll) was actually
    /// loaded from. <see cref="AppContext.BaseDirectory"/> reflects the host
    /// process's base directory (e.g. Dynamo's own install folder when this library
    /// is loaded as a plugin/package), not the folder containing this DLL and its
    /// ".env" file, so it cannot be relied on here.
    /// </summary>
    private static string GetDefaultStartDirectory()
    {
        string? assemblyLocation = typeof(DotEnvLoader).Assembly.Location;

        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            string? assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            if (!string.IsNullOrEmpty(assemblyDirectory))
            {
                return assemblyDirectory;
            }
        }

        return AppContext.BaseDirectory;
    }
}
