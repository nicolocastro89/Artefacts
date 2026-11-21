using System.Net.Http.Json;
using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;

namespace Artefacts.Api.IntegrationTests;

/// <summary>Helpers for posting GraphQL requests and reading their responses.</summary>
internal static class Graph
{
    /// <summary>Posts an ad-hoc document; returns the whole response (data and errors).</summary>
    public static async Task<JsonElement> QueryAsync(this HttpClient client, string document, object? variables = null, string? lang = null)
    {
        var response = await client.PostAsJsonAsync("/graphql" + (lang is null ? "" : $"?lang={lang}"), new { query = document, variables });
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Runs a stored query by its identifier.</summary>
    public static async Task<JsonElement> RunStoredAsync(this HttpClient client, string id, object? variables = null, string? lang = null)
    {
        var response = await client.PostAsJsonAsync("/graphql" + (lang is null ? "" : $"?lang={lang}"), new { id, variables });
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static JsonElement Data(this JsonElement response)
    {
        if (response.TryGetProperty("errors", out var errors))
            throw new InvalidOperationException($"GraphQL errors: {errors}");
        return response.GetProperty("data");
    }

    public static IReadOnlyList<string> Errors(this JsonElement response) =>
        response.TryGetProperty("errors", out var errors)
            ? errors.EnumerateArray().Select(e => e.GetProperty("message").GetString() ?? "").ToList()
            : [];
}

[Collection(ApiCollection.Name)]
public class GraphApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Ad_hoc_query_returns_exactly_the_fields_asked_for()
    {
        var created = await _client.CreateArtefactAsync("Coppa Celadon");

        var data = (await _client.QueryAsync("query($id: UUID!) { artefact(id: $id) { title inventoryNumber } }", new { id = created.Id })).Data();

        var artefact = data.GetProperty("artefact");
        Assert.Equal(["title", "inventoryNumber"], artefact.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Coppa Celadon", artefact.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Stored_query_runs_by_its_identifier()
    {
        var created = await _client.CreateArtefactAsync("Piatto");

        var data = (await _client.RunStoredAsync("artefact-detail", new { id = created.Id })).Data();

        Assert.Equal("Piatto", data.GetProperty("artefact").GetProperty("title").GetString());
    }

    [Fact]
    public async Task Unknown_artefact_is_null_without_errors()
    {
        var response = await _client.QueryAsync("query($id: UUID!) { artefact(id: $id) { title } }", new { id = Guid.NewGuid() });

        Assert.Empty(response.Errors());
        Assert.Equal(JsonValueKind.Null, response.Data().GetProperty("artefact").ValueKind);
    }

    [Fact]
    public async Task Mutations_are_rejected()
    {
        var response = await _client.QueryAsync("mutation { deleteArtefact(id: \"x\") }");

        Assert.NotEmpty(response.Errors());
        Assert.False(response.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object && data.EnumerateObject().Any());
    }

    [Fact]
    public async Task Too_deep_query_is_rejected_naming_the_limit()
    {
        // Introspection nests freely, so it shows the depth rule without depending on the schema's own shape.
        var deep = "{ __schema { types { fields { type { ofType { ofType { ofType { ofType { ofType { name } } } } } } } } } }";

        var response = await _client.QueryAsync(deep);

        Assert.Contains(response.Errors(), e => e.Contains("depth", StringComparison.OrdinalIgnoreCase));
    }
}
