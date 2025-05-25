using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TrafficDataCollection.Api.Models.Entities
{
    // Add this model class for mapping collection
    public class MappingVietmapLight
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; }

        [BsonElement("vml_light_id")]
        public int VmlLightId { get; set; }

        [BsonElement("map_light_id")]
        public int MapLightId { get; set; }

        [BsonElement("lastModified")]
        public DateTime LastModified { get; set; }

        [BsonElement("lastModifiedBy")]
        public string LastModifiedBy { get; set; }
    }
}
