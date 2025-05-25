using TrafficDataCollection.Api.Models.Entities;

namespace TrafficDataCollection.Api.Repository.Interfaces
{
    public interface IAirflowDagRunRepository
    {
        Task<AirflowDagRun> GetByAirflowIdsAsync(string airflowDagId, string airflowDagRunId);
        Task CreateAsync(AirflowDagRun dagRun);
        Task UpdateAsync(AirflowDagRun dagRun);
    }
}
