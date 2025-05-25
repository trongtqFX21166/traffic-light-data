namespace TrafficDataCollection.Api.Models.Cache
{
    public class RedisSettings
    {
        public string Hosts { get; set; }
        public string Ports { get; set; }
        public string Password { get; set; }
        public string PrefixKey { get; set; }
    }
}
