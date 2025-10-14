using System.Text.Json;
using Microsoft.Extensions.Logging;
using APITest.Validator;

namespace APITest.APIDummyJSON
{
    #region Setup Classes
    public class Employees
    {
        public List<Employee> users { get; set; } = new();
    }

    public class Employee
    {
        public int id { get; set; } = 0;
        public string firstName { get; set; } = "";
        public string lastName { get; set; } = "";
        public int age { get; set; } = 0;
        public string gender { get; set; } = "";
        public string email { get; set; } = "";
        public string phone { get; set; } = "";
        public string username { get; set; } = "";
        public string password { get; set; } = "";
        public string birthDate { get; set; } = "";
        public string image { get; set; } = "";
        public Address address { get; set; } = new();
    }

    public class Address
    {
        public string address { get; set; } = "";
        public string city { get; set; } = "";
        public string state { get; set; } = "";
        public string stateCode { get; set; } = "";
        public string postalCode { get; set; } = "";
        public Coordinates coordinates { get; set; } = new();
    }

    public class Coordinates
    {
        public decimal lat { get; set; } = 0;
        public decimal lng { get; set; } = 0;
    }
    #endregion

    #region Tasks
    public class DummyJSONProcessor
    {
        //public HttpClient client = new();
        private readonly HttpClient _httpClient;
        private readonly ILogger<DummyJSONProcessor> _logger;
        private readonly IHostValidator _hostValidator;
        private readonly ApiSettings _apiSettings;

        public DummyJSONProcessor
        (
            HttpClient httpClient,
            ILogger<DummyJSONProcessor> logger,
            IHostValidator hostValidator,
            ApiSettings apiSettings
        )
        {
            _httpClient = httpClient;
            _logger = logger;
            _hostValidator = hostValidator;
            _apiSettings = apiSettings;
        }

        public async Task<List<Employee>> GetEmployeeAsync(string path)
        {
            List<Employee> employees = new();

            try
            {
                string fullUrl = new Uri(_httpClient.BaseAddress!, path).ToString();
                _hostValidator.ValidateHost(fullUrl, _apiSettings.AllowedHosts, "DummyJSON API");

                _logger.LogInformation("Fetching employees from {Path}", path);
                // HttpResponseMessage response = await client.GetAsync(Program.responseURL);
                HttpResponseMessage response = await _httpClient.GetAsync(path);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();

                    if (json.Contains("{\"users\""))
                    {
                        Employees? wrapper = JsonSerializer.Deserialize<Employees>(json);
                        if (wrapper != null)
                        {
                            employees = wrapper.users;
                            _logger.LogInformation($"Fetched {employees.Count} employees.");
                        }
                    }
                    else
                    {
                        Employee? user = JsonSerializer.Deserialize<Employee>(json);
                        if (user != null)
                        {
                            employees.Add(user);
                            _logger.LogInformation($"Fetched 1 employee.");
                        }
                    }
                }
                else
                {
                    _logger.LogError("Failed to fetch employees. Status code: {StatusCode}", response.StatusCode);
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (SecurityException ex)
            {
                _logger.LogError(ex, "Security validation failed");
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request error occurred");
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error occurred");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred");
                throw;
            }

            return employees;
        }
    }
    #endregion

    public class Application
    {
        private readonly DummyJSONProcessor _apiProcessor;
        private readonly ILogger<Application> _logger;

        public Application(DummyJSONProcessor apiProcessor, ILogger<Application> logger)
        {
            _apiProcessor = apiProcessor;
            _logger = logger;
        }

        public async Task<List<Employee>> RunAsync()
        {
            try
            {
                List<Employee> employees = await _apiProcessor.GetEmployeeAsync("users");

                // Display results
                // Console.WriteLine($"\nFound {employees.Count} employees:\n");
                // foreach (var employee in employees)
                // {
                //     Console.WriteLine($"ID: {employee.id}");
                //     Console.WriteLine($"Name: {employee.firstName} {employee.lastName}");
                //     Console.WriteLine($"Email: {employee.email}");
                //     Console.WriteLine($"Age: {employee.age}");
                //     Console.WriteLine();
                // }

                _logger.LogInformation("Application completed successfully");

                return employees;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Application failed");
                throw;
            }
        }

        #region Output
        public static void ShowEmployees(List<Employee> employees)
        {
            foreach (var user in employees)
            {
                Console.WriteLine($"ID: {user.id}");
                Console.WriteLine($"Name: {user.firstName + " " + user.lastName}");
                Console.WriteLine($"Age: {user.age}");
                Console.WriteLine($"Gender: {user.gender}");
                Console.WriteLine($"Email: {user.email}");
                Console.WriteLine($"Image: {user.image}");
                Console.WriteLine($"Address: {user.address.address}");
                Console.WriteLine($"City: {user.address.city}");
                Console.WriteLine($"State: {user.address.state}");
                Console.WriteLine($"State Code: {user.address.stateCode}");
                Console.WriteLine($"ZIP: {user.address.postalCode}");
                Console.WriteLine($"Lat: {user.address.coordinates.lat}");
                Console.WriteLine($"Lng: {user.address.coordinates.lng}");
                Console.WriteLine();

                // Type objectType = user.GetType();
                // PropertyInfo[] properties = objectType.GetProperties();

                // foreach (PropertyInfo property in properties)
                // {
                //     Console.WriteLine($"{property.Name}: {property.GetValue(user, null)}");
                // }
            }
        }
        #endregion
    }
}