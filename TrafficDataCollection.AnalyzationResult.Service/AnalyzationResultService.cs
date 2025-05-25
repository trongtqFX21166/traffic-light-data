using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Platform.KafkaClient;
using System.Net.Http;
using TrafficDataCollection.AnalyzationResult.Service.Models;
using TrafficDataCollection.AnalyzationResult.Service.Repo;

namespace TrafficDataCollection.AnalyzationResult.Service
{
    public class AnalyzationResultService : BackgroundService
    {
        private readonly IConsumer _consumer;
        private readonly ICycleLightRepository _cycleLightRepository;
        private readonly ICycleLightHistoryRepository _cycleLightHistoryRepository;
        private readonly ILogger<AnalyzationResultService> _logger;

        // Error codes that should trigger a Teams notification, updated with all error codes from the PDF
        private readonly string[] _errorReasonCodes = new[]
        {
            "ERR_NO_FRAMES",    // No Frames
            "ERR_NO_TL",        // Traffic Light Not Detected
            "ERR_TL_NOT_ACTIVE", // Traffic Light Not Active
            "ERR_INCOMPLETE_CYCLE", // Incomplete Cycle
            "ERR_OCR",          // OCR Extraction Error
            "ERR_TIMESTAMP",    // Timestamp Format Error
            "ERR_SEQUENCE",     // Frame Sequence Error
            "ERR_UNKNOWN"       // Unknown Error
        };

        // Dictionary mapping error codes to detailed descriptions for better notifications
        private readonly Dictionary<string, string> _errorDescriptions = new Dictionary<string, string>
        {
            { "ERR_NO_FRAMES", "Dữ liệu đầu vào không chứa bất kỳ frame nào, nên không thể thực hiện phân tích." },
            { "ERR_NO_TL", "Trong toàn bộ các frame không tìm thấy bất kỳ thông tin nào liên quan đến đèn giao thông (có thể do góc quay, chất lượng ảnh hoặc lỗi trích xuất)." },
            { "ERR_TL_NOT_ACTIVE", "Đèn giao thông được phát hiện nhưng không hoạt động (Không sáng đèn)." },
            { "ERR_INCOMPLETE_CYCLE", "Phát hiện chu kỳ đèn (xanh – vàng – đỏ) không đầy đủ, có thể do các frame đầu đang ở trạng thái giữa chừng nên không đủ dữ liệu để tính toàn bộ chu kỳ." },
            { "ERR_OCR", "Quá trình nhận diện ký tự quang học (OCR) thất bại khi trích xuất thông tin timestamp hoặc các thông tin liên quan từ frame." },
            { "ERR_TIMESTAMP", "Thông tin timestamp trích xuất không hợp lệ hoặc không thể chuyển đổi sang UnixTimestamp (có thể do định dạng sai hoặc lỗi trong OCR)." },
            { "ERR_SEQUENCE", "Các frame không theo thứ tự thời gian hợp lý hoặc thứ tự thay đổi màu đèn không tuân thủ chu trình hợp lệ (Xanh → Vàng → Đỏ → Xanh)." },
            { "ERR_UNKNOWN", "Một lỗi không xác định đã xảy ra trong quá trình phân tích, cần xem lại log hoặc kiểm tra lại dữ liệu đầu vào để xác định nguyên nhân cụ thể." }
        };

        // Valid status codes for updating cycle light records, loaded from config
        private readonly string[] _validCycleLightStatusCodes;

        private readonly HttpClient _trafficLightClient;

        public AnalyzationResultService(
            IConsumer consumer,
            ICycleLightRepository cycleLightRepository,
            ICycleLightHistoryRepository cycleLightHistoryRepository,
            IConfiguration configuration,
            IHttpClientFactory clientFac,
            ILogger<AnalyzationResultService> logger)
        {
            _consumer = consumer;
            _cycleLightRepository = cycleLightRepository;
            _cycleLightHistoryRepository = cycleLightHistoryRepository;

            _trafficLightClient = clientFac.CreateClient("TrafficLightClient");

            _logger = logger;

            _consumer.Consume += OnConsumeAnalyzationResult;

            // Get valid cycle light status codes from configuration or use defaults
            _validCycleLightStatusCodes = configuration.GetSection("ValidCycleLightStatusCodes").Get<string[]>();
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
                var cycleLightHistoryId = analyzationResult.SeqId;

                // Get existing cycle light record if it exists
                var existingCycleLight = await _cycleLightRepository.GetByLightIdAsync(int.Parse(analyzationResult.Id));

                // Create a new cycle light history record
                var cycleLightHistory = new CycleLightHistory
                {
                    Id = cycleLightHistoryId,
                    LightId = int.Parse(analyzationResult.Id),
                    CreatedAt = DateTime.UtcNow,
                    FramesInSecond = existingCycleLight?.FramesInSecond ?? 1, // Default to 1 if no existing record
                    SecondsExtractFrame = 180, // Default value (3 minutes)
                    Source = analyzationResult.CameraSource ?? "CCTV",
                    Status = analyzationResult.Status ?? "Active",
                    ReasonCode = analyzationResult.ReasonCode,
                    Reason = analyzationResult.Reason
                };

                if (analyzationResult.Data != null)
                {
                    cycleLightHistory.RedTime = analyzationResult.Data.RedTime;
                    cycleLightHistory.GreenTime = analyzationResult.Data.GreenTime;
                    cycleLightHistory.YellowTime = analyzationResult.Data.YellowTime;
                    cycleLightHistory.TimeTick = analyzationResult.Data.TimeTick;
                    cycleLightHistory.CycleStartTimestamp = analyzationResult.Data.CycleStartUnixTimestamp;
                }

                // Save the history record
                await _cycleLightHistoryRepository.CreateAsync(cycleLightHistory);
                _logger.LogInformation($"Created cycle light history record {cycleLightHistoryId} for Light ID {analyzationResult.Id}");

                // Update traffic light command status
                await UpdateTrafficLightCommandStatus(analyzationResult);

                // Update or create the cycle light record
                if (existingCycleLight != null
                    && _validCycleLightStatusCodes.Contains(analyzationResult.ReasonCode))
                {
                    // Update existing record
                    existingCycleLight.CycleLightHistoryId = cycleLightHistoryId;
                    existingCycleLight.LastModified = DateTime.UtcNow;
                    existingCycleLight.Source = analyzationResult.CameraSource ?? existingCycleLight.Source;
                    existingCycleLight.Status = analyzationResult.Status ?? existingCycleLight.Status;
                    existingCycleLight.ReasonCode = analyzationResult.ReasonCode;
                    existingCycleLight.Reason = analyzationResult.Reason;

                    if (analyzationResult.Data != null)
                    {
                        existingCycleLight.RedTime = analyzationResult.Data.RedTime;
                        existingCycleLight.GreenTime = analyzationResult.Data.GreenTime;
                        existingCycleLight.YellowTime = analyzationResult.Data.YellowTime;
                        existingCycleLight.TimeTick = analyzationResult.Data.TimeTick;
                        existingCycleLight.CycleStartTimestamp = analyzationResult.Data.CycleStartUnixTimestamp;
                    }

                    await _cycleLightRepository.UpdateAsync(existingCycleLight);
                    _logger.LogInformation($"Updated cycle light record {existingCycleLight.Id} for Light ID {analyzationResult.Id}");

                    // Check if notification is needed - uncomment if needed
                    //if (!string.IsNullOrEmpty(existingCycleLight.ReasonCode) &&
                    //    _errorReasonCodes.Contains(existingCycleLight.ReasonCode))
                    //{
                    //    await _teamsNotificationService.SendErrorNotificationAsync(
                    //        existingCycleLight,
                    //        analyzationResult.CameraName,
                    //        GetDetailedErrorDescription(existingCycleLight.ReasonCode));
                    //}
                }
                else
                {
                    // Create new record
                    var newCycleLight = new CycleLight
                    {
                        Id = cycleLightId,
                        CycleLightHistoryId = cycleLightHistoryId,
                        LightId = int.Parse(analyzationResult.Id),
                        LastModified = DateTime.UtcNow,
                        FramesInSecond = 1, // Default value
                        SecondsExtractFrame = 180, // Default value (3 minutes)
                        Source = analyzationResult.CameraSource ?? "CCTV",
                        Status = analyzationResult.Status ?? "Active",
                        ReasonCode = analyzationResult.ReasonCode,
                        Reason = analyzationResult.Reason
                    };

                    if (analyzationResult.Data != null)
                    {
                        newCycleLight.RedTime = analyzationResult.Data.RedTime;
                        newCycleLight.GreenTime = analyzationResult.Data.GreenTime;
                        newCycleLight.YellowTime = analyzationResult.Data.YellowTime;
                        newCycleLight.TimeTick = analyzationResult.Data.TimeTick;
                        newCycleLight.CycleStartTimestamp = analyzationResult.Data.CycleStartUnixTimestamp;
                    }

                    await _cycleLightRepository.CreateAsync(newCycleLight);
                    _logger.LogInformation($"Created new cycle light record {cycleLightId} for Light ID {analyzationResult.Id}");

                    // Check if notification is needed - uncomment if needed
                    //if (!string.IsNullOrEmpty(newCycleLight.ReasonCode) &&
                    //    _errorReasonCodes.Contains(newCycleLight.ReasonCode))
                    //{
                    //    await _teamsNotificationService.SendErrorNotificationAsync(
                    //        newCycleLight,
                    //        analyzationResult.CameraName,
                    //        GetDetailedErrorDescription(newCycleLight.ReasonCode));
                    //}
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing analyzation result for Light ID {analyzationResult.Id}");
            }
        }

