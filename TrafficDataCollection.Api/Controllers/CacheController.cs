using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using TrafficDataCollection.Api.Models.Cache;
using TrafficDataCollection.Api.Services.Interfaces;

namespace TrafficDataCollection.Api.Controllers
{
    
    [ApiController]
    [ApiVersion(1.0)]
    [Route("v{version:apiVersion}/[controller]")]
    public class CacheController : BaseApiController
    {
        private readonly IRedisService _redisService;
        private readonly ILogger<CacheController> _logger;

        public CacheController(IRedisService redisService, ILogger<CacheController> logger)
        {
            _redisService = redisService;
            _logger = logger;
        }

        [HttpPost("load-traffic-lights")]
        public async Task<IActionResult> LoadTrafficLights()
        {
            try
            {
                _logger.LogInformation("Creating Redis.OM index if needed");
                var indexCreated = await _redisService.CreateIndexIfNotExistsAsync();
                if (!indexCreated)
                {
                    return ErrorResponse(500, 500, "Failed to create Redis.OM index");
                }

                _logger.LogInformation("Loading traffic lights into Redis");
                var result = await _redisService.LoadTrafficLightsToRedisAsync();
                if (result)
                {
                    return SuccessResponse(new { }, "Traffic lights successfully loaded to Redis");
                }
                else
                {
                    return ErrorResponse(500, 500, "Failed to load traffic lights");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading traffic lights to Redis");
                return InternalErrorResponse(ex.Message);
            }
        }

        [HttpGet("traffic-light/{id}")]
        public async Task<IActionResult> GetTrafficLight(int id)
        {
            try
            {
                var light = await _redisService.GetTrafficLightByIdAsync(id);
                if (light == null)
                {
                    return NotFoundResponse($"Traffic light with ID {id} not found");
                }

                return SuccessResponse(light);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving traffic light {id}");
                return InternalErrorResponse(ex.Message);
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchTrafficLights(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radius = 1.0) // Default radius in km
        {
            try
            {
                var lights = await _redisService.SearchTrafficLightsAsync(lat, lng, radius);
                return SearchResponse(lights, lights.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching traffic lights");
                return InternalErrorResponse(ex.Message);
            }
        }


        [HttpGet("get-traffic-lights")]
        public async Task<IActionResult> GetTrafficLights(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            try
            {
                _logger.LogInformation($"Retrieving traffic lights, page {page}, pageSize {pageSize}");

                var (lights, totalCount) = await _redisService.GetTrafficLightsAsync(page, pageSize);

                return SearchResponse(lights, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving traffic lights from Redis");
                return InternalErrorResponse(ex.Message);
            }
        }

    }
}