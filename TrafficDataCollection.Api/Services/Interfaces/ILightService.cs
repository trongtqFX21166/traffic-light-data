using TrafficDataCollection.Api.Models.Entities;

namespace TrafficDataCollection.Api.Services.Interfaces
{
    public interface ILightService
    {
        Task<IEnumerable<Light>> GetAllMainLightsAsync();
    }
}
