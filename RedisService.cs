using Microsoft.Extensions.Options;
using Redis.OM.Searching;
using Redis.OM;
using TrafficDataCollection.Api.Models.Cache;
using TrafficDataCollection.Api.Repository.Interfaces;
using TrafficDataCollection.Api.Services.Interfaces;
using TrafficDataCollection.Api.Models.Entities;

namespace TrafficDataCollection.Api.Services
{
    public class RedisService : IRedisService, IDisposable
    {
        private readonly RedisConnectionProvider _provider;
        private readonly IRedisCollection<TrafficLightRedisModel> _trafficLights;
        private readonly ILightRepository _lightRepository;
        private readonly ILogger<RedisService> _logger;
        private const string INDEX_NAME = "traffic-light-idx";
        private readonly string _prefixKey;

        public RedisService(
            IOptions<RedisSettings> redisSettings,
            ILightRepository lightRepository,
            ILogger<RedisService> logger)
        {
            var host = redisSettings.Value.Hosts;
            var port = redisSettings.Value.Ports;
            var password = redisSettings.Value.Password;

            var connectionString = $"redis://:{password}@{host}:{port}";

            _provider = new RedisConnectionProvider(connectionString);
            _trafficLights = _provider.RedisCollection<TrafficLightRedisModel>();
            _lightRepository = lightRepository;
            _logger = logger;
            _prefixKey = redisSettings.Value.PrefixKey ?? "";
        }

        public async Task<bool> CreateIndexIfNotExistsAsync()
        {
            try
            {
                await _provider.Connection.CreateIndexAsync(typeof(TrafficLightRedisModel));
                _logger.LogInformation("Successfully created or updated Redis.OM index for traffic lights");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Redis.OM index for traffic lights");
                return false;
            }
        }

        public async Task<bool> LoadTrafficLightsToRedisAsync()
        {
            try
            {
                // Get all lights
                var lights = await _lightRepository.GetAllLightsAsync();
                var count = 0;

                // Track IDs for existing records
                var existingIds = new HashSet<string>(_trafficLights.Select(t => t.Id));

                foreach (var light in lights)
                {
                    // Get associated cycle light data
                    var cycleLight = await _lightRepository.GetCycleLightByLightIdAsync(light.Id);

                    // Skip if no cycle light data is available
                    if (cycleLight == null)
                    {
                        _logger.LogWarning($"No cycle light data found for light ID {light.Id}, skipping");
                        continue;
                    }

                    // Create combined model
                    var trafficLightModel = TrafficLightRedisModel.FromEntities(light, cycleLight);

                    // Check if record already exists
                    var id = $"{_prefixKey}traffic_light:{light.Id}";
                    trafficLightModel.Id = id; // Ensure the correct ID with prefix

                    if (existingIds.Contains(id))
                    {
                        // Update existing record
                        await _trafficLights.UpdateAsync(trafficLightModel);
                    }
                    else
                    {
                        // Insert new record
                        await _trafficLights.InsertAsync(trafficLightModel);
                    }

                    count++;
                }

                _logger.LogInformation($"Successfully loaded {count} traffic lights to Redis");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading traffic lights to Redis");
                return false;
            }
        }

        public async Task<TrafficLightRedisModel> GetTrafficLightByIdAsync(int lightId)
        {
            try
            {
                return await _trafficLights.Where(x => x.Id == $"traffic_light:{lightId}").FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting traffic light {lightId} from Redis");
                return null;
            }
        }

        public async Task<IEnumerable<TrafficLightRedisModel>> SearchTrafficLightsAsync(double lat, double lng, double radius)
        {
            try
            {
                // Use Redis.OM's GeoLoc functionality for more efficient geo-radius search
                // Note: radius is in kilometers for GeoFilter
                var radiusKm = radius * 111.12; // Convert approximate degrees to km (1 degree ≈ 111.12 km)

                // Create a geospatial query using the GeoLoc field
                var results = _trafficLights
                    .GeoFilter(light => light.Geom, lng, lat, radiusKm, GeoLocDistanceUnit.Kilometers)
                    .OrderBy(light => light.Name)
                    .Take(100);

                return await Task.FromResult(results.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching traffic lights in Redis");
                return new List<TrafficLightRedisModel>();
            }
        }

        public void Dispose()
        {
            _provider?.Connection?.Dispose();
        }
    }
}
