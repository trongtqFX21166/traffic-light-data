using Newtonsoft.Json;
using Platform.KafkaClient;
using TrafficDataCollection.Api.Models;
using TrafficDataCollection.Api.Models.Entities;
using TrafficDataCollection.Api.Repository.Interfaces;

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
    }
}
