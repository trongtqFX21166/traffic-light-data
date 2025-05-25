using TrafficDataCollection.Api.Models;

namespace TrafficDataCollection.Api.Services.Interfaces
{
    public interface ISchedulerService
    {
        Task<string> TriggerCollectLightsAsync(string airflowDagId, string airflowDagRunId);
        Task<bool> CheckCollectLightsStatusAsync(string airflowDagId, string airflowDagRunId);

        Task<(int, bool)> SetTimeoutForPendingCommandsAsync(string airflowDagId, string airflowDagRunId);

        Task<bool> UpdateDagRunStatusAsync(string airflowDagId, string airflowDagRunId, string status, string reason = null);

        Task<JobSummary> GenerateJobSummaryAndPushNotifyAsync(string airflowDagId, string airflowDagRunId, string[] notifyTo);
    }
}
