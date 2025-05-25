using MongoDB.Driver;
using TrafficDataCollection.AnalyzationResult.Service.Models;
using Vietmap.NetCore.MongoDb;

namespace TrafficDataCollection.AnalyzationResult.Service.Repo
{
    public interface ICycleLightRepository
    {
        Task<CycleLight> GetByLightIdAsync(int lightId);
        Task UpdateAsync(CycleLight cycleLight);
        Task CreateAsync(CycleLight cycleLight);
    }

    public class CycleLightRepository : ICycleLightRepository
    {
        private readonly IMongoCollection<CycleLight> _cycleLights;

        public CycleLightRepository(IMongoDbHelper dbHelper)
        {
            _cycleLights = dbHelper.GetCollection<CycleLight>("cycle_light");
        }

        public async Task<CycleLight> GetByLightIdAsync(int lightId)
        {
            return await _cycleLights
                .Find(c => c.LightId == lightId)
                .FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(CycleLight cycleLight)
        {
            await _cycleLights.ReplaceOneAsync(
                c => c.Id == cycleLight.Id,
                cycleLight);
        }

        public async Task CreateAsync(CycleLight cycleLight)
        {
            await _cycleLights.InsertOneAsync(cycleLight);
        }
    }

}
