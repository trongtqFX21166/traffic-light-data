using System.Text.Json.Serialization;

namespace TrafficDataCollection.Api.Models
{
    public class UpdateCommandStatusRequest
    {
        [JsonPropertyName("command_id")]
        public string CommandId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("reason_code")]
        public string ReasonCode { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; }
    }
}
