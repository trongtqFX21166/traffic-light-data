using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using TrafficDataCollection.AnalyzationResult.Service.Models;
using Vietmap.NetCore.MongoDb;

namespace TrafficDataCollection.AnalyzationResult.Service.Repo
{
    public interface ITrafficLightCommandRepository
    {
        Task<TrafficLightCommand> GetBySeqIdAsync(string seqId);
        Task UpdateAsync(string seqId, string status, string reasonCode, string reason);
    }

    public class TrafficLightCommandRepository : ITrafficLightCommandRepository
    {
        private readonly IMongoCollection<TrafficLightCommand> _cmds;
        private readonly ILogger<TrafficLightCommandRepository> _logger;

        public TrafficLightCommandRepository(IMongoDbHelper dbHelper, ILogger<TrafficLightCommandRepository> logger)
        {
            _cmds = dbHelper.GetCollection<TrafficLightCommand>("traffic_light_commands");
            _logger = logger;
        }

        public async Task<TrafficLightCommand> GetBySeqIdAsync(string seqId)
        {
            return await _cmds.Find(cmd => cmd.Id == seqId).FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(string seqId, string status, string reasonCode, string reason)
        {
            try
            {
                // Find the command with the given sequence ID
                var command = await GetBySeqIdAsync(seqId);

                if (command == null)
                {
                    _logger.LogWarning($"No traffic light command found with sequence ID: {seqId}");
                    return;
                }

                // Update the command status and reason
                var update = Builders<TrafficLightCommand>.Update
                    .Set(c => c.Status, status)
                    .Set(c => c.ReasonCode, reasonCode)
                    .Set(c => c.Reason, reason)
                    .Set(c => c.UpdatedAt, DateTime.UtcNow);

                // If this is a retry, increment the retry count and update the last retry time
                if (status == "Retry")
                {
                    update = update
                        .Inc(c => c.RetryCount, 1)
                        .Set(c => c.LastRetryTime, DateTime.UtcNow);
                }

                // Update the command in the database
                var result = await _cmds.UpdateOneAsync(
                    cmd => cmd.Id == seqId,
                    update);

                if (result.ModifiedCount > 0)
                {
                    _logger.LogInformation($"Successfully updated traffic light command {seqId} with status: {status}");
                }
                else
                {
                    _logger.LogWarning($"Failed to update traffic light command {seqId}, command not modified");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating traffic light command {seqId}");
                throw;
            }
        }
    }
}
