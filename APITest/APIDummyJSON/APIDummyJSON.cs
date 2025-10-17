using APITest.Validator;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace APITest.APIDummyJSON
{
    #region Data Models
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

    #region API Service
    public interface IApiService
    {
        Task<List<Employee>> GetEmployeesAsync(string path);
    }

    public class DummyJSONService : IApiService
    {
        //public HttpClient client = new();
        private readonly HttpClient _httpClient;
        private readonly ILogger<DummyJSONService> _logger;
        private readonly IHostValidator _hostValidator;
        private readonly ApiSettings _apiSettings;

        public DummyJSONService
        (
            HttpClient httpClient,
            ILogger<DummyJSONService> logger,
            IHostValidator hostValidator,
            ApiSettings apiSettings
        )
        {
            _httpClient = httpClient;
            _logger = logger;
            _hostValidator = hostValidator;
            _apiSettings = apiSettings;
        }

        public async Task<List<Employee>> GetEmployeesAsync(string path)
        {
            List<Employee> employees = new();

            try
            {
                path += "?limit=0";
                string fullUrl = new Uri(_httpClient.BaseAddress!, path).ToString();
                _hostValidator.ValidateHost(fullUrl, _apiSettings.AllowedHosts, "DummyJSON API");

                _logger.LogInformation("Fetching employees from {Path}", path);

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
}