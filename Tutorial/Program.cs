using System.Net.Http.Headers;
using System.Net.Http.Json;

using HttpClient client = new();
client.DefaultRequestHeaders.Accept.Clear();
// client.DefaultRequestHeaders.Accept.Add(
//     new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
client.DefaultRequestHeaders.Accept.Add(
    new MediaTypeWithQualityHeaderValue("application/json"));
client.DefaultRequestHeaders.Add("x-api-key", "reqres-free-v1");
//client.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter");

var respositories = await ProcessRepositoriesAsync(client);
//await ProcessRepositoriesAsync(client);

foreach (var repo in respositories)
{
    // Console.WriteLine($"Name: {repo.Name}");
    // Console.WriteLine($"Homepage: {repo.Homepage}");
    // Console.WriteLine($"GitHub: {repo.GitHubHomeUrl}");
    // Console.WriteLine($"Description: {repo.Description}");
    // Console.WriteLine($"Watechers: {repo.Watchers:#,0}");
    // Console.WriteLine($"Last Push: {repo.LastPush}");
    // Console.WriteLine($"{repo.LastPushUtc}");
    // Console.WriteLine();

    Console.WriteLine($"ID: {repo.id}");
    Console.WriteLine($"Name: {repo.name}");

    Console.WriteLine();
}

static async Task<List<Repository>> ProcessRepositoriesAsync(HttpClient client)
//static async Task ProcessRepositoriesAsync(HttpClient client)
{
    // var json = await client.GetStringAsync(
    //     "https://reqres.in/api/users/2");

    // Console.Write(json);

    //var repositories = await client.GetFromJsonAsync<List<Repository>>("https://api.github.com/orgs/dotnet/repos");
    var repositories = await client.GetFromJsonAsync<List<Repository>>("https://reqres.in/api/users");

    return repositories ?? new();

    // foreach (var repo in repositories ?? Enumerable.Empty<Repository>())
    //     Console.WriteLine(repo.Name);
}