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
            await _commands.ReplaceOneAsync(cmd => cmd.Id == command.Id, command);
        }
    }
}
