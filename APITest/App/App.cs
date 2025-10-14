using APITest.APIDummyJSON;
using APITest.Dataverse;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;

namespace APITest.App
{
    #region Application Orchestration Layer
    public class Application
    {
        private readonly IApiService _apiService;
        private readonly IDataverseService _dataverseService;
        private readonly ILogger<Application> _logger;

        public Application(IApiService apiService, IDataverseService dataverseService, ILogger<Application> logger)
        {
            _apiService = apiService;
            _dataverseService = dataverseService;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            _logger.LogInformation("Application started");

            try
            {
                _logger.LogInformation("Testing Dataverse connection...");
                await _dataverseService.TestConnectionAsync();

                _logger.LogInformation("Fetching employees from API...");
                List<Employee> employees = await _apiService.GetEmployeesAsync("users");

                Console.WriteLine($"\nFound {employees.Count} employees from API\n");

                var genderMap = await _dataverseService.GetChoiceMapAsync("crfbe_employee", "crfbe_gender");

                List<Entity> entities = ConvertToDataverseEntities(employees, employees.Count, genderMap);

                _logger.LogInformation("Creating employee records in Dataverse...");
                await _dataverseService.CreateEmployeesAsync(entities);

                Console.WriteLine($"\nSuccessfully created {entities.Count} employee records in Dataverse!");

                _logger.LogInformation("Application completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Application failed");
                throw;
            }
        }

        static List<Entity> ConvertToDataverseEntities(List<Employee> sources, int count, Dictionary<string, int> genderMap)
        {
            List<Entity> entities = new();

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

                if (!string.IsNullOrEmpty(sources[i].birthDate))
                {
                    //DateTime dateOfBirth = DateTime.ParseExact(sources[i].birthDate, "yyyy-M-d", CultureInfo.InvariantCulture);
                    if (DateTime.TryParse(sources[i].birthDate, out DateTime dateOfBirth))
                        target["crfbe_birithdate"] = dateOfBirth;
                }

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
    #endregion
}