using MongoDB.Driver;
using TrafficDataCollection.Api.Models.Entities;
using TrafficDataCollection.Api.Repository.Interfaces;
using Vietmap.NetCore.MongoDb;

namespace TrafficDataCollection.Api.Repository
{
    public class TrafficLightCommandRepository : ITrafficLightCommandRepository
    {
        private readonly IMongoDbHelper _dbHelper;
        private readonly IMongoCollection<TrafficLightCommand> _commands;

        public TrafficLightCommandRepository(IEnumerable<IMongoDbHelper> dbHelpers)
        {
            _dbHelper = dbHelpers.FirstOrDefault(x => x.DatabaseName == "traffic_light_db");
            _commands = _dbHelper.GetCollection<TrafficLightCommand>("traffic_light_commands");
        }

        public async Task<IEnumerable<TrafficLightCommand>> GetCommandsByDagRunIdAsync(string dagRunId)
        {
            return await _commands.Find(cmd => cmd.DagRunId == dagRunId).ToListAsync();
        }

        public async Task<bool> AreAllCommandsCompletedAsync(string dagRunId)
        {
            var pendingCount = await _commands.CountDocumentsAsync(cmd =>
                cmd.DagRunId == dagRunId &&
                (cmd.Status == "Pending" || cmd.Status == "Running"));

            return pendingCount == 0;
        }

        public async Task CreateManyAsync(IEnumerable<TrafficLightCommand> commands)
        {
            await _commands.InsertManyAsync(commands);
        }

        public async Task UpdateAsync(TrafficLightCommand command)
        {
            await _commands.ReplaceOneAsync(cmd => cmd.SeqId == command.SeqId, command);
        }

        public async Task<IEnumerable<TrafficLightCommand>> GetPendingOrRunningCommandsByDagRunIdAsync(string dagRunId)
        {
            return await _commands.Find(cmd =>
                cmd.DagRunId == dagRunId &&
                (cmd.Status == "Pending" || cmd.Status == "Running"))
                .ToListAsync();
        }

        public async Task UpdateManyAsync(IEnumerable<TrafficLightCommand> commands)
        {
            if (commands == null || !commands.Any())
                return;

            var bulkOps = new List<WriteModel<TrafficLightCommand>>();

            foreach (var command in commands)
            {
                var filter = Builders<TrafficLightCommand>.Filter.Eq(x => x.SeqId, command.SeqId);
                var updateOp = new ReplaceOneModel<TrafficLightCommand>(filter, command);
                bulkOps.Add(updateOp);
            }

            if (bulkOps.Any())
            {
                await _commands.BulkWriteAsync(bulkOps);
            }
        }

        public async Task<TrafficLightCommand> GetByIdAsync(string commandId)
        {
            return await _commands.Find(cmd => cmd.SeqId == commandId).FirstOrDefaultAsync();
        }


        public async Task UpdateStatusAsync(string commandId, string status, string reasonCode, string reason)
        {
            var filter = Builders<TrafficLightCommand>.Filter.Eq(x => x.SeqId, commandId);
            var update = Builders<TrafficLightCommand>.Update
                .Set(x => x.Status, status)
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .Set(x => x.ReasonCode, reasonCode)
                .Set(x => x.Reason, reason);

            await _commands.UpdateOneAsync(filter, update);
        }
    }
}
