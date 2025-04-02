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

        [HttpPost("update-dag-run-status")]
        public async Task<IActionResult> UpdateDagRunStatus([FromBody] UpdateDagRunStatusRequest request)
        {
            if (string.IsNullOrEmpty(request.AirflowDagId) || string.IsNullOrEmpty(request.AirflowDagRunId))
            {
                return BadRequestResponse("airflow_dag_id and airflow_dag_run_id are required");
            }

            if (string.IsNullOrEmpty(request.Status))
            {
                return BadRequestResponse("status is required");
            }

            try
            {
                _logger.LogInformation($"Updating DAG run status for DAG {request.AirflowDagId}, run {request.AirflowDagRunId} to {request.Status}");
                var updated = await _schedulerService.UpdateDagRunStatusAsync(request.AirflowDagId, request.AirflowDagRunId, request.Status, request.Reason);

                if (updated)
                {
                    return SuccessResponse(new
                    {
                        updated = true,
                        status = request.Status
                    });
                }
                else
                {
                    return NotFoundResponse($"DAG run not found for {request.AirflowDagId} / {request.AirflowDagRunId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating DAG run status");
                return InternalErrorResponse(ex.Message);
            }
        }

        [HttpPost("summary-job-and-push-notify")]
        public async Task<IActionResult> SummaryJobAndPushNotify([FromBody] SummaryJobAndPushNotifyRequest request)
        {
            if (string.IsNullOrEmpty(request.AirflowDagId) || string.IsNullOrEmpty(request.AirflowDagRunId))
            {
                return BadRequestResponse("airflow_dag_id and airflow_dag_run_id are required");
            }

            try
            {
                _logger.LogInformation($"Generating job summary for DAG {request.AirflowDagId}, run {request.AirflowDagRunId}");

                var summary = await _schedulerService.GenerateJobSummaryAndPushNotifyAsync(
                    request.AirflowDagId,
                    request.AirflowDagRunId,
                    request.NotifyTo
                );

                return SuccessResponse(summary);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex.Message);
                return NotFoundResponse(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating job summary");
                return InternalErrorResponse(ex.Message);
            }
        }

    }
}