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
                // Step 1: Get all main lights (those without main_light_id)
                var mainLights = await _lightRepository.GetAllLightsWithoutMainLightIdAsync();
                _logger.LogInformation($"Found {mainLights.Count()} main traffic lights");

                // Track IDs for existing records
                var existingIds = new HashSet<string>(_trafficLights.Select(t => t.Id));
                var processedCount = 0;

                // Step 2 & 3: Process main lights first
                foreach (var mainLight in mainLights)
                {
                    // Get associated cycle light data for the main light
                    var mainCycleLight = await _lightRepository.GetCycleLightByLightIdAsync(mainLight.Id);

                    // Skip if no cycle light data is available
                    if (mainCycleLight == null)
                    {
                        _logger.LogWarning($"No cycle light data found for main light ID {mainLight.Id}, skipping");
                        continue;
                    }

                    // Create combined model for main light
                    var mainLightModel = TrafficLightRedisModel.FromEntities(mainLight, mainCycleLight);

                    // Set correct ID with prefix
                    var mainLightId = $"{_prefixKey}traffic_light:{mainLight.Id}";
                    mainLightModel.Id = mainLightId;

                    // Update or insert main light
                    if (existingIds.Contains(mainLightId))
                    {
                        await _trafficLights.UpdateAsync(mainLightModel);
                    }
                    else
                    {
                        await _trafficLights.InsertAsync(mainLightModel);
                    }

                    processedCount++;

                    // Step 4 & 5: Get and process dependent lights
                    var dependentLights = await _lightRepository.GetLightsByMainLightIdAsync(mainLight.Id);
                    _logger.LogInformation($"Found {dependentLights.Count()} dependent lights for main light ID {mainLight.Id}");

                    foreach (var dependentLight in dependentLights)
                    {
                        // Clone the main cycle light for this dependent light
                        var dependentCycleLight = CloneCycleLight(mainCycleLight, dependentLight.Id);

                        // Step 5.1 & 5.2: Handle different directions
                        if (dependentLight.MainLightDirection?.ToLower() == "opposite")
                        {
                            // For opposite direction: adjust cycle start timestamp
                            // Move the start time by the duration of green + yellow phases
                            dependentCycleLight.CycleStartTimestamp = mainCycleLight.CycleStartTimestamp +
                                                                     mainCycleLight.GreenTime +
                                                                     mainCycleLight.YellowTime;
                        }
                        // For "Same" direction, we keep the original timing

                        // Create combined model for dependent light
                        var dependentLightModel = TrafficLightRedisModel.FromEntities(dependentLight, dependentCycleLight);

                        // Set correct ID with prefix
                        var dependentLightId = $"{_prefixKey}traffic_light:{dependentLight.Id}";
                        dependentLightModel.Id = dependentLightId;

                        // Update or insert dependent light
                        if (existingIds.Contains(dependentLightId))
                        {
                            await _trafficLights.UpdateAsync(dependentLightModel);
                        }
                        else
                        {
                            await _trafficLights.InsertAsync(dependentLightModel);
                        }

                        processedCount++;
                    }
                }

                _logger.LogInformation($"Successfully loaded {processedCount} traffic lights to Redis");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading traffic lights to Redis");
                return false;
            }
        }

        // Helper method to clone a cycle light for a dependent light
        private CycleLight CloneCycleLight(CycleLight sourceCycleLight, int targetLightId)
        {
            return new CycleLight
            {
                Id = Guid.NewGuid().ToString(),
                LightId = targetLightId,
                RedTime = sourceCycleLight.RedTime,
                GreenTime = sourceCycleLight.GreenTime,
                YellowTime = sourceCycleLight.YellowTime,
                CycleStartTimestamp = sourceCycleLight.CycleStartTimestamp,
                LastModified = DateTime.UtcNow,
                TimeTick = sourceCycleLight.TimeTick,
                FramesInSecond = sourceCycleLight.FramesInSecond,
                SecondsExtractFrame = sourceCycleLight.SecondsExtractFrame,
                Source = sourceCycleLight.Source,
                Status = sourceCycleLight.Status,
                ReasonCode = sourceCycleLight.ReasonCode,
                Reason = "Generated from main light"
            };
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

        public async Task<(IEnumerable<TrafficLightRedisModel>, long)> GetTrafficLightsAsync(
    int page = 1,
    int pageSize = 100)
        {
            try
            {
                // Start with all traffic lights
                var query = _trafficLights.AsQueryable();

                // Get total count for pagination
                long totalCount = query.Count();

                // Apply pagination
                var pagedResults = query
                    .OrderBy(light => light.LightId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                _logger.LogInformation($"Retrieved {pagedResults.Count} traffic lights out of {totalCount} total");

                return (pagedResults, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving traffic lights from Redis");
                return (new List<TrafficLightRedisModel>(), 0);
            }
        }

        public void Dispose()
        {
            _provider?.Connection?.Dispose();
        }
    }
}
