using Elastic.Apm.Api;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Platform.KafkaClient;
using TrafficDataCollection.AnalyzationResult.Service.Models;
using TrafficDataCollection.AnalyzationResult.Service.Repo;
using TrafficDataCollection.AnalyzationResult.Service.Service;

namespace TrafficDataCollection.AnalyzationResult.Service
{
    public class AnalyzationResultService : BackgroundService
    {
        private readonly IConsumer _consumer;
        private readonly ICycleLightRepository _cycleLightRepository;
        private readonly ICycleLightHistoryRepository _cycleLightHistoryRepository;
        private readonly ITeamsNotificationService _teamsNotificationService;
        private readonly ILogger<AnalyzationResultService> _logger;

        // Error codes that should trigger a Teams notification
        private readonly string[] _errorReasonCodes = new[]
        {
            "ERR_TL_NOT_ACTIVE",
            "ERR_TIMESTAMP",
            "ERR_NO_TL",
            "ERR_OCR"
        };

        public AnalyzationResultService(
            IConsumer consumer,
            ICycleLightRepository cycleLightRepository,
            ICycleLightHistoryRepository cycleLightHistoryRepository,
            ITeamsNotificationService teamsNotificationService,
            ILogger<AnalyzationResultService> logger)
        {
            _consumer = consumer;
            _cycleLightRepository = cycleLightRepository;
            _cycleLightHistoryRepository = cycleLightHistoryRepository;
            _teamsNotificationService = teamsNotificationService;
            _logger = logger;

            _consumer.Consume += OnConsumeAnalyzationResult;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.WhenAll(
                Task.Factory.StartNew(() => _consumer.RegisterConsume(stoppingToken)));
        }

