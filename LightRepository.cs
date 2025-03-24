using MongoDB.Driver;
using TrafficDataCollection.Api.Models.Entities;
using TrafficDataCollection.Api.Repository.Interfaces;
using Vietmap.NetCore.MongoDb;

namespace TrafficDataCollection.Api.Repository
{
    public class LightRepository : ILightRepository
    {
        private readonly IMongoCollection<Light> _lights;
        private readonly IMongoDbHelper _dbHelper;

        public LightRepository(IEnumerable<IMongoDbHelper> dbHelpers)
        {
            _dbHelper = dbHelpers.FirstOrDefault(x => x.DatabaseName == "traffic_light_db");
            _lights = _dbHelper.GetCollection<Light>("light");
        }

        public async Task<IEnumerable<Light>> GetAllLightsWithoutMainLightIdAsync()
        {
            return await _lights.Find(light => string.IsNullOrEmpty(light.MainLightId)).ToListAsync();
        }

        public async Task<Light> GetLightByIdAsync(int id)
        {
            return await _lights.Find(light => light.Id == id).FirstOrDefaultAsync();
        }
    }
}
