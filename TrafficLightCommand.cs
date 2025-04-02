using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TrafficDataCollection.AnalyzationResult.Service.Models
{
    // TrafficLightCommand model
    public class TrafficLightCommand
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; }

        public string Id { get; set; }
        public string DagRunId { get; set; }
        public string TaskType { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public string ReasonCode { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int RetryCount { get; set; }
        public DateTime? LastRetryTime { get; set; }
        public int TrafficLightId { get; set; }
        public string CameraId { get; set; }
        public string CommandPayload { get; set; }
    }
}
