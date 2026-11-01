using Serilog.Events;
using Serilog.Formatting;

namespace DynamoGovernance.Core.Services.Telemetry;

/// <summary>
/// Writes the raw telemetry JSON (stored as the "TelemetryJson" property on the
/// LogEvent) with no Serilog envelope, timestamp, or level wrapping. Used by both
/// the local JSONL file sink and the HTTP sink so the exact same payload that
/// <see cref="TelemetryLogger.Log{T}"/> serialized is what ends up on disk and in
/// the batched array sent to the Azure Function.
/// </summary>
public sealed class TelemetryJsonFormatter : ITextFormatter
{
    public const string TelemetryJsonPropertyName = "TelemetryJson";

    public void Format(LogEvent logEvent, TextWriter output)
    {
        if (logEvent.Properties.TryGetValue(TelemetryJsonPropertyName, out LogEventPropertyValue? value)
            && value is ScalarValue { Value: string json })
        {
            output.Write(json);
            output.Write(Environment.NewLine);
            return;
        }

        // Fallback: should not normally happen, but avoids losing data if the
        // property is ever missing.
        output.Write(logEvent.RenderMessage());
        output.Write(Environment.NewLine);
    }
}
