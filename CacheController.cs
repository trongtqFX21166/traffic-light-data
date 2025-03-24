﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TrafficDataCollection.Api.Services.Interfaces;

namespace TrafficDataCollection.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CacheController : ControllerBase
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
                    return StatusCode(500, new { error = "Failed to create Redis.OM index" });
                }

                _logger.LogInformation("Loading traffic lights into Redis");
                var result = await _redisService.LoadTrafficLightsToRedisAsync();
                if (result)
                {
                    return Ok(new { message = "Traffic lights successfully loaded to Redis" });
                }
                else
                {
                    return StatusCode(500, new { error = "Failed to load traffic lights" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading traffic lights to Redis");
                return StatusCode(500, new { error = "An unexpected error occurred", message = ex.Message });
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
                    return NotFound(new { error = $"Traffic light with ID {id} not found" });
                }

                return Ok(light);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving traffic light {id}");
                return StatusCode(500, new { error = "An unexpected error occurred", message = ex.Message });
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
                return Ok(lights);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching traffic lights");
                return StatusCode(500, new { error = "An unexpected error occurred", message = ex.Message });
            }
        }
    }
}
