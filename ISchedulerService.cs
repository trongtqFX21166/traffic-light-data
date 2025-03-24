namespace TrafficDataCollection.Api.Services
{
    public interface ISchedulerService
    {
        Task<string> TriggerCollectLightsAsync(string airflowDagId, string airflowDagRunId);
        Task<bool> CheckCollectLightsStatusAsync(string airflowDagId, string airflowDagRunId);
    }
}
