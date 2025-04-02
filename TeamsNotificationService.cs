using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Reflection.Metadata;
using System.Text;
using TrafficDataCollection.Api.Models;
using TrafficDataCollection.Api.Services.Interfaces;

namespace TrafficDataCollection.Api.Services
{
    public interface INotificationService
    {
        Task SendJobSummaryNotificationAsync(JobSummary summary, string[] recipients);
    }

    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        private readonly String[] ManualErrorShouldCheckByAdmin = new string[]{ 
            "ERR_NO_TL", 
            "ERR_TL_NOT_ACTIVE",
            "ERR_SEQUENCE"
        }; 

        public NotificationService(
            ILogger<NotificationService> logger,
            IHttpClientFactory httpClientFac,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClientFac.CreateClient("TeamNotifyClient");
            _configuration = configuration;
        }

        public async Task SendJobSummaryNotificationAsync(JobSummary summary, string[] recipients)
        {
            try
            {


                string teamsWebhookUrl = _configuration.GetValue<string>("TeamsWebhookSettings:WebhookUrl");

                if (!string.IsNullOrEmpty(teamsWebhookUrl))
                {
                    await SendTeamsNotificationAsync(summary, teamsWebhookUrl);
                }

                // Send email notifications if email addresses are provided
                if (recipients?.Length > 0)
                {
                    var emailRecipients = recipients.Where(r => IsValidEmail(r)).ToArray();
                    if (emailRecipients.Length > 0)
                    {
                        await SendEmailNotificationAsync(summary, emailRecipients);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send job summary notification");
            }
        }

        private async Task SendTeamsNotificationAsync(JobSummary summary, string webhookUrl)
        {
            try
            {
                // Choose color based on status
                string themeColor = summary.Status switch
                {
                    "Success" => "00FF00", // Green
                    "Failed" => "FF0000",  // Red
                    "Timeout" => "FFA500", // Orange
                    _ => "0078D7"          // Default blue
                };

                // Calculate success rate for display
                string successRateDisplay = $"{summary.SuccessRate:F1}%";

                // Create mentions section with users to notify
                var mentions = new List<object>();
                var mentionTexts = new List<string>();


                // Create initial sections with job details
                var sections = new List<object>
                {
                    new
                    {
                        facts = new[]
                        {
                            new { name = "Job ID:", value = summary.DagId },
                            new { name = "Run ID:", value = summary.DagRunId },
                            new { name = "Status:", value = summary.Status },
                            new { name = "Start Time:", value = summary.StartTime.ToString("yyyy-MM-dd HH:mm:ss") },
                            new { name = "End Time:", value = summary.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A" },
                            new { name = "Duration:", value = summary.Duration },
                            new { name = "Total Traffic Lights:", value = summary.TotalTrafficLights.ToString() },
                            new { name = "Processed Traffic Lights:", value = summary.ProcessedTrafficLights.ToString() },
                            new { name = "Success Rate:", value = successRateDisplay },
                            new { name = "Total Commands:", value = summary.TotalCommands.ToString() },
                            new { name = "Completed Commands:", value = summary.CompletedCommands.ToString() },
                            new { name = "Failed Commands:", value = summary.ErrorCommands.ToString() },
                            new { name = "Timeout Commands:", value = summary.TimeoutCommands.ToString() }
                        }
                    }
                };

                // If there are error details, add them as a new section
                if (summary.ErrorDetails != null && summary.ErrorDetails.Count > 0)
                {
                    var errorSection = new
                    {
                        title = "Error Need Manual Check",
                        text = string.Join("\n\n", summary.ErrorDetails.Select(e =>
                            $"- Light ID: {e.TrafficLightId}\n  Camera ID: {e.CameraId}\n  Code: {e.ReasonCode}\n  Reason: {e.Reason}"
                        ))
                    };

                    // Add the error section to the sections list
                    sections.Add(errorSection);
                }

                // Create Teams message card with all sections
                var card = new
                {
                    type = "MessageCard",
                    context = "http://schema.org/extensions",
                    themeColor = themeColor,
                    summary = $"Traffic Light Collection Job Summary - {summary.DagId}",
                    title = $"Traffic Light Collection Job Summary",
                    text = $"{mentionsText}Summary for job {summary.DagId} run on {summary.ExecutionDate:yyyy-MM-dd HH:mm:ss}",
                    sections = sections.ToArray()
                };

                // Serialize to JSON
                var json = JsonConvert.SerializeObject(card);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Send to Teams webhook
                var response = await _httpClient.PostAsync(webhookUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to send Teams notification. Status code: {response.StatusCode}, Error: {errorContent}");
                }
                else
                {
                    _logger.LogInformation($"Teams notification sent successfully for job {summary.DagId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while sending Teams notification for job {summary.DagId}");
            }
        }

        private async Task SendEmailNotificationAsync(JobSummary summary, string[] recipients)
        {
            try
            {
                // Email sending logic would go here
                // This is a placeholder for actual email implementation
                _logger.LogInformation($"Email notification would be sent to: {string.Join(", ", recipients)}");

                // In a real implementation, you would use a service like SendGrid, SMTP, etc.
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while sending email notification for job {summary.DagId}");
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}