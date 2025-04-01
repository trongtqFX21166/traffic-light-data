using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrafficDataCollection.AnalyzationResult.Service.Models;
using Vietmap.NetCore.MongoDb;

namespace TrafficDataCollection.AnalyzationResult.Service.Repo
{
    public interface ICycleLightHistoryRepository
    {
        Task CreateAsync(CycleLightHistory cycleLightHistory);
    }

    public class CycleLightHistoryRepository : ICycleLightHistoryRepository
    {
        private readonly IMongoCollection<CycleLightHistory> _cycleLightHistories;

        public CycleLightHistoryRepository(IMongoDbHelper dbHelper)
        {
            _cycleLightHistories = dbHelper.GetCollection<CycleLightHistory>("cycle_light_history");
        }

        public async Task CreateAsync(CycleLightHistory cycleLightHistory)
        {
            await _cycleLightHistories.InsertOneAsync(cycleLightHistory);
        }
    }
}
