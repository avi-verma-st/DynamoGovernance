using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

namespace DynamoGovernance.Core.Services
{
    internal class DiagnosticLogger
    {
        private readonly object _writeLock = new();
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
        public void LogError(string component, string operation, Exception exception)
        {
            try
            {
                DiagnosticRecord record = new()
                {
                    OccurredUtc = DateTimeOffset.UtcNow,
                    Message = exception.Message,
                    Exception = exception.ToString(),
                    Component = component,
                    Operation = operation,
                };
                string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DynamoGovernance", "Diagnostics");
                string logFilePath = Path.Combine(logDirectory,$"diagnostics_{DateTime.UtcNow:yyyyy-MM-dd}.jsonl");
                string json = JsonSerializer.Serialize(record,_jsonOptions);
                lock (_writeLock)
                {
                    Directory.CreateDirectory(logDirectory);
                    File.AppendAllText(
                        logFilePath, json + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {

            }
        }
        private class DiagnosticRecord
        {
            [JsonPropertyName("occurred_utc")]
            public required DateTimeOffset OccurredUtc { get; init; }
            [JsonPropertyName("message")]
            public required string Message { get; set; }
            [JsonPropertyName("exception")]
            public required string Exception { get; set; }
            [JsonPropertyName("component")]
            public required string Component { get; set; }
            [JsonPropertyName("operation")]
            public required string Operation { get; set; }
        }
    }
}
