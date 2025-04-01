using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrafficDataCollection.AnalyzationResult.Service.Models;

namespace TrafficDataCollection.AnalyzationResult.Service.Service
{
    public interface ITeamsNotificationService
    {
        Task SendErrorNotificationAsync(CycleLight cycleLight, string lightName);
    }

    public class TeamsNotificationService : ITeamsNotificationService
    {
        private readonly ILogger<TeamsNotificationService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _webhookUrl;

        public TeamsNotificationService(
            ILogger<TeamsNotificationService> logger,
            IOptions<TeamsWebhookSettings> settings)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _webhookUrl = settings.Value.WebhookUrl;
        }

        public async Task SendErrorNotificationAsync(CycleLight cycleLight, string lightName)
        {
            try
            {
                if (string.IsNullOrEmpty(_webhookUrl))
                {
                    _logger.LogWarning("Teams webhook URL is not configured. Notification not sent.");
                    return;
                }

                // Create a Teams message card
                var card = new
                {
                    type = "MessageCard",
                    context = "http://schema.org/extensions",
                    themeColor = "FF0000", // Red for error
                    title = $"Traffic Light Error: {cycleLight.ReasonCode}",
                    sections = new[]
                    {
                        new
                        {
                            facts = new[]
                            {
                                new { name = "Light ID:", value = cycleLight.LightId.ToString() },
                                new { name = "Light Name:", value = lightName ?? "Unknown" },
                                new { name = "Error Code:", value = cycleLight.ReasonCode },
                                new { name = "Error Reason:", value = cycleLight.Reason },
                                new { name = "Timestamp:", value = DateTimeOffset.FromUnixTimeSeconds(cycleLight.CycleStartTimestamp).ToString("yyyy-MM-dd HH:mm:ss") }
                            }
                        }
                    }
                };

                // Serialize to JSON
                var json = JsonConvert.SerializeObject(card);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Send to Teams webhook
                var response = await _httpClient.PostAsync(_webhookUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to send Teams notification. Status code: {response.StatusCode}, Error: {errorContent}");
                }
                else
                {
                    _logger.LogInformation($"Teams notification sent successfully for Light ID {cycleLight.LightId} with error {cycleLight.ReasonCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while sending Teams notification for Light ID {cycleLight.LightId}");
            }
        }
    }
}
