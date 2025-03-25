using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.KafkaClient;
using Platform.KafkaClient.Models;
using Platform.Serilog;

namespace Platform.TrafficDataCollection.ExtractFrames.Service
{
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
                services.AddHostedService<HostedService>();

                services.Configure<ProducerConfig>("Producer", context.Configuration.GetSection("Producer"));
                services.AddSingleton<IProducer>(s =>
                {
                    var settings = s.GetRequiredService<IOptionsMonitor<ProducerConfig>>().Get("Producer");
                    var logger = s.GetRequiredService<ILogger<Producer>>();

                    return new Producer(logger, settings);
                });

                services.Configure<ConsumerConfig>("Consumer",
                        context.Configuration.GetSection("Consumer"));
                services.AddSingleton<IConsumer>(s =>
                {
                    var settings = s.GetRequiredService<IOptionsMonitor<ConsumerConfig>>().Get("Consumer");
                    var logger = s.GetRequiredService<ILogger<Consumer>>();

                    return new Consumer(logger, settings);
                });
            })
            .RegisterSerilogConfig();
    }
}
