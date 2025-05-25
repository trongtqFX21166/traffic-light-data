using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using TrafficDataCollection.Api.Helper;
using TrafficDataCollection.Api.Models;
using TrafficDataCollection.Api.Models.Entities;
using TrafficDataCollection.Api.Services;

namespace TrafficDataCollection.Api.Controllers
{
    [ApiController]
    [ApiVersion(1.0)]
    [Route("v{version:apiVersion}/[controller]")]
    public class CycleLightHistoryController : BaseApiController
    {
        private readonly ICycleLightHistoryService _cycleLightHistoryService;
        private readonly ILogger<CycleLightHistoryController> _logger;

        public CycleLightHistoryController(
            ICycleLightHistoryService cycleLightHistoryService,
            ILogger<CycleLightHistoryController> logger)
        {
            _cycleLightHistoryService = cycleLightHistoryService;
            _logger = logger;
        }

        /// <summary>
        /// Get cycle light history records for a specific traffic light
        /// </summary>
        /// <param name="lightId">The ID of the traffic light</param>
        /// <param name="request">Query parameters for filtering</param>
        /// <returns>List of cycle light history records</returns>
        [HttpGet("{lightId}")]
        public async Task<IActionResult> GetCycleLightHistory(
            [FromRoute] int lightId,
            [FromQuery] CycleLightHistoryRequest request)
        {
            try
            {
                _logger.LogInformation($"Getting cycle light history for light ID {lightId}");

                // Validate input
                if (lightId <= 0)
                {
                    return BadRequestResponse("Invalid light ID. Must be a positive integer.");
                }

                // Apply default limit if not provided
                if (!request.Limit.HasValue || request.Limit <= 0)
                {
                    request.Limit = 100;
                }

                // Get history records
                var historyRecords = await _cycleLightHistoryService.GetCycleLightHistoryAsync(
                    lightId,
                    request.FromTime,
                    request.ToTime,
                    request.Limit);

                // Map to DTOs
                var dtos = historyRecords.Select(MapToDto).ToList();

                // Return response
                return SuccessResponse(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting cycle light history for light ID {lightId}");
                return InternalErrorResponse(ex.Message);
            }
        }

        private CycleLightHistoryDto MapToDto(CycleLightHistory history)
        {
            // Convert Unix timestamp (milliseconds) to DateTime
            var cycleStartDateTime = DateTimeHelper.FromUnixTimeMilliseconds(history.CycleStartTimestamp);

            return new CycleLightHistoryDto
            {
                Id = history.Id,
                LightId = history.LightId,
                RedTime = history.RedTime,
                GreenTime = history.GreenTime,
                YellowTime = history.YellowTime,
                CycleStartTimestamp = history.CycleStartTimestamp,
                CycleStartTime = cycleStartDateTime.ToStandardString(),
                CreatedAt = history.CreatedAt.ToStandardString(),
                TimeTick = history.TimeTick,
                FramesInSecond = history.FramesInSecond,
                SecondsExtractFrame = history.SecondsExtractFrame,
                Source = history.Source,
                Status = history.Status,
                ReasonCode = history.ReasonCode,
                Reason = history.Reason
            };
        }
    }
}