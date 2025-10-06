//=====================================================================================
// Reqres with standard output, no parsing
//=====================================================================================
using System.Net.Http.Headers;
using System.Text.Json;

namespace APITest
{
    public class Program1
    {
        static HttpClient client = new HttpClient();

        public class EmployeeReqres
        {
            public List<EmployeeDataReqres> data { get; set; } = new();
        }

        public class EmployeeDJson
        {
            public List<EmployeeDataDJson> users { get; set; } = new();
        }
        public class EmployeeDataReqres
        {
            public string id { get; set; } = "";
            public string name { get; set; } = "";
            public string year { get; set; } = "";
            public string color { get; set; } = "";
            public string pantone_value { get; set; } = "";
        }

        public class EmployeeDataDJson
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
            public AddressDJson address { get; set; } = new();
        }

        public class AddressDJson
        {
            public string address { get; set; } = "";
            public string city { get; set; } = "";
            public string state { get; set; } = "";
            public string stateCode { get; set; } = "";
            public string postalCode { get; set; } = "";
            public CoordinatesDJson coordinates { get; set; } = new();
        }

        public class CoordinatesDJson
        {
            public float lat { get; set; } = 0;
            public float lng { get; set; } = 0;
        }

        static void ShowEmployeesReqRes(List<EmployeeDataReqres> employees)
        {
            foreach (var user in employees)
            {
                Console.WriteLine($"ID: {user.id}");
                Console.WriteLine($"Name: {user.name}");
                Console.WriteLine($"Year: {user.year}");
                Console.WriteLine($"Color: {user.color}");
                Console.WriteLine($"Pantone: {user.pantone_value}");
                Console.WriteLine();
            }
        }

        static void ShowEmployeesDJson(List<EmployeeDataDJson> employees)
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
            }
        }

        static async Task<EmployeeReqres> GetEmployeeAsyncReqres(string path)
        {
            EmployeeReqres employees = new EmployeeReqres();
            HttpResponseMessage response = await client.GetAsync(path);

            if (response.IsSuccessStatusCode)
            {
                employees = await response.Content.ReadAsAsync<EmployeeReqres>();
            }

            return employees;
        }

        static async Task<List<EmployeeDataDJson>> GetEmployeeAsyncDJson(string path)
        {
            List<EmployeeDataDJson> employees = new();

            try
            {
                HttpResponseMessage response = await client.GetAsync(path);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();

                    if (json.Contains("{\"users\""))
                    {
                        Console.WriteLine($"Working on multiple users...");
                        EmployeeDJson? wrapper = JsonSerializer.Deserialize<EmployeeDJson>(json);
                        if (wrapper != null) employees = wrapper.users;
                    }
                    else
                    {
                        EmployeeDataDJson? user = JsonSerializer.Deserialize<EmployeeDataDJson>(json);
                        if (user != null) employees.Add(user);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }

            return employees;
        }

        static void Main(string[] args)
        {
            // string responseURLReqres = "https://reqres.in/api/users";
            // string sourceReqres = "Reqres";
            // string apiKeyReqRes = "reqres-free-v1";

            string responseURLDJson = "https://dummyjson.com/users/1";
            string sourceDJson = "DJson";

            //RunAsync(responseURLReqres, apiKeyReqRes, sourceReqres).GetAwaiter().GetResult();
            RunAsync(responseURLDJson, "", sourceDJson).GetAwaiter().GetResult();
        }

        static async Task RunAsync(string responseURL, string apiKey, string source)
        {
            client.BaseAddress = new Uri(responseURL);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);

            try
            {
                switch (source)
                {
                    case "Reqres":
                        // var response = await client.GetStringAsync(client.BaseAddress);
                        // Console.WriteLine($"{response}");
                        EmployeeReqres employeesReqres = new EmployeeReqres();

                        employeesReqres = await GetEmployeeAsyncReqres(client.BaseAddress.ToString());
                        List<EmployeeDataReqres> employeeArrayReqres = employeesReqres.data;
                        ShowEmployeesReqRes(employeeArrayReqres);
                        break;
                    case "DJson":
                        // var response2 = await client.GetStringAsync(client.BaseAddress);
                        // Console.WriteLine($"{response2}");

                        List<EmployeeDataDJson> employees = await GetEmployeeAsyncDJson(client.BaseAddress.ToString());
                        ShowEmployeesDJson(employees);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}



//=====================================================================================
// Reqres with standard output, no parsing
//=====================================================================================
// using System.Net.Http.Headers;

// using HttpClient client = new();
// client.DefaultRequestHeaders.Accept.Clear();
// client.DefaultRequestHeaders.Accept.Add(
//     new MediaTypeWithQualityHeaderValue("application/json"));
// client.DefaultRequestHeaders.Add("x-api-key", "reqres-free-v1");

// await ProcessRepositoriesAsync(client);

// static async Task ProcessRepositoriesAsync(HttpClient client)
// {
//     var json = await client.GetStringAsync(
//         "https://reqres.in/api/employees/2");

//     Console.Write(json);
// }


//=====================================================================================
// GitHub with repositories
//=====================================================================================
// using System.Net.Http.Headers;
// using System.Net.Http.Json;

// using HttpClient client = new();
// client.DefaultRequestHeaders.Accept.Clear();
// client.DefaultRequestHeaders.Accept.Add(
//     new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
// client.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter");

// var respositories = await ProcessRepositoriesAsync(client);

// foreach (var repo in respositories)
// {
//     Console.WriteLine($"Name: {repo.Name}");
//     Console.WriteLine($"Homepage: {repo.Homepage}");
//     Console.WriteLine($"GitHub: {repo.GitHubHomeUrl}");
//     Console.WriteLine($"Description: {repo.Description}");
//     Console.WriteLine($"Watechers: {repo.Watchers:#,0}");
//     Console.WriteLine($"Last Push: {repo.LastPush}");
//     Console.WriteLine($"{repo.LastPushUtc}");
//     Console.WriteLine();
// }

// static async Task<List<Repository>> ProcessRepositoriesAsync(HttpClient client)
// //static async Task ProcessRepositoriesAsync(HttpClient client)
// {
//     var repositories = await client.GetFromJsonAsync<List<Repository>>("https://api.github.com/orgs/dotnet/repos");

//     return repositories ?? new();
// }