using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DynamoGovernance.Core.Services
{
    public class DiagnosticLogger
    {
        private static readonly Lazy<Logger> SharedLogger = new(CreateLogger);
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        private static Logger CreateLogger()
        {
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DynamoGovernance",
                "Diagnostics");

            Directory.CreateDirectory(logDirectory);

            string logFilePathTemplate = Path.Combine(logDirectory, "diagnostics_.jsonl");

            return new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Async(sinkConfiguration => sinkConfiguration.File(
                    logFilePathTemplate,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Message:lj}{NewLine}"),
                    blockWhenFull: false)
                .CreateLogger();
        }

        private void WriteRecord(
            string level,
            string component,
            string operation,
            string message,
            string? exception)
        {
            try
            {
                DiagnosticRecord record = new()
                {
                    OccurredUtc = DateTimeOffset.UtcNow,
                    Level = level,
                    Message = message,
                    Exception = exception,
                    Component = component,
                    Operation = operation,
                };

                string json = JsonSerializer.Serialize(record, _jsonOptions);
                LogEventLevel logEventLevel = level == "error" ? LogEventLevel.Error : LogEventLevel.Information;
                SharedLogger.Value.Write(logEventLevel, json);
            }
            catch
            {
                //This will not have any failure safety as this is the last level of logging or consuming an error
            }
        }
        public void LogInformation(string component, string operation, string message)
        {
            WriteRecord(
                "information",
                component,
                operation,
                message,
                null);
        }
        public void LogError(string component, string operation, Exception exception)
        {
            WriteRecord(
                "error",
                component,
                operation,
                exception.Message,
                exception.ToString());
        }
        private class DiagnosticRecord
        {
            [JsonPropertyName("occurred_utc")]
            public required DateTimeOffset OccurredUtc { get; init; }
            [JsonPropertyName("level")]
            public required string Level { get; init; }
            [JsonPropertyName("message")]
            public required string Message { get; set; }
            [JsonPropertyName("exception")]
            public string? Exception { get; set; }
            [JsonPropertyName("component")]
            public required string Component { get; set; }
            [JsonPropertyName("operation")]
            public required string Operation { get; set; }
        }
    }
}
