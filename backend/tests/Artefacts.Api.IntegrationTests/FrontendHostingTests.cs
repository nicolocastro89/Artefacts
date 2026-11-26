using System.Net;
using Artefacts.Api.IntegrationTests.Infrastructure;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class FrontendHostingTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/")]
    [InlineData("/artefacts/7f3c2a/print")]
    [InlineData("/queries")]
    public async Task Client_routes_get_index_html_revalidated(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ApiFactory.FrontendIndex, await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task Fingerprinted_assets_are_cached_for_good()
    {
        var response = await _client.GetAsync(ApiFactory.FrontendAsset);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl?.MaxAge);
        Assert.Contains("immutable", response.Headers.CacheControl!.Extensions.Select(e => e.Name));
    }

    [Theory]
    [InlineData("/api/no-such-thing")]
    [InlineData("/assets/missing-abc.js")]
    public async Task Unknown_api_paths_and_files_are_not_found(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Api_endpoints_are_not_shadowed()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }
}
