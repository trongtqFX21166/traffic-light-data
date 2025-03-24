using MongoDB.Driver;
using TrafficDataCollection.Api.Models.Entities;
using TrafficDataCollection.Api.Repository.Interfaces;
using Vietmap.NetCore.MongoDb;

namespace TrafficDataCollection.Api.Repository
{
    public class AirflowDagRunRepository : IAirflowDagRunRepository
    {
        private readonly IMongoCollection<AirflowDagRun> _dagRuns;
        private readonly IMongoDbHelper _dbHelper;


        public AirflowDagRunRepository(IEnumerable<IMongoDbHelper> dbHelpers)
        {
            _dbHelper = dbHelpers.FirstOrDefault(x => x.DatabaseName == "traffic_light_db");
            _dagRuns = _dbHelper.GetCollection<AirflowDagRun>("airflow_dag_runs");
        }

        public async Task<AirflowDagRun> GetByAirflowIdsAsync(string airflowDagId, string airflowDagRunId)
        {
            return await _dagRuns.Find(run =>
                run.AirflowDagId == airflowDagId &&
                run.AirflowDagRunId == airflowDagRunId).FirstOrDefaultAsync();
        }

        public async Task CreateAsync(AirflowDagRun dagRun)
        {
            await _dagRuns.InsertOneAsync(dagRun);
        }

        public async Task UpdateAsync(AirflowDagRun dagRun)
        {
            await _dagRuns.ReplaceOneAsync(run => run.Id == dagRun.Id, dagRun);
        }
    }
}
