using MongoDB.Driver;
using TrafficDataCollection.AnalyzationResult.Service.Models;
using Vietmap.NetCore.MongoDb;

namespace TrafficDataCollection.AnalyzationResult.Service.Repo
{
    public interface ITrafficLightCommandRepository
    {
        Task UpdateAsync(string seqId, string status, string reason);
    }

    public class TrafficLightCommandRepository : ITrafficLightCommandRepository
    {
        private readonly IMongoCollection<TrafficLightCommand> _cmds;
        public TrafficLightCommandRepository(IMongoDbHelper dbHelper)
        {
            _cmds = dbHelper.GetCollection<TrafficLightCommand>("traffic_light_commands");
        }
        public async Task UpdateAsync(string seqId, string status, string reason)
        {
            //todo: save status
        }
    }
}
