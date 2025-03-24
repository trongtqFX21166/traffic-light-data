using TrafficDataCollection.Api.Models.Entities;
using TrafficDataCollection.Api.Repository.Interfaces;
using TrafficDataCollection.Api.Services.Interfaces;

namespace TrafficDataCollection.Api.Services
{
    public class LightService : ILightService
    {
        private readonly ILightRepository _lightRepository;

        public LightService(ILightRepository lightRepository)
        {
            _lightRepository = lightRepository;
        }

        public async Task<IEnumerable<Light>> GetAllMainLightsAsync()
        {
            return await _lightRepository.GetAllLightsWithoutMainLightIdAsync();
        }
    }
}
