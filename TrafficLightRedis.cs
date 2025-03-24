using Redis.OM.Modeling;
using System.Text.Json.Serialization;
using TrafficDataCollection.Api.Models.Entities;

namespace TrafficDataCollection.Api.Models.Cache
{

    // Traffic light data model for Redis.OM
    [Document(StorageType = StorageType.Json, Prefixes = new[] { "traffic_light" })]
    public class TrafficLightRedisModel
    {
        [RedisIdField]
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [Indexed]
        [JsonPropertyName("LightId")]
        public int LightId { get; set; }

        [JsonPropertyName("lat")]
        public double Latitude { get; set; }

        [JsonPropertyName("lng")]
        public double Longitude { get; set; }

        [Indexed]
        public GeoLoc Geom { get; set; }

        [Indexed]
        [JsonPropertyName("heading")]
        public int Heading { get; set; }

        [Searchable]
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [Indexed]
        [JsonPropertyName("red_time")]
        public int RedTime { get; set; }

        [Indexed]
        [JsonPropertyName("green_time")]
        public int GreenTime { get; set; }

        [Indexed]
        [JsonPropertyName("yellow_time")]
        public int YellowTime { get; set; }

        [Indexed]
        [JsonPropertyName("cycle_start_timestamp")]
        public long CycleStartTimestamp { get; set; }

        [Indexed]
        [JsonPropertyName("time_tick")]
        public int TimeTick { get; set; }

        // Factory method to create from Light and CycleLight entities
        public static TrafficLightRedisModel FromEntities(Light light, CycleLight cycleLight)
        {
            return new TrafficLightRedisModel
            {
                Id = $"traffic_light:{light.Id}",
                LightId = light.Id,
                Latitude = light.Latitude,
                Longitude = light.Longitude,
                Geom = new GeoLoc(light.Longitude, light.Latitude), // Note: GeoLoc takes (longitude, latitude) order
                Heading = light.Heading,
                Name = light.Name,
                RedTime = cycleLight?.RedTime ?? 0,
                GreenTime = cycleLight?.GreenTime ?? 0,
                YellowTime = cycleLight?.YellowTime ?? 0,
                CycleStartTimestamp = cycleLight?.CycleStartTimestamp ?? 0,
                TimeTick = cycleLight?.TimeTick ?? 0
            };
        }
    }
}
