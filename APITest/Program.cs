/*
20251006 Joseph Parchem
This is the current method for standard API calls from C#. There is still a lot to research
    1. async methods
    2. httpClient factories
    3. List notation
    4. Json custom parsing and labels

I already have some code to connect to the Dataverse using a client secret and will be bringing that in
somehow. I think I will need to research how to have separate files for this namespace/class.

Once that is all together, I will continue to flesh out the Dataverse Employee table I started and have
this code insert all the dummy data into the table. More to do here too.
    1. Can this be run from Power Platform?
    2. Can app workflow run this somehow?
    3. Field by field comparison to log changes
    4. Where to store log files?
*/
using APITest.APIDummyJSON;
using APITest.Dataverse;
using APITest.Validator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using System.Globalization;
using System.Net.Http.Headers;

namespace APITest
{
    public class Program
    {
        // Connection to Dataverse
        static DataverseProcessor myDataverse = new();

        //private static string responseURL = "";
        private static string dataverseConnectionString = "";
        //private static string whiteListing = "";

        #region Main

        public static async Task Main(string[] args)
        {
            List<Employee> employees = new();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
                .Build();

            dataverseConnectionString = configuration.GetConnectionString("Dataverse") ?? throw new InvalidOperationException("Dataverse connection string is not found.");
            //responseURL = configuration.GetConnectionString("DummyJSON") ?? throw new InvalidOperationException("API connection string is not found.");

            // Dataverse Steps
            myDataverse.DataverseInitialize(dataverseConnectionString);

            // API HttpClientFactory and Logging setup
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    Console.WriteLine($"{context.HostingEnvironment.EnvironmentName}");

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

                    var tempValidator = new HostValidator(
                        services.BuildServiceProvider().GetRequiredService<ILogger<HostValidator>>());

                    tempValidator.ValidateHost(apiSettings.BaseUrl, apiSettings.AllowedHosts, "API");

                    services.AddHttpClient<DummyJSONProcessor>(client =>
                    {
                        client.BaseAddress = new Uri(apiSettings.BaseUrl);
                        client.DefaultRequestHeaders.Accept.Clear();
                        client.DefaultRequestHeaders.Accept.Add(
                            new MediaTypeWithQualityHeaderValue("application/json"));
                        client.Timeout = TimeSpan.FromSeconds(apiSettings.TimeoutSeconds);
                    });

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
                employees = await app.RunAsync();
            }

            // TEST/DEBUG
            //Application.ShowEmployees(employees);

            // Build Dataversie entities from API data
            List<Entity> entities = SyncEmployees(employees, employees.Count());
            // TEST/DEBUG
            //myDataverse.ShowEntities(entities);

            // Create entites in Dataverse
            // myDataverse.CreateEntities(entities);
        }
        #endregion

        #region Tasks
        static List<Entity> SyncEmployees(List<Employee> sources, int count)
        {
            List<Entity> entities = new();

            Dictionary<string, int> genderMap = myDataverse.GetChoiceMap("crfbe_employee", "crfbe_gender");

            for (int i = 0; i < count; i++)
            {
                Entity target = new Entity("crfbe_employee");
                target["crfbe_id"] = sources[i].id.ToString();
                target["crfbe_name"] = sources[i].firstName + " " + sources[i].lastName;
                target["crfbe_firstname"] = sources[i].firstName;
                target["crfbe_lastname"] = sources[i].lastName;
                target["crfbe_age"] = sources[i].age;

                string apiGenderText = sources[i].gender;

                if (genderMap.TryGetValue(apiGenderText.ToLower(), out int genderValue))
                    target["crfbe_gender"] = new OptionSetValue(genderValue);

                target["crfbe_email"] = sources[i].email;
                target["crfbe_workphone"] = sources[i].phone;
                target["crfbe_username"] = sources[i].username;
                target["crfbe_password"] = sources[i].password;

                DateTime dateOfBirth = DateTime.ParseExact(sources[i].birthDate, "yyyy-M-d", CultureInfo.InvariantCulture);
                target["crfbe_birithdate"] = dateOfBirth;

                target["crfbe_image"] = sources[i].image;
                target["crfbe_address1"] = sources[i].address.address;
                target["crfbe_city"] = sources[i].address.city;
                target["crfbe_statecode"] = sources[i].address.stateCode;
                target["crfbe_zip"] = sources[i].address.postalCode;
                target["crfbe_latitude"] = sources[i].address.coordinates.lat;
                target["crfbe_longitude"] = sources[i].address.coordinates.lng;

                entities.Add(target);
            }

            return entities;
        }

        public static void DefaultException(Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
            Console.WriteLine(ex.InnerException);
            Console.WriteLine(ex.Source);
        }
        #endregion
    }
}