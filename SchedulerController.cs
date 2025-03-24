using Microsoft.AspNetCore.Mvc;
using TrafficDataCollection.Api.Models;
using TrafficDataCollection.Api.Services.Interfaces;

namespace TrafficLightMonitoring.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchedulerController : ControllerBase
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
                return BadRequest("airflow_dag_id and airflow_dag_run_id are required");
            }

            try
            {
                _logger.LogInformation($"Triggering light collection for DAG {request.AirflowDagId}, run {request.AirflowDagRunId}");
                var dagRunId = await _schedulerService.TriggerCollectLightsAsync(request.AirflowDagId, request.AirflowDagRunId);

                return Ok(new { dag_run_id = dagRunId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering light collection");
                return StatusCode(500, new { error = "Failed to trigger light collection", message = ex.Message });
            }
        }

        [HttpGet("check-collect-lights")]
        public async Task<IActionResult> CheckCollectLights([FromQuery] string airflow_dag_id, [FromQuery] string airflow_dag_run_id)
        {
            if (string.IsNullOrEmpty(airflow_dag_id) || string.IsNullOrEmpty(airflow_dag_run_id))
            {
                return BadRequest("airflow_dag_id and airflow_dag_run_id are required");
            }

            try
            {
                _logger.LogInformation($"Checking light collection status for DAG {airflow_dag_id}, run {airflow_dag_run_id}");
                var allCompleted = await _schedulerService.CheckCollectLightsStatusAsync(airflow_dag_id, airflow_dag_run_id);

                var response = new CheckCollectLightsResponse
                {
                    Code = allCompleted ? 0 : 1,
                    Message = allCompleted ? "success" : "processing"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking light collection status");
                return StatusCode(500, new { code = -1, msg = "error", error = ex.Message });
            }
        }
    }
}