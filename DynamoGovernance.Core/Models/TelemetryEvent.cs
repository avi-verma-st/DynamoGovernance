using System;
using System.Text.Json.Serialization;

namespace DynamoGovernance.Core.Models
{
    public class TelemetryEvent
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "1.0";
        [JsonPropertyName("event_id")]
        public Guid EventId { get; set; } = Guid.NewGuid();
        [JsonPropertyName("session_id")]
        public Guid SessionId { get; set; }
        [JsonPropertyName("timestamp_utc")]
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;
        [JsonPropertyName("machine_id")]
        public string MachineId { get; set; } = string.Empty;
        [JsonPropertyName("host_application")]
        public string HostApplication { get; set; } = string.Empty;
        [JsonPropertyName("dynamo_version")]
        public string DynamoVersion { get; set; } = string.Empty;
        [JsonPropertyName("graph_id")]
        public Guid? GraphId { get; set; }
        [JsonPropertyName("outcome")]
        public string Outcome { get; set; } = "success";
        [JsonPropertyName("error_details")]
        public string? ErrorDetails { get; set; }
    }
}
