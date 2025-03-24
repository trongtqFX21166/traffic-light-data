using TrafficDataCollection.Api.Models.Entities;

namespace TrafficDataCollection.Api.Services
{
    public interface ILightService
    {
        Task<IEnumerable<Light>> GetAllMainLightsAsync();
    }
}
