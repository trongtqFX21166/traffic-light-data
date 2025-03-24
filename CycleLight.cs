using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TrafficDataCollection.Api.Models.Entities
{
    public class CycleLight
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; }

        public string Id { get; set; }
        public string CycleLightHistoryId { get; set; }
        public string LightId { get; set; }
        public int RedTime { get; set; }
        public int GreenTime { get; set; }
        public int YellowTime { get; set; }
        public long CycleStartTimestamp { get; set; }
        public DateTime ExpiredTime { get; set; }
        public DateTime LastModified { get; set; }
        public int TimeTick { get; set; }
        public int FramesInSecond { get; set; }
        public int SecondsExtractFrame { get; set; }
        public string Source { get; set; }
        public string Status { get; set; }
        public string ReasonCode { get; set; }
        public string Reason { get; set; }
    }
}
