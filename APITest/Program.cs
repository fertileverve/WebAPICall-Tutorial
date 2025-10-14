using APITest.App;
using APITest.APIDummyJSON;
using APITest.Dataverse;
using APITest.Validator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace APITest
{
    #region Program Entry Point
    public class Program
    {
        #region Main
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    Console.WriteLine($"{context.HostingEnvironment.EnvironmentName}");
                    Console.WriteLine($"{context.HostingEnvironment.IsDevelopment()}");

                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        config.AddUserSecrets<Program>();
                    }

                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;
                    var apiSettings = configuration.GetSection("Api").Get<ApiSettings>()
                        ?? throw new InvalidOperationException("Api settings not found in configuration.");
                    var dataverseSettings = configuration.GetSection("Dataverse").Get<DataverseSettings>()
                        ?? throw new InvalidOperationException("Dataverse settings no found in configuration.");

                    services.AddSingleton(apiSettings);
                    services.AddSingleton(dataverseSettings);
                    services.AddSingleton<IHostValidator, HostValidator>();
                    services.AddSingleton<IDataverseServiceFactory, DataverseServiceFactory>();

                    services.AddHttpClient<IApiService, DummyJSONService>(client =>
                    {
                        client.BaseAddress = new Uri(apiSettings.BaseUrl);
                        client.DefaultRequestHeaders.Accept.Clear();
                        client.DefaultRequestHeaders.Accept.Add(
                            new MediaTypeWithQualityHeaderValue("application/json"));
                        client.Timeout = TimeSpan.FromSeconds(apiSettings.TimeoutSeconds);
                    });

                    services.AddScoped<IDataverseService, DataverseService>();
                    services.AddTransient<Application>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .Build();

            using (var scope = host.Services.CreateScope())
            {
                var app = scope.ServiceProvider.GetRequiredService<Application>();
                await app.RunAsync();
            }
        }
        #endregion
    }
    #endregion
}