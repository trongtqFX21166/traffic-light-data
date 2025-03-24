using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TrafficDataCollection.Api.Models.Entities
{
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
}
