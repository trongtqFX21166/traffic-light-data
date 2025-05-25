using TrafficDataCollection.Api.Models.Entities;

namespace TrafficDataCollection.Api.Repository.Interfaces
{
    public interface ILightRepository
    {
        Task<IEnumerable<Light>> GetAllLightsWithoutMainLightIdAsync();
        Task<Light> GetLightByIdAsync(int id);
        Task<IEnumerable<Light>> GetAllLightsAsync();
        Task<IEnumerable<Light>> GetLightsByMainLightIdAsync(int mainLightId);

        Task<CycleLight> GetCycleLightByLightIdAsync(int lightId);
        Task<IEnumerable<CycleLight>> GetAllActiveCycleLightsAsync();
    }
}
