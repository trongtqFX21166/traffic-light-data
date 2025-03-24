using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TrafficDataCollection.Api.Models.Entities
{
    // Light model
    public class Light
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; }


        [BsonElement("id")]
        public int Id { get; set; }

        [BsonElement("lat")]
        public double Latitude { get; set; }

        [BsonElement("lng")]
        public double Longitude { get; set; }

        [BsonElement("heading")]
        public int Heading { get; set; }

        [BsonElement("name")]
        public string Name { get; set; }

        [BsonElement("camera_id")]
        public string CameraId { get; set; }

        [BsonElement("camera_live_url")]
        public string CameraLiveUrl { get; set; }

        [BsonElement("main_light_id")]
        public int? MainLightId { get; set; }

        [BsonElement("main_light_direction")]
        public string MainLightDirection { get; set; }

        [BsonElement("original_id")]
        public int OriginalId { get; set; }

        [BsonElement("bboxes")]
        public List<List<List<int>>> Bboxes { get; set; }

        [BsonElement("camera_source_id")]
        public string CameraSourceId { get; set; }

        [BsonElement("timestampBBox")]
        public List<List<double>> TimestampBBox { get; set; }
    }
}