        private async void OnConsumeAnalyzationResult(Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, string> consumeResult)
        {
            if (string.IsNullOrWhiteSpace(consumeResult.Value))
            {
                return;
            }

            try
            {
                // Deserialize the message
                var analyzationResult = JsonConvert.DeserializeObject<VMLAnalyzationResultDto>(consumeResult.Value);

                if (analyzationResult == null)
                {
                    _logger.LogWarning("Failed to deserialize analyzation result message");
                    return;
                }

                _logger.LogInformation($"Processing analyzation result for Light ID {analyzationResult.Id}, Seq ID {analyzationResult.SeqId}");

                _logger.LogInformation($"Received analyzation result for Light ID {analyzationResult.Id}, Seq ID {analyzationResult.SeqId}, Status: {analyzationResult.Status}");

                // Process the result
                await ProcessAnalyzationResultAsync(analyzationResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing analyzation result message");
            }
        }

        private async Task ProcessAnalyzationResultAsync(VMLAnalyzationResultDto analyzationResult)
        {
            try
            {
                // Generate a unique ID for the cycle light
                var cycleLightId = Guid.NewGuid().ToString();
                var cycleLightHistoryId = Guid.NewGuid().ToString();

                // Get existing cycle light record if it exists
                var existingCycleLight = await _cycleLightRepository.GetByLightIdAsync(int.Parse(analyzationResult.Id));

                // Create a new cycle light history record
                var cycleLightHistory = new CycleLightHistory
                {
                    Id = cycleLightHistoryId,
                    LightId = int.Parse(analyzationResult.Id),
                   
                    
                    CreatedAt = DateTime.UtcNow,
                    FramesInSecond = existingCycleLight.FramesInSecond,
                    SecondsExtractFrame = 180, // Default value (3 minutes)
                    Source = analyzationResult.CameraSource ?? "CCTV",
                    Status = analyzationResult.Status ?? "Active",
                    ReasonCode = analyzationResult.ReasonCode,
                    Reason = analyzationResult.Reason
                };

                if (analyzationResult.Data != null) {
                    cycleLightHistory.RedTime = analyzationResult.Data.RedTime;
                    cycleLightHistory.GreenTime = analyzationResult.Data.GreenTime;
                    cycleLightHistory.YellowTime = analyzationResult.Data.YellowTime;
                    cycleLightHistory.TimeTick = analyzationResult.Data.TimeTick;
                    cycleLightHistory.CycleStartTimestamp = analyzationResult.Data.CycleStartUnixTimestamp;
                }

                // Save the history record
                await _cycleLightHistoryRepository.CreateAsync(cycleLightHistory);
                _logger.LogInformation($"Created cycle light history record {cycleLightHistoryId} for Light ID {analyzationResult.Id}");

                // Update or create the cycle light record
                if (existingCycleLight != null)
                {
                    await _cycleLightRepository.UpdateAsync(existingCycleLight);
                    _logger.LogInformation($"Updated cycle light record {existingCycleLight.Id} for Light ID {analyzationResult.Id}");

                    // Check if notification is needed
                    if (!string.IsNullOrEmpty(existingCycleLight.ReasonCode) &&
                        _errorReasonCodes.Contains(existingCycleLight.ReasonCode))
                    {
                        await _teamsNotificationService.SendErrorNotificationAsync(
                            existingCycleLight,
                            analyzationResult.CameraName);
                    }
                    else 
                    {
                        // Update existing record
                        existingCycleLight.CycleLightHistoryId = cycleLightHistoryId;
                       
                        
                        existingCycleLight.LastModified = DateTime.UtcNow;                       
                        existingCycleLight.FramesInSecond = existingCycleLight.FramesInSecond;
                        existingCycleLight.Source = analyzationResult.CameraSource ?? existingCycleLight.Source;
                        existingCycleLight.Status = analyzationResult.Status ?? existingCycleLight.Status;
                        existingCycleLight.ReasonCode = analyzationResult.ReasonCode;
                        existingCycleLight.Reason = analyzationResult.Reason;

                        if (analyzationResult.Data != null) {
                            existingCycleLight.RedTime = analyzationResult.Data.RedTime;
                            existingCycleLight.GreenTime = analyzationResult.Data.GreenTime;
                            existingCycleLight.YellowTime = analyzationResult.Data.YellowTime;
                            existingCycleLight.TimeTick = analyzationResult.Data.TimeTick;
                            existingCycleLight.CycleStartTimestamp = analyzationResult.Data.CycleStartUnixTimestamp;
                        }

                    }
                }
                else
                {
                    // Create new record
                    var newCycleLight = new CycleLight
                    {
                        Id = cycleLightId,
                        CycleLightHistoryId = cycleLightHistoryId,
                        LightId = int.Parse(analyzationResult.Id),
                        RedTime = analyzationResult.Data.RedTime,
                        GreenTime = analyzationResult.Data.GreenTime,
                        YellowTime = analyzationResult.Data.YellowTime,
                        CycleStartTimestamp = analyzationResult.Data.CycleStartUnixTimestamp,
                        LastModified = DateTime.UtcNow,
                        TimeTick = analyzationResult.Data.TimeTick,
                        FramesInSecond = existingCycleLight.FramesInSecond,
                        SecondsExtractFrame = 180, // Default value (3 minutes)
                        Source = analyzationResult.CameraSource ?? "CCTV",
                        Status = analyzationResult.Status ?? "Active",
                        ReasonCode = analyzationResult.ReasonCode,
                        Reason = analyzationResult.Reason
                    };

                    await _cycleLightRepository.CreateAsync(newCycleLight);
                    _logger.LogInformation($"Created new cycle light record {cycleLightId} for Light ID {analyzationResult.Id}");

                    // Check if notification is needed
                    if (!string.IsNullOrEmpty(newCycleLight.ReasonCode) &&
                        _errorReasonCodes.Contains(newCycleLight.ReasonCode))
                    {
                        await _teamsNotificationService.SendErrorNotificationAsync(
                            newCycleLight,
                            analyzationResult.CameraName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing analyzation result for Light ID {analyzationResult.Id}");
            }
        }
    }
}