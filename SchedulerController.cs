using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using TrafficDataCollection.Api.Controllers;
using TrafficDataCollection.Api.Models;
using TrafficDataCollection.Api.Services.Interfaces;

namespace TrafficLightMonitoring.Controllers
{
    [ApiController]
    [ApiVersion(1.0)]
    [Route("v{version:apiVersion}/[controller]")]
    public class SchedulerController : BaseApiController
    {
        private readonly ISchedulerService _schedulerService;
        private readonly ILogger<SchedulerController> _logger;

        public SchedulerController(ISchedulerService schedulerService, ILogger<SchedulerController> logger)
        {
            _schedulerService = schedulerService;
            _logger = logger;
        }

        [HttpPost("trigger-collect-lights")]
        public async Task<IActionResult> TriggerCollectLights([FromBody] TriggerCollectLightsRequest request)
        {
            if (string.IsNullOrEmpty(request.AirflowDagId) || string.IsNullOrEmpty(request.AirflowDagRunId))
            {
                return BadRequestResponse("airflow_dag_id and airflow_dag_run_id are required");
            }

            try
            {
                _logger.LogInformation($"Triggering light collection for DAG {request.AirflowDagId}, run {request.AirflowDagRunId}");
                var dagRunId = await _schedulerService.TriggerCollectLightsAsync(request.AirflowDagId, request.AirflowDagRunId);

                return SuccessResponse(new { dag_run_id = dagRunId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering light collection");
                return InternalErrorResponse(ex.Message);
            }
        }

        [HttpGet("check-collect-lights")]
        public async Task<IActionResult> CheckCollectLights([FromQuery] string airflow_dag_id, [FromQuery] string airflow_dag_run_id)
        {
            if (string.IsNullOrEmpty(airflow_dag_id) || string.IsNullOrEmpty(airflow_dag_run_id))
            {
                return BadRequestResponse("airflow_dag_id and airflow_dag_run_id are required");
            }

            try
            {
                _logger.LogInformation($"Checking light collection status for DAG {airflow_dag_id}, run {airflow_dag_run_id}");
                var allCompleted = await _schedulerService.CheckCollectLightsStatusAsync(airflow_dag_id, airflow_dag_run_id);

                return SuccessResponse(new
                {
                    is_completed = allCompleted,
                    status = allCompleted ? "success" : "processing"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking light collection status");
                return InternalErrorResponse(ex.Message);
            }
        }

        [HttpPost("set-time-out-collect-lights")]
        public async Task<IActionResult> SetTimeOutCollectLights([FromBody] TriggerCollectLightsRequest request)
        {
            if (string.IsNullOrEmpty(request.AirflowDagId) || string.IsNullOrEmpty(request.AirflowDagRunId))
            {
                return BadRequestResponse("airflow_dag_id and airflow_dag_run_id are required");
            }

            try
            {
                _logger.LogInformation($"Setting timeout for light collection DAG {request.AirflowDagId}, run {request.AirflowDagRunId}");
                var result = await _schedulerService.SetTimeoutForPendingCommandsAsync(request.AirflowDagId, request.AirflowDagRunId);

                return SuccessResponse(new
                {
                    timedout_commands_count = result.Item1,
                    dag_status_updated = result.Item2
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting timeout for light collection commands");
                return InternalErrorResponse(ex.Message);
            }
        }
    }
}