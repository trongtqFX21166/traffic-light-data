using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TrafficDataCollection.Api.Models.Entities
{
    // AirflowDagRun model
    public class AirflowDagRun
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; }

        public string Id { get; set; }
        public string AirflowDagId { get; set; }
        public string AirflowDagRunId { get; set; }
        public string AirflowTaskId { get; set; }
        public string PreviousRunId { get; set; }
        public DateTime ExecutionDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; }
        public string Region { get; set; }
        public string Priority { get; set; }
        public int TotalCommands { get; set; }
        public int CompletedCommands { get; set; }
        public int TotalTrafficLights { get; set; }
        public int ProcessedTrafficLights { get; set; }
        public DateTime TimeoutTime { get; set; }
        public string ExecutionParameters { get; set; }
    }
}
