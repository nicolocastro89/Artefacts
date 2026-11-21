using Artefacts.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class GraphLimitsTests(ApiFactory factory)
{
    private HttpClient ClientWith(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration(c =>
            c.AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value)))).CreateClient();

    private async Task<(IReadOnlyList<string> Errors, int Reads)> RunAsync(HttpClient client, string document)
    {
        IReadOnlyList<string> errors = [];
        var commands = await factory.Sql.CaptureAsync(async () => errors = (await client.QueryAsync(document)).Errors());
        return (errors, commands.Count(c => c.Contains("v_artefact")));
    }

    [Fact]
    public async Task A_query_deeper_than_the_configured_depth_is_rejected_before_any_read()
    {
        var created = await factory.CreateClient().CreateArtefactAsync("Vase");
        var client = ClientWith(("GraphQL:MaxDepth", "2"));

        var (errors, reads) = await RunAsync(client, $$"""{ artefact(id: "{{created.Id}}") { objectType { name } } }""");

        Assert.Contains(errors, e => e.Contains("depth", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, reads);
    }

    [Fact]
    public async Task A_query_costlier_than_the_configured_cost_is_rejected_before_any_read()
    {
        var client = ClientWith(("GraphQL:MaxFieldCost", "5"));

        var (errors, reads) = await RunAsync(client, "{ artefacts { total items { title } } }");

        Assert.Contains(errors, e => e.Contains("cost", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, reads);
    }

    [Fact]
    public async Task The_same_query_runs_within_the_default_limits()
    {
        var created = await factory.CreateClient().CreateArtefactAsync("Vase");

        var (errors, reads) = await RunAsync(factory.CreateClient(),
            $$"""{ artefact(id: "{{created.Id}}") { objectType { name } } artefacts { total items { title } } }""");

        Assert.Empty(errors);
        Assert.True(reads > 0);
    }
}
