using TrafficDataCollection.Api.Models.Entities;

namespace TrafficDataCollection.Api.Repository.Interfaces
{
    public interface ITrafficLightCommandRepository
    {
        Task<IEnumerable<TrafficLightCommand>> GetCommandsByDagRunIdAsync(string dagRunId);
        Task<bool> AreAllCommandsCompletedAsync(string dagRunId);
        Task CreateManyAsync(IEnumerable<TrafficLightCommand> commands);
        Task UpdateAsync(TrafficLightCommand command);

        Task<IEnumerable<TrafficLightCommand>> GetPendingOrRunningCommandsByDagRunIdAsync(string dagRunId);
        Task UpdateManyAsync(IEnumerable<TrafficLightCommand> commands);

        Task<TrafficLightCommand> GetByIdAsync(string commandId);
        Task UpdateStatusAsync(string commandId, string status, string reasonCode, string reason);
    }
}
