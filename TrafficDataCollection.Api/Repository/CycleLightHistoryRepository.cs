using MongoDB.Driver;
using TrafficDataCollection.Api.Models.Entities;
using TrafficDataCollection.Api.Repository.Interfaces;
using Vietmap.NetCore.MongoDb;

namespace TrafficDataCollection.Api.Repository
{
    public interface ICycleLightHistoryRepository
    {
        Task<IEnumerable<CycleLightHistory>> GetByLightIdAsync(int lightId, DateTime? fromTime = null, DateTime? toTime = null, int limit = 500);
        Task CreateAsync(CycleLightHistory cycleLightHistory);
    }
    public class CycleLightHistoryRepository : ICycleLightHistoryRepository
    {
        private readonly IMongoCollection<CycleLightHistory> _cycleLightHistories;
        private readonly IMongoDbHelper _dbHelper;

        public CycleLightHistoryRepository(IEnumerable<IMongoDbHelper> dbHelpers)
        {
            _dbHelper = dbHelpers.FirstOrDefault(x => x.DatabaseName == "traffic_light_db");
            _cycleLightHistories = _dbHelper.GetCollection<CycleLightHistory>("cycle_light_history");
        }

        public async Task CreateAsync(CycleLightHistory cycleLightHistory)
        {
            await _cycleLightHistories.InsertOneAsync(cycleLightHistory);
        }

        public async Task<IEnumerable<CycleLightHistory>> GetByLightIdAsync(int lightId, DateTime? fromTime = null, DateTime? toTime = null, int limit = 500)
        {
            // Create a filter builder
            var builder = Builders<CycleLightHistory>.Filter;
            var filter = builder.Eq(c => c.LightId, lightId);

            // Add time range filters if provided
            if (fromTime.HasValue)
            {
                // Convert to date without time component (start of day)
                var fromDate = fromTime.Value.Date;
                filter = filter & builder.Gte(c => c.CreatedAt, fromDate);
            }

            if (toTime.HasValue)
            {
                // Convert to date without time component and add one day (end of day)
                var toDate = toTime.Value.Date.AddDays(1);
                filter = filter & builder.Lt(c => c.CreatedAt, toDate);
            }

            // Create a fluent interface for the query
            var query = _cycleLightHistories.Find(filter)
                .SortByDescending(c => c.CreatedAt)
                .Limit(limit);


            // Execute the query
            return await query.ToListAsync();
        }
    }
}