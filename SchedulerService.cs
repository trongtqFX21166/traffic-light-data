using Newtonsoft.Json;
using Platform.KafkaClient;
using TrafficDataCollection.Api.Models;
using TrafficDataCollection.Api.Models.Entities;
using TrafficDataCollection.Api.Repository.Interfaces;
using TrafficDataCollection.Api.Services.Interfaces;

namespace TrafficDataCollection.Api.Services
{
    public class SchedulerService : ISchedulerService
    {
        private readonly ILightService _lightService;
        private readonly IAirflowDagRunRepository _dagRunRepository;
        private readonly ITrafficLightCommandRepository _commandRepository;
        private readonly IProducer _kafkaProducer;
        private readonly ILogger<SchedulerService> _logger;

        public SchedulerService(
            ILightService lightService,
            IAirflowDagRunRepository dagRunRepository,
            ITrafficLightCommandRepository commandRepository,
            IProducer kafkaProducer,
            ILogger<SchedulerService> logger)
        {
            _lightService = lightService;
            _dagRunRepository = dagRunRepository;
            _commandRepository = commandRepository;
            _kafkaProducer = kafkaProducer;
            _logger = logger;
        }

        public async Task<string> TriggerCollectLightsAsync(string airflowDagId, string airflowDagRunId)
        {
            // Check if this DAG run already exists
            var existingRun = await _dagRunRepository.GetByAirflowIdsAsync(airflowDagId, airflowDagRunId);
            if (existingRun != null)
            {
                return existingRun.Id;
            }

            // Get all main lights (those without main_light_id)
            var lights = await _lightService.GetAllMainLightsAsync();

            // Create new DAG run
            var dagRun = new AirflowDagRun
            {
                Id = Guid.NewGuid().ToString(),
                AirflowDagId = airflowDagId,
                AirflowDagRunId = airflowDagRunId,
                AirflowTaskId = "Start_Collect_TrafficLight",
                ExecutionDate = DateTime.UtcNow,
                StartTime = DateTime.UtcNow,
                Status = "Running",
                TotalCommands = 0,
                CompletedCommands = 0,
                TotalTrafficLights = 0,
                ProcessedTrafficLights = 0,
                TimeoutTime = DateTime.UtcNow.AddHours(1) // 1 hour timeout
            };

            // Create commands for each light
            var commands = new List<TrafficLightCommand>();
            foreach (var light in lights.Where(x => x.Bboxes != null))
            {
                var sequenceId = Guid.NewGuid().ToString();

                var command = new TrafficLightCommand
                {
                    Id = sequenceId,
                    DagRunId = dagRun.Id,
                    TaskType = "Start_Collect_TrafficLight",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RetryCount = 0,
                    TrafficLightId = light.Id,
                    CameraId = light.CameraId,
                    CommandPayload = JsonConvert.SerializeObject(new
                    {
                        LightId = light.Id,
                        CameraId = light.CameraId,
                        CameraUrl = light.CameraLiveUrl
                    })
                };

                commands.Add(command);

                // Prepare and send Kafka message
                var message = new CameraCollectionMessage
                {
                    SeqId = sequenceId,
                    Id = light.Id,
                    Type = "Light",
                    CameraSource = "CCTV", // Default source
                    CameraId = light.CameraId, 
                    CameraLiveUrl = light.CameraLiveUrl, 
                    CameraName = light.Name,
                    FramesInSecond = 1,     // Default value
                    DurationExtractFrame = 180, // Default value - 3 minutes
                    TimestampBBox = light.TimestampBBox,
                    Bboxes = light.Bboxes,
                };

                var kafkaMessage = JsonConvert.SerializeObject(message);
                await _kafkaProducer.ProduceMessageAsync("VML_Camera_Collection", kafkaMessage);
            }

            // Update DAG run with command counts
            dagRun.TotalCommands = commands.Count;
            dagRun.TotalTrafficLights = commands.Count;

            // Save DAG run and commands to database
            await _dagRunRepository.CreateAsync(dagRun);
            await _commandRepository.CreateManyAsync(commands);

            return dagRun.Id;
        }

        public async Task<bool> CheckCollectLightsStatusAsync(string airflowDagId, string airflowDagRunId)
        {
            // Get the DAG run
            var dagRun = await _dagRunRepository.GetByAirflowIdsAsync(airflowDagId, airflowDagRunId);
            if (dagRun == null)
            {
                _logger.LogWarning($"DAG run not found for {airflowDagId} / {airflowDagRunId}");
                return false;
            }

            // Check if all commands are completed
            var allCompleted = await _commandRepository.AreAllCommandsCompletedAsync(dagRun.Id);

            // If all completed, update the DAG run status
            if (allCompleted && dagRun.Status != "Success")
            {
                dagRun.Status = "Success";
                dagRun.EndTime = DateTime.UtcNow;
                await _dagRunRepository.UpdateAsync(dagRun);
            }

            return allCompleted;
        }

        public async Task<(int, bool)> SetTimeoutForPendingCommandsAsync(string airflowDagId, string airflowDagRunId)
        {
            // Get the DAG run
            var dagRun = await _dagRunRepository.GetByAirflowIdsAsync(airflowDagId, airflowDagRunId);
            if (dagRun == null)
            {
                _logger.LogWarning($"DAG run not found for {airflowDagId} / {airflowDagRunId}");
                return (0, false);
            }

            // Get all pending or running commands for this DAG run
            var pendingOrRunningCommands = await _commandRepository.GetPendingOrRunningCommandsByDagRunIdAsync(dagRun.Id);
            var commandsList = pendingOrRunningCommands.ToList();

            if (!commandsList.Any())
            {
                _logger.LogInformation($"No pending or running commands found for DAG run {dagRun.Id}");
                return (0, false);
            }

            // Update all pending or running commands to "Timeout" status
            foreach (var command in commandsList)
            {
                command.Status = "Timeout";
                command.Reason = "Command timed out manually";
                command.UpdatedAt = DateTime.UtcNow;
            }

            // Bulk update all commands
            await _commandRepository.UpdateManyAsync(commandsList);

            // Update DAG run status if needed
            bool dagStatusUpdated = false;
            if (dagRun.Status == "Running")
            {
                dagRun.Status = "Timeout";
                dagRun.EndTime = DateTime.UtcNow;
                await _dagRunRepository.UpdateAsync(dagRun);
                dagStatusUpdated = true;
            }

            _logger.LogInformation($"Set {commandsList.Count} commands to timeout status for DAG run {dagRun.Id}");
            return (commandsList.Count, dagStatusUpdated);
        }

        public async Task<bool> UpdateDagRunStatusAsync(string airflowDagId, string airflowDagRunId, string status, string reason = null)
        {
            // Get the DAG run
            var dagRun = await _dagRunRepository.GetByAirflowIdsAsync(airflowDagId, airflowDagRunId);
            if (dagRun == null)
            {
                _logger.LogWarning($"DAG run not found for {airflowDagId} / {airflowDagRunId}");
                return false;
            }

            // Validate the status value
            string[] validStatuses = { "Running", "Success", "Failed", "Timeout", "Canceled" };
            if (!validStatuses.Contains(status))
            {
                _logger.LogWarning($"Invalid status '{status}' for DAG run {dagRun.Id}. Valid values are: {string.Join(", ", validStatuses)}");
                throw new ArgumentException($"Invalid status value. Valid values are: {string.Join(", ", validStatuses)}");
            }

            // Check if the status is actually changing
            if (dagRun.Status == status)
            {
                _logger.LogInformation($"DAG run {dagRun.Id} already has status '{status}', no update needed");
                return true;
            }

            // Update the DAG run status
            _logger.LogInformation($"Updating DAG run {dagRun.Id} status from '{dagRun.Status}' to '{status}'");
            dagRun.Status = status;

            // If status indicates completion, set the end time
            if (status == "Success" || status == "Failed" || status == "Timeout" || status == "Canceled")
            {
                dagRun.EndTime = DateTime.UtcNow;
            }

            // If a reason was provided, store it in the execution parameters (as a JSON object)
            if (!string.IsNullOrEmpty(reason))
            {
                // Parse existing execution parameters if they exist
                var executionParams = !string.IsNullOrEmpty(dagRun.ExecutionParameters)
                    ? JsonConvert.DeserializeObject<Dictionary<string, object>>(dagRun.ExecutionParameters)
                    : new Dictionary<string, object>();

                // Add or update the status reason
                executionParams["status_reason"] = reason;
                executionParams["status_updated_at"] = DateTime.UtcNow.ToString("o");

                // Update the execution parameters
                dagRun.ExecutionParameters = JsonConvert.SerializeObject(executionParams);
            }

            // Save the updated DAG run
            await _dagRunRepository.UpdateAsync(dagRun);

            _logger.LogInformation($"Successfully updated DAG run {dagRun.Id} status to '{status}'");
            return true;
        }
    }
}