        private async Task UpdateTrafficLightCommandStatus(VMLAnalyzationResultDto result)
        {
            try
            {
                if (string.IsNullOrEmpty(result.SeqId))
                {
                    _logger.LogWarning($"Cannot update traffic light command: SeqId is null or empty for Light ID {result.Id}");
                    return;
                }

                // Convert status from analyzation result to command status
                string commandStatus;
                string reasonText = result.Reason ?? "";

                // Determine command status based on analyzation result status and reason code
                if (result.ReasonCode == "SUCCESS" || string.IsNullOrEmpty(result.ReasonCode))
                {
                    commandStatus = "Completed";
                }
                else if (_errorReasonCodes.Contains(result.ReasonCode))
                {
                    // Handle specific error codes
                    switch (result.ReasonCode)
                    {
                        case "ERR_NO_FRAMES":
                        case "ERR_OCR":
                        case "ERR_TIMESTAMP":
                            // These might be retriable errors
                            commandStatus = "Failed";
                            break;
                        default:
                            // Other errors are considered non-retriable
                            commandStatus = "Failed";
                            break;
                    }
                }
                else
                {
                    // Default case for unknown status
                    commandStatus = "Failed";
                }

                // Call the API directly from the service
                _logger.LogInformation($"Updating command {result.SeqId} with status {commandStatus}");

                var requestBody = new
                {
                    command_id = result.SeqId,
                    status = commandStatus,
                    reason_code = result.ReasonCode,
                    reason = reasonText
                };

                var url = $"v1/command/update-status";
                var response = await _trafficLightClient.PostAsync(url, new StringContent(JsonConvert.SerializeObject(requestBody), System.Text.Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to update command status. Status code: {response.StatusCode}, Error: {errorContent}");
                }
                else
                {
                    _logger.LogInformation($"Successfully updated command {result.SeqId} to status {commandStatus}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating traffic light command for SeqId {result.SeqId}");
            }
        }


        // Helper method to get detailed error description
        private string GetDetailedErrorDescription(string reasonCode)
        {
            if (_errorDescriptions.TryGetValue(reasonCode, out string description))
            {
                return description;
            }

            return "Không có mô tả chi tiết cho mã lỗi này.";
        }
    }
}