using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Artefacts.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the API against a throwaway PostgreSQL container and file-system media storage.
/// Shared by all API tests (one database); tests use unique names so they do not interfere.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const long MaxUploadBytes = 1024 * 1024; // 1 MB keeps the "oversized" test fast

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17.12").Build();

    public string MediaRoot { get; } = Path.Combine(Path.GetTempPath(), "artefacts-tests", Guid.NewGuid().ToString("N"));

    public string ConnectionString => _db.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Collection"] = _db.GetConnectionString(),
            ["Database:MigrateOnStartup"] = "true",
            ["Storage:Provider"] = "FileSystem",
            ["Storage:FileSystem:Root"] = MediaRoot,
            ["Imgproxy:PublicBaseUrl"] = "http://imgproxy.test",
            ["Imgproxy:Key"] = "736563726574",
            ["Imgproxy:Salt"] = "68656C6C6F",
            ["Media:MaxUploadBytes"] = MaxUploadBytes.ToString(),
            ["Imports:PollSeconds"] = "1",
        }));
    }

    public async ValueTask InitializeAsync()
    {
        await _db.StartAsync();
        _ = Server; // start the host (runs migrations)
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
        try { Directory.Delete(MediaRoot, recursive: true); } catch (DirectoryNotFoundException) { }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

public static class HttpExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Expected success but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var value = await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json);
        return value ?? throw new InvalidOperationException("Empty response body.");
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, ApiFactory.Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, ApiFactory.Json);

    public static Task<HttpResponseMessage> PatchJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PatchAsJsonAsync(url, body, ApiFactory.Json);

    /// <summary>Reads validation problem details and returns the error keys.</summary>
    public static async Task<IReadOnlyDictionary<string, string[]>> ValidationErrorsAsync(this HttpResponseMessage response)
    {
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        return doc.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
    }

    public static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
