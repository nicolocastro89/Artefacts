using System.Net;
using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Infrastructure.Graph;
using Microsoft.Extensions.DependencyInjection;
using Artefacts.Application.Abstractions;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class StoredQueryDescriberTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private Task<QueryDescriptionDto> DescribeDocumentAsync(string document) =>
        factory.Services.GetRequiredService<IStoredQueryDescriber>().DescribeAsync(document, CancellationToken.None);

    /// <summary>Each case of the rules the frontend also checks: the GraphQL type must be described as the case says.</summary>
    public static TheoryData<string, string> SharedCases()
    {
        var data = new TheoryData<string, string>();
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "openapi", "field-type-rules.json"))) dir = dir.Parent;
        using var rules = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "openapi", "field-type-rules.json")));
        foreach (var c in rules.RootElement.GetProperty("cases").EnumerateArray())
            data.Add(c.GetProperty("graphql").GetString()!, c.GetProperty("type").GetString()!);
        return data;
    }

    [Theory]
    [MemberData(nameof(SharedCases))]
    public void Type_rules_match_the_cases_shared_with_the_frontend(string graphQlType, string expected)
    {
        var actual = FieldTypeRules.ObjectTypes.TryGetValue(graphQlType, out var special) ? special : FieldTypeRules.Scalar(graphQlType);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task The_list_query_is_described_by_path_and_type()
    {
        var response = await _client.GetAsync("/api/queries/artefact-list/description");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var description = await response.ReadAsync<QueryDescriptionDto>();
        Assert.Equal("image", description.Fields["artefacts.items[].primaryImage.urls"]);
        Assert.Equal("text", description.Fields["artefacts.items[].title"]);
        Assert.Equal("list", description.Fields["artefacts.groups[]"]);
        Assert.Equal("number", description.Fields["artefacts.total"]);
        Assert.Equal("historical-date", description.Fields["artefacts.items[].creationDate"]);
        Assert.False(description.Fields.ContainsKey("artefacts.items[].primaryImage"));
        Assert.Equal(new QueryVariableDto("number", false), description.Variables["page"]);
        Assert.Equal(new QueryVariableDto("text", false), description.Variables["sort"]);
    }

    [Fact]
    public async Task The_detail_query_describes_dates_money_references_and_nested_lists()
    {
        var description = await (await _client.GetAsync("/api/queries/artefact-detail/description")).ReadAsync<QueryDescriptionDto>();

        Assert.Equal("money", description.Fields["artefact.valuation"]);
        Assert.Equal("money", description.Fields["artefact.acquisition.price"]);
        Assert.Equal("date", description.Fields["artefact.acquisition.date"]);
        Assert.Equal("reference", description.Fields["artefact.holder.id"]);
        Assert.Equal("text", description.Fields["artefact.holder.kind"]);
        Assert.Equal("text", description.Fields["artefact.exhibitions[]"]);
        Assert.Equal("list", description.Fields["artefact.provenance[]"]);
        Assert.Equal("text", description.Fields["artefact.provenance[].region.name"]);
        Assert.Equal("historical-date", description.Fields["artefact.provenance[].period"]);
        Assert.Equal("text", description.Fields["artefact.catalogueEntryLanguage"]);
        Assert.Equal(new QueryVariableDto("reference", true), description.Variables["id"]);
    }

    [Fact]
    public async Task Aliases_are_path_segments_and_fragments_are_followed()
    {
        var description = await DescribeDocumentAsync("""
            query($size: Int = 10, $filter: ArtefactFilterInput, $id: UUID!) {
              latest: artefacts(pageSize: $size, where: $filter) { total ...Page }
              artefact(id: $id) { ... on Artefact { shown: title } }
            }
            fragment Page on ArtefactPage { rows: items { name: title cover: primaryImage { urls { web } } } }
            """);

        Assert.Equal("number", description.Fields["latest.total"]);
        Assert.Equal("list", description.Fields["latest.rows[]"]);
        Assert.Equal("text", description.Fields["latest.rows[].name"]);
        Assert.Equal("image", description.Fields["latest.rows[].cover.urls"]);
        Assert.Equal("text", description.Fields["artefact.shown"]);
        Assert.Equal(new QueryVariableDto("number", false), description.Variables["size"]);
        Assert.Equal(new QueryVariableDto("map", false), description.Variables["filter"]);
        Assert.Equal(new QueryVariableDto("reference", true), description.Variables["id"]);
    }

    [Fact]
    public async Task An_unknown_query_has_no_description()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/queries/q-nope/description")).StatusCode);
    }
}
