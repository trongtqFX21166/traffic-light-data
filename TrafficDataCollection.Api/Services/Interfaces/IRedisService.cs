using TrafficDataCollection.Api.Models.Cache;

namespace TrafficDataCollection.Api.Services.Interfaces
{
    public interface IRedisService
    {
        Task<bool> CreateIndexIfNotExistsAsync();
        Task<bool> LoadTrafficLightsToRedisAsync();
        Task<TrafficLightRedisModel> GetTrafficLightByIdAsync(int lightId);
        Task<IEnumerable<TrafficLightRedisModel>> SearchTrafficLightsAsync(double lat, double lng, double radius);

        Task<(IEnumerable<TrafficLightRedisModel>, long)> GetTrafficLightsAsync(int page = 1, int pageSize = 100);

    }
}
