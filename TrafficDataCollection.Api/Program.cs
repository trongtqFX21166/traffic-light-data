
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Platform.KafkaClient;
using Platform.KafkaClient.Models;
using TrafficDataCollection.Api.Helper;
using TrafficDataCollection.Api.Models;
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

            // Add API versioning services
            builder.Services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
            })
            .AddApiExplorer(options =>
            {
                // Format the version as "v1.0" in the URL
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

            // Configure Swagger for API versioning
            builder.Services.AddSwaggerGen(options =>
            {
                // Add a swagger document for each discovered API version
                var provider = builder.Services.BuildServiceProvider()
                    .GetRequiredService<IApiVersionDescriptionProvider>();

                foreach (var description in provider.ApiVersionDescriptions)
                {
                    options.SwaggerDoc(
                        description.GroupName,
                        new OpenApiInfo
                        {
                            Title = $"Traffic Light API {description.ApiVersion}",
                            Version = description.ApiVersion.ToString(),
                            Description = "API for traffic light data collection and analysis"
                        });
                }

                options.OperationFilter<SwaggerDefaultValues>();
            });


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
                services.AddTransient<ICycleLightHistoryService, CycleLightHistoryService>();

                services.Configure<RedisSettings>(context.Configuration.GetSection("LightRedis"));
                services.AddScoped<IRedisService, RedisService>();

                // Register notification service
                builder.Services.AddHttpClient("TeamNotifyClient");
                builder.Services.AddSingleton<INotificationService, NotificationService>();

            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsProduction())
            {
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

                    foreach (var description in provider.ApiVersionDescriptions)
                    {
                        options.SwaggerEndpoint(
                            $"/swagger/{description.GroupName}/swagger.json",
                            description.GroupName.ToUpperInvariant());
                    }
                });
            }

            // Add global exception handler middleware
            app.UseGlobalExceptionHandler();

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
