using System.Text.Json.Serialization;

namespace TrafficDataCollection.Api.Models
{
    public class CycleLightHistoryDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("light_id")]
        public int LightId { get; set; }

        [JsonPropertyName("red_time")]
        public int RedTime { get; set; }

        [JsonPropertyName("green_time")]
        public int GreenTime { get; set; }

        [JsonPropertyName("yellow_time")]
        public int YellowTime { get; set; }

        [JsonPropertyName("cycle_start_timestamp")]
        public long CycleStartTimestamp { get; set; }

        [JsonPropertyName("cycle_start_time")]
        public string CycleStartTime { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }

        [JsonPropertyName("time_tick")]
        public int TimeTick { get; set; }

        [JsonPropertyName("frames_in_second")]
        public int FramesInSecond { get; set; }

        [JsonPropertyName("seconds_extract_frame")]
        public int SecondsExtractFrame { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("reason_code")]
        public string ReasonCode { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; }

        [JsonPropertyName("cycle_time")]
        public int CycleTime => RedTime + GreenTime + YellowTime;
    }
}
