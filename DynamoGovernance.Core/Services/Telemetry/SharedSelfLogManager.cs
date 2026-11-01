using Serilog.Debugging;

namespace DynamoGovernance.Core.Services.Telemetry;

/// <summary>
/// Serilog's <see cref="SelfLog"/> is a single global static sink, so it must not be
/// independently enabled/disabled by each <see cref="TelemetryLogger"/> instance -
/// doing so would let one instance's Dispose() silently turn off diagnostics for
/// every other Serilog user in the Dynamo process. This class ref-counts active
/// users and only disables the global SelfLog output once nobody is using it,
/// while redacting known-sensitive header/token substrings before they reach disk.
/// </summary>
internal static class SharedSelfLogManager
{
    private static readonly object Gate = new();
    private static int _activeUsers;

    public static void AddUser(Action<string> onMessage)
    {
        lock (Gate)
        {
            if (_activeUsers == 0)
            {
                SelfLog.Enable(message => onMessage(Redact(message)));
            }

            _activeUsers++;
        }
    }

    public static void RemoveUser()
    {
        lock (Gate)
        {
            if (_activeUsers == 0)
            {
                return;
            }

            _activeUsers--;

            if (_activeUsers == 0)
            {
                SelfLog.Disable();
            }
        }
    }

    private static string Redact(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        foreach (string sensitiveMarker in SensitiveMarkers)
        {
            int index = message.IndexOf(sensitiveMarker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                return message[..(index + sensitiveMarker.Length)] + " [REDACTED]";
            }
        }

        return message;
    }

    private static readonly string[] SensitiveMarkers =
    [
        "x-functions-key",
        "authorization",
        "bearer "
    ];
}
