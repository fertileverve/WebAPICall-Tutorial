using System.Net.Http.Headers;
using System.Text.Json;

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

    public class DummyJSONProcessor
    {
        public HttpClient client = new();

        #region Tasks
        public async Task<List<Employee>> GetEmployeeAsync()
        {
            List<Employee> employees = new();

            try
            {
                HttpResponseMessage response = await client.GetAsync(Program.responseURL);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();

                    if (json.Contains("{\"users\""))
                    {
                        Employees? wrapper = JsonSerializer.Deserialize<Employees>(json);
                        if (wrapper != null) employees = wrapper.users;
                    }
                    else
                    {
                        Employee? user = JsonSerializer.Deserialize<Employee>(json);
                        if (user != null) employees.Add(user);
                    }
                }
            }
            catch (Exception ex)
            {
                Program.DefaultException(ex);
            }

            return employees;
        }

        public void APIInitialize()
        {
            client.BaseAddress = new Uri(Program.responseURL);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }
        #endregion

        #region Output
        public void ShowEmployees(List<Employee> employees)
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