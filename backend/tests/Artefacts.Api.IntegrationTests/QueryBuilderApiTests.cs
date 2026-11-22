using System.Net;
using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class QueryBuilderApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<BuilderRulesDto> RulesAsync() => await (await _client.GetAsync("/api/queries/builder-schema")).ReadAsync<BuilderRulesDto>();

    private async Task<DocumentDescriptionDto> DescribeAsync(string? document) =>
        await (await _client.PostJsonAsync("/api/queries/description", new DescribeDocumentRequest(document))).ReadAsync<DocumentDescriptionDto>();

    [Fact]
    public async Task The_list_can_be_filtered_on_marked_fields_with_the_operators_of_their_type()
    {
        var artefacts = (await RulesAsync()).Roots.Single(r => r.Name == "artefacts");

        var method = Assert.Single(artefacts.Filter!, f => f.Name == "acquisitionMethod");
        var count = Assert.Single(artefacts.Filter!, f => f.Name == "imageCount");

        Assert.Equal(("text", "String"), (method.ValueType, method.GraphQlType));
        Assert.Contains("contains", method.Operators.Select(o => o.Name));
        Assert.Equal(("number", "Int"), (count.ValueType, count.GraphQlType));
        Assert.DoesNotContain("contains", count.Operators.Select(o => o.Name));
        Assert.Contains(count.Operators, o => o is { Name: "in", List: true });
        Assert.Contains(count.Operators, o => o is { Name: "gt", List: false });
        Assert.DoesNotContain(artefacts.Filter!, f => f.Name is "images" or "provenance" or "and" or "or");
        Assert.Contains(artefacts.Sort!, s => s.Name == "title");
    }

    [Fact]
    public async Task Filter_fields_are_labelled_from_the_field_catalog_and_told_apart_when_a_field_has_several_columns()
    {
        var filter = (await RulesAsync()).Roots.Single(r => r.Name == "artefacts").Filter!;

        Assert.Equal("Title", filter.Single(f => f.Name == "title").Label);
        Assert.NotEqual(
            filter.Single(f => f.Name == "creationDateEarliestYear").Label, filter.Single(f => f.Name == "creationDateLatestYear").Label);
    }

    [Fact]
    public async Task Roots_state_their_arguments_and_the_single_artefact_takes_an_identifier()
    {
        var roots = (await RulesAsync()).Roots;

        var artefact = roots.Single(r => r.Name == "artefact");
        var id = Assert.Single(artefact.Arguments, a => a.Name == "id");
        Assert.Equal(("UUID", true), (id.Type, id.Required));
        Assert.Null(artefact.Filter);

        var page = roots.Single(r => r.Name == "artefacts").Arguments.Single(a => a.Name == "pageSize");
        Assert.Equal(("Int", "scalar", "25"), (page.Type, page.Kind, page.Default));
    }

    [Fact]
    public async Task Fields_have_their_kind_and_financial_fields_are_marked_sensitive()
    {
        var types = (await RulesAsync()).Types;

        var artefact = types["Artefact"].Fields;
        Assert.Equal("financial", artefact.Single(f => f.Name == "valuation").Sensitivity);
        Assert.Equal("financial", types["Acquisition"].Fields.Single(f => f.Name == "price").Sensitivity);
        Assert.Null(artefact.Single(f => f.Name == "title").Sensitivity);
        Assert.Equal(("object", true), (artefact.Single(f => f.Name == "provenance").Kind, artefact.Single(f => f.Name == "provenance").List));
        Assert.Equal(("scalar", "text"), (artefact.Single(f => f.Name == "title").Kind, artefact.Single(f => f.Name == "title").ValueType));
        Assert.Equal("money", artefact.Single(f => f.Name == "valuation").ValueType);
    }

    [Fact]
    public async Task Enums_list_their_values()
    {
        var enums = (await RulesAsync()).Enums;

        Assert.Contains("OBJECT_TYPE", enums["ArtefactGrouping"]);
        Assert.Contains("TITLE", enums["ArtefactSort"]);
    }

    [Fact]
    public async Task The_published_page_size_limit_is_the_one_the_server_enforces()
    {
        var max = (await RulesAsync()).Limits.MaxPageSize;

        var atMax = await _client.QueryAsync($"{{ artefacts(pageSize: {max}) {{ total }} }}");
        var overMax = await _client.QueryAsync($"{{ artefacts(pageSize: {max + 1}) {{ total }} }}");

        Assert.Empty(atMax.Errors());
        Assert.NotEmpty(overMax.Errors());
    }

    [Fact]
    public async Task The_rules_agree_with_the_published_schema()
    {
        var rules = await RulesAsync();
        var sdl = File.ReadAllText(PublishedSchema());

        foreach (var (name, type) in rules.Types.Where(t => t.Key is "Artefact" or "Acquisition" or "ArtefactPage"))
            Assert.Equal(FieldNames(sdl, "type", name).Order(), type.Fields.Select(f => f.Name).Order());

        Assert.Equal(
            FieldNames(sdl, "input", "ArtefactFilterInput").Where(n => n is not ("and" or "or")).Order(),
            rules.Roots.Single(r => r.Name == "artefacts").Filter!.Select(f => f.Name).Order());
        Assert.Equal(
            FieldNames(sdl, "input", "ArtefactSortInput").Order(), rules.Roots.Single(r => r.Name == "artefacts").Sort!.Select(f => f.Name).Order());
    }

    /// <summary>backend/openapi/schema.graphql, found by walking up from the test output.</summary>
    private static string PublishedSchema()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "openapi", "schema.graphql");
            if (File.Exists(path)) return path;
        }

        throw new FileNotFoundException("openapi/schema.graphql was not found above the test output.");
    }

    private static IEnumerable<string> FieldNames(string sdl, string kind, string name)
    {
        var block = System.Text.RegularExpressions.Regex.Match(sdl, $@"^{kind} {name} \{{\r?\n(?<body>.*?)^\}}", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(block.Success, $"{kind} {name} is not in the published schema.");
        return System.Text.RegularExpressions.Regex.Matches(block.Groups["body"].Value, @"^\s{2}(\w+)", System.Text.RegularExpressions.RegexOptions.Multiline).Select(m => m.Groups[1].Value);
    }

    [Fact]
    public async Task A_valid_document_is_described_without_being_stored()
    {
        var before = (await (await _client.GetAsync("/api/queries")).ReadAsync<List<StoredQuerySummary>>()).Count;

        var result = await DescribeAsync("query($search: String) { artefacts(search: $search) { items { title } } }");

        Assert.Empty(result.Problems);
        Assert.Equal("text", result.Description!.Fields["artefacts.items[].title"]);
        Assert.Equal(("text", false), (result.Description.Variables["search"].Type, result.Description.Variables["search"].Required));
        Assert.Equal(before, (await (await _client.GetAsync("/api/queries")).ReadAsync<List<StoredQuerySummary>>()).Count);
    }

    [Fact]
    public async Task An_invalid_document_is_reported_with_its_position()
    {
        var result = await DescribeAsync("query {\n  artefacts {\n    nonsense\n  }\n}");

        Assert.Null(result.Description);
        var problem = Assert.Single(result.Problems);
        Assert.Contains("nonsense", problem.Message);
        Assert.Equal((3, 5), (problem.Line, problem.Column));
    }

    [Fact]
    public async Task A_missing_document_is_a_problem_not_a_failure()
    {
        var response = await _client.PostJsonAsync("/api/queries/description", new DescribeDocumentRequest(null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty((await response.ReadAsync<DocumentDescriptionDto>()).Problems);
    }
}
