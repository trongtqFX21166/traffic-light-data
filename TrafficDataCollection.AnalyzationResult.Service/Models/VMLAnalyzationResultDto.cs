using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TrafficDataCollection.AnalyzationResult.Service.Models
{
    // DTO for incoming Kafka messages on VML_Analyzation_Result topic
    public class VMLAnalyzationResultDto
    {
        public string SeqId { get; set; }
        public string Id { get; set; } // This is the light id
        public string Type { get; set; }
        public string CameraSource { get; set; }
        public string CameraName { get; set; }
        public string Status { get; set; }
        public string ReasonCode { get; set; }
        public string Reason { get; set; }
        public CycleLightData Data { get; set; }
    }

    public class CycleLightData
    {
        public int RedTime { get; set; }
        public int GreenTime { get; set; }
        public int YellowTime { get; set; }
        public string CycleStartTimestamp { get; set; } // String format like "10.Mar.2025 16:19:39"
        public long CycleStartUnixTimestamp { get; set; } // Unix timestamp in milliseconds
        public int TimeTick { get; set; }
        public double Confidence { get; set; }
    }

    // Entity model for cycle_light collection
    public class CycleLight
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; }

        [BsonElement("id")]
        public string Id { get; set; }

        [BsonElement("cycle_light_history_id")]
        public string CycleLightHistoryId { get; set; }

        [BsonElement("light_id")]
        public int LightId { get; set; }

        [BsonElement("red_time")]
        public int RedTime { get; set; }

        [BsonElement("green_time")]
        public int GreenTime { get; set; }

        [BsonElement("yellow_time")]
        public int YellowTime { get; set; }

        [BsonElement("cycle_start_timestamp")]
        public long CycleStartTimestamp { get; set; }

        [BsonElement("last_modified")]
        public DateTime LastModified { get; set; }

        [BsonElement("time_tick")]
        public int TimeTick { get; set; }

        [BsonElement("frames_in_second")]
        public int FramesInSecond { get; set; }

        [BsonElement("seconds_extract_frame")]
        public int SecondsExtractFrame { get; set; }

        [BsonElement("source")]
        public string Source { get; set; }

        [BsonElement("status")]
        public string Status { get; set; }

        [BsonElement("reason_code")]
        public string ReasonCode { get; set; }

        [BsonElement("reason")]
        public string Reason { get; set; }
    }

    // Entity model for cycle_light_history collection
    public class CycleLightHistory
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; }

        [BsonElement("id")]
        public string Id { get; set; }

        [BsonElement("light_id")]
        public int LightId { get; set; }

        [BsonElement("red_time")]
        public int RedTime { get; set; }

        [BsonElement("green_time")]
        public int GreenTime { get; set; }

        [BsonElement("yellow_time")]
        public int YellowTime { get; set; }

        [BsonElement("cycle_start_timestamp")]
        public long CycleStartTimestamp { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("time_tick")]
        public int TimeTick { get; set; }

        [BsonElement("frames_in_second")]
        public int FramesInSecond { get; set; }

        [BsonElement("seconds_extract_frame")]
        public int SecondsExtractFrame { get; set; }

        [BsonElement("source")]
        public string Source { get; set; }

        [BsonElement("status")]
        public string Status { get; set; }

        [BsonElement("reason_code")]
        public string ReasonCode { get; set; }

        [BsonElement("reason")]
        public string Reason { get; set; }
    }

    // Settings for Microsoft Teams webhook
    public class TeamsWebhookSettings
    {
        public string WebhookUrl { get; set; }
    }
}