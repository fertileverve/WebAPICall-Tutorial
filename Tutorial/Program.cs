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
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace APITest
{
    public class Program
    {
        //Connection to API Land
        static HttpClient client = new HttpClient();

        //Connection for Dataverse
        static string dataverseConnect = "";
        static string dataverseSecretID = "";
        static string dataverseAppID = "";
        static string dataverseUri = "";

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
            public float lat { get; set; } = 0;
            public float lng { get; set; } = 0;
        }
        #endregion

        #region Output

        static void ShowEmployeesDJson(List<Employee> employees)
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

        #region Tasks

        static async Task<List<Employee>> GetEmployeeAsyncDJson(string path)
        {
            List<Employee> employees = new();

            try
            {
                HttpResponseMessage response = await client.GetAsync(path);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();

                    if (json.Contains("{\"users\""))
                    {
                        Console.WriteLine($"Working on multiple users...");
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
                Console.WriteLine($"{ex.Message}");
            }

            return employees;
        }
        #endregion

        static void Main(string[] args)
        {
            string responseURLDJson = "https://dummyjson.com/users/1";

            RunAsync(responseURLDJson).GetAwaiter().GetResult();
        }

        static async Task RunAsync(string responseURL)
        {
            client.BaseAddress = new Uri(responseURL);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                List<Employee> employees = await GetEmployeeAsyncDJson(client.BaseAddress.ToString());
                ShowEmployeesDJson(employees);

                //DEBUG
                // var response2 = await client.GetStringAsync(client.BaseAddress);
                // Console.WriteLine($"{response2}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            // Run import after the awaits are over
            // TODO: Is there a better way to make sure the async has completed?
            dataverseTest();
        }

        public static void dataverseInitialize()
        {
            dataverseSecretID = "";
            dataverseAppID = "";
            dataverseUri = "";

            dataverseConnect = $@"AuthType=ClientSecret;
                                SkipDiscovery=true;url={dataverseUri};
                                Secret={dataverseSecretID};
                                ClientId={dataverseAppID};
                                RequireNewInstance=true";
        }

        public static void dataverseTest()
        {
            dataverseInitialize();

            try
            {
                using (ServiceClient svc = new ServiceClient(dataverseConnect))
                {
                    if (svc.IsReady)
                    {
                        QueryExpression query = new QueryExpression("account");
                        query.ColumnSet = new ColumnSet(true);

                        EntityCollection accounts = svc.RetrieveMultiple(query);

                        foreach (Entity account in accounts.Entities)
                        {
                            Console.WriteLine($"Name: {account.GetAttributeValue<string>("name")}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to connect to Dataverse.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
        }

    }
}