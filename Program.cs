using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.KafkaClient;
using Platform.KafkaClient.Models;
using Platform.Serilog;
using TrafficDataCollection.AnalyzationResult.Service;
using TrafficDataCollection.AnalyzationResult.Service.Models;
using TrafficDataCollection.AnalyzationResult.Service.Repo;
using TrafficDataCollection.AnalyzationResult.Service.Service;
using Vietmap.NetCore.MongoDb;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            // Register main background service
            services.AddHostedService<AnalyzationResultService>();

            // Register Kafka consumer
            services.Configure<ConsumerConfig>("Consumer",
                context.Configuration.GetSection("Consumer"));
            services.AddSingleton<IConsumer>(s =>
            {
                var settings = s.GetRequiredService<IOptionsMonitor<ConsumerConfig>>().Get("Consumer");
                var logger = s.GetRequiredService<ILogger<Consumer>>();
                return new Consumer(logger, settings);
            });

            // Configure MongoDB
            services.Configure<MongoDbSettings>("MongoDbSettings",
                context.Configuration.GetSection("MongoDbSettings"));
            services.AddSingleton<IMongoDbHelper>(s =>
            {
                var settings = s.GetRequiredService<IOptionsMonitor<MongoDbSettings>>().Get("MongoDbSettings");
                return new MongoDbHelper(settings);
            });

            // Register repositories
            services.AddSingleton<ICycleLightRepository, CycleLightRepository>();
            services.AddSingleton<ICycleLightHistoryRepository, CycleLightHistoryRepository>();

            // Register notification service
            services.Configure<TeamsWebhookSettings>(context.Configuration.GetSection("TeamsWebhookSettings"));
            services.AddSingleton<ITeamsNotificationService, TeamsNotificationService>();
        })
        .RegisterSerilogConfig();
}