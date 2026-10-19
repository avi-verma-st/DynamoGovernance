using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

namespace DynamoGovernance.Core.Services
{
    public class DiagnosticLogger
    {
        private static readonly object WriteLock = new();
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
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
                string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DynamoGovernance", "Diagnostics");
                string logFilePath = Path.Combine(logDirectory, $"diagnostics_{DateTime.UtcNow:yyyy-MM-dd}.jsonl");
                string json = JsonSerializer.Serialize(record, _jsonOptions);
                lock (WriteLock)
                {
                    Directory.CreateDirectory(logDirectory);
                    File.AppendAllText(
                        logFilePath, json + Environment.NewLine, Encoding.UTF8);
                }
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
