using TrafficDataCollection.Api.Models.Entities;
using TrafficDataCollection.Api.Repository;
using TrafficDataCollection.Api.Repository.Interfaces;
using TrafficDataCollection.Api.Services.Interfaces;

namespace TrafficDataCollection.Api.Services
{
    public interface ICycleLightHistoryService
    {
        Task<IEnumerable<CycleLightHistory>> GetCycleLightHistoryAsync(int lightId, DateTime? fromTime = null, DateTime? toTime = null, int? limit = null);
    }
    public class CycleLightHistoryService : ICycleLightHistoryService
    {
        private readonly ICycleLightHistoryRepository _cycleLightHistoryRepository;
        private readonly ILogger<CycleLightHistoryService> _logger;

        public CycleLightHistoryService(
            ICycleLightHistoryRepository cycleLightHistoryRepository,
            ILogger<CycleLightHistoryService> logger)
        {
            _cycleLightHistoryRepository = cycleLightHistoryRepository;
            _logger = logger;
        }

        public async Task<IEnumerable<CycleLightHistory>> GetCycleLightHistoryAsync(int lightId, DateTime? fromTime = null, DateTime? toTime = null, int? limit = null)
        {
            try
            {
                _logger.LogInformation($"Fetching cycle light history for light ID {lightId} from {fromTime} to {toTime} with limit {limit}");
                return await _cycleLightHistoryRepository.GetByLightIdAsync(lightId, fromTime, toTime, limit ?? 500);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting cycle light history for light ID {lightId}");
                throw;
            }
        }
    }
}