using MongoDB.Driver;
using TrafficDataCollection.Api.Models.Entities;
using TrafficDataCollection.Api.Repository.Interfaces;
using Vietmap.NetCore.MongoDb;

namespace TrafficDataCollection.Api.Repository
{
    public class LightRepository : ILightRepository
    {
        private readonly IMongoCollection<Light> _lights;
        private readonly IMongoCollection<CycleLight> _cycleLights;

        private readonly IMongoDbHelper _dbHelper;

        public LightRepository(IEnumerable<IMongoDbHelper> dbHelpers)
        {
            _dbHelper = dbHelpers.FirstOrDefault(x => x.DatabaseName == "traffic_light_db");
            _lights = _dbHelper.GetCollection<Light>("light");
            _cycleLights = _dbHelper.GetCollection<CycleLight>("cycle_light");
        }

        public async Task<IEnumerable<Light>> GetAllLightsWithoutMainLightIdAsync()
        {
            return await _lights.Find(light => light.MainLightId == null).ToListAsync();
        }

        public async Task<Light> GetLightByIdAsync(int id)
        {
            return await _lights.Find(light => light.Id == id).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Light>> GetAllLightsAsync()
        {
            return await _lights.Find(_ => true).ToListAsync();
        }

        public async Task<IEnumerable<Light>> GetLightsByMainLightIdAsync(int mainLightId)
        {
            return await _lights.Find(light => light.MainLightId == mainLightId).ToListAsync();
        }

        public async Task<CycleLight> GetCycleLightByLightIdAsync(int lightId)
        {
            return await _cycleLights.Find(cycle => cycle.LightId == lightId && cycle.Status == "Active")
                .FirstOrDefaultAsync();
        }

       

        public async Task<IEnumerable<CycleLight>> GetAllActiveCycleLightsAsync()
        {
            return await _cycleLights.Find(cycle => cycle.Status == "Active").ToListAsync();
        }
    }
}
