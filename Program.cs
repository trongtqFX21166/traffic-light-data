
using Microsoft.Extensions.Options;
using Platform.KafkaClient;
using Platform.KafkaClient.Models;
using TrafficDataCollection.Api.Models.Cache;
using TrafficDataCollection.Api.Repository;
using TrafficDataCollection.Api.Services;
using TrafficDataCollection.Api.Services.Interfaces;

namespace TrafficDataCollection.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Host.ConfigureServices((context, services) => {

                services.RegisterRepo(context);

                services.Configure<ProducerConfig>("CameraCollectionProducer", context.Configuration.GetSection("CameraCollectionProducer"));
                services.AddSingleton<IProducer>(s =>
                {
                    var settings = s.GetRequiredService<IOptionsMonitor<ProducerConfig>>().Get("CameraCollectionProducer");
                    var logger = s.GetRequiredService<ILogger<Producer>>();

                    return new Producer(logger, settings);
                });

                services.AddTransient<ILightService, LightService>();
                services.AddTransient<ISchedulerService, SchedulerService>();

                services.Configure<RedisSettings>(context.Configuration.GetSection("LightRedis"));
                services.AddScoped<IRedisService, RedisService>();

            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
