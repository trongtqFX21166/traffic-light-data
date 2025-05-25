using Microsoft.Extensions.Options;
using TrafficDataCollection.Api.Repository.Interfaces;
using TrafficDataCollection.Api.Services;
using Vietmap.NetCore.MongoDb;

namespace TrafficDataCollection.Api.Repository
{
    public static class RegisterMasterDb
    {
        public static void RegisterRepo(this IServiceCollection services, HostBuilderContext context)
        {
            services.Configure<MongoDbSettings>("MongoDbSettings", context.Configuration.GetSection("MongoDbSettings"));
            services.AddSingleton<IMongoDbHelper>((service) =>
            {
                var settings = service.GetRequiredService<IOptionsMonitor<MongoDbSettings>>().Get("MongoDbSettings");

                return new MongoDbHelper(settings);
            });
            services.AddScoped<ILightRepository, LightRepository>();
            services.AddScoped<ITrafficLightCommandRepository, TrafficLightCommandRepository>();
            services.AddScoped<IAirflowDagRunRepository, AirflowDagRunRepository>();

            services.AddScoped<ICycleLightHistoryRepository, CycleLightHistoryRepository>();
        }
    }
}
