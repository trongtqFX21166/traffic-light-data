using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using TrafficDataCollection.Api.Models;
using TrafficDataCollection.Api.Repository.Interfaces;

namespace TrafficDataCollection.Api.Controllers
{
    [ApiController]
    [ApiVersion(1.0)]
    [Route("v{version:apiVersion}/[controller]")]
    public class CommandController : BaseApiController
    {
        private readonly ITrafficLightCommandRepository _commandRepository;
        private readonly ILogger<CommandController> _logger;

        public CommandController(
            ITrafficLightCommandRepository commandRepository,
            ILogger<CommandController> logger)
        {
            _commandRepository = commandRepository;
            _logger = logger;
        }

        [HttpPost("update-status")]
        public async Task<IActionResult> UpdateCommandStatus([FromBody] UpdateCommandStatusRequest request)
        {
            if (string.IsNullOrEmpty(request.CommandId))
            {
                return BadRequestResponse("command_id is required");
            }

            if (string.IsNullOrEmpty(request.Status))
            {
                return BadRequestResponse("status is required");
            }

            try
            {
                _logger.LogInformation($"Updating command status for command ID {request.CommandId} to {request.Status}");

                var command = await _commandRepository.GetByIdAsync(request.CommandId);
                if (command == null)
                {
                    return NotFoundResponse($"Command with ID {request.CommandId} not found");
                }

                // Update command status
                command.Status = request.Status;
                command.UpdatedAt = DateTime.UtcNow;
                command.ReasonCode = request.ReasonCode;
                command.Reason = request.Reason;

                await _commandRepository.UpdateAsync(command);

                return SuccessResponse(new { updated = true, status = request.Status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating command status");
                return InternalErrorResponse(ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCommand(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequestResponse("Command ID is required");
            }

            try
            {
                var command = await _commandRepository.GetByIdAsync(id);
                if (command == null)
                {
                    return NotFoundResponse($"Command with ID {id} not found");
                }

                return SuccessResponse(command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving command {id}");
                return InternalErrorResponse(ex.Message);
            }
        }
    }
}