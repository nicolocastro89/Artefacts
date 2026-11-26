namespace Artefacts.Api;

/// <summary>
/// <c>dotnet Artefacts.Api.dll --health-check</c>: asks the running API for <c>/health</c> and exits 0 when it answers.
/// The container's healthcheck uses it because the ASP.NET runtime image has no curl or wget.
/// </summary>
public static class HealthProbe
{
    public static async Task<int> RunAsync()
    {
        var port = (Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080").Split(';', ',')[0];
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            using var response = await http.GetAsync($"http://127.0.0.1:{port}/health");
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
