using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class StoredQueryApiTests(ApiFactory factory)
{
    private const string GiftsDocument = """
        query Gifts {
          artefacts(where: { acquisitionMethod: { eq: "Gift" } }) { items { title primaryImage { urls { thumb } } } }
        }
        """;

    private readonly HttpClient _client = factory.CreateClient();

    private async Task<StoredQueryDto> SaveAsync(string? name = null, string document = GiftsDocument)
    {
        var response = await _client.PostJsonAsync("/api/queries", new CreateStoredQueryRequest(name ?? HttpExtensions.Unique("Gifts"), document));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<StoredQueryDto>();
    }

    [Fact]
    public async Task A_valid_query_is_saved_and_read_back_unchanged()
    {
        var saved = await SaveAsync("Gifts with images " + HttpExtensions.Unique("x"));

        var read = await (await _client.GetAsync($"/api/queries/{saved.Id}")).ReadAsync<StoredQueryDto>();

        Assert.Equal(GiftsDocument, read.Document);
        Assert.Equal(StoredQueryKind.User, read.Kind);
        Assert.StartsWith("q-", read.Id);
    }

    [Fact]
    public async Task A_document_asking_for_a_field_the_schema_lacks_is_rejected_with_its_position()
    {
        var response = await _client.PostJsonAsync("/api/queries", new CreateStoredQueryRequest(HttpExtensions.Unique("Bad"),
            "query {\n  artefacts {\n    nonsense\n  }\n}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = Assert.Single((await response.ValidationErrorsAsync())["document"]);
        Assert.Contains("nonsense", message);
        Assert.Contains("line 3, column 5", message);
    }

    [Fact]
    public async Task A_document_with_two_operations_is_rejected()
    {
        var response = await _client.PostJsonAsync("/api/queries", new CreateStoredQueryRequest(HttpExtensions.Unique("Two"),
            "query A { language } query B { language }"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("document", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task A_syntax_error_is_rejected_with_its_position()
    {
        var response = await _client.PostJsonAsync("/api/queries", new CreateStoredQueryRequest(HttpExtensions.Unique("Syntax"), "query { language"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Matches(@"line \d+, column \d+", Assert.Single((await response.ValidationErrorsAsync())["document"]));
    }

    [Fact]
    public async Task The_list_shows_system_queries_first_then_user_queries_with_their_kind()
    {
        var mine = await SaveAsync();

        var list = await (await _client.GetAsync("/api/queries")).ReadAsync<List<StoredQuerySummary>>();

        Assert.Equal(["artefact-detail", "artefact-list"], list.Where(q => q.Kind == StoredQueryKind.System).Select(q => q.Id).Order());
        Assert.True(list.FindIndex(q => q.Id == mine.Id) > list.FindLastIndex(q => q.Kind == StoredQueryKind.System));
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task A_system_query_cannot_be_changed_or_deleted(string method)
    {
        var before = await (await _client.GetAsync("/api/queries/artefact-detail")).ReadAsync<StoredQueryDto>();

        var response = method == "PUT"
            ? await _client.PutJsonAsync("/api/queries/artefact-detail", new ChangeStoredQueryRequest(Document: "query { language }"))
            : await _client.DeleteAsync("/api/queries/artefact-detail");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("system_query", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(before.Document, (await (await _client.GetAsync("/api/queries/artefact-detail")).ReadAsync<StoredQueryDto>()).Document);
    }

    [Fact]
    public async Task A_system_query_can_be_duplicated_and_the_copy_edited()
    {
        var name = HttpExtensions.Unique("My list");
        var response = await _client.PostJsonAsync("/api/queries/artefact-list/duplicate", new DuplicateStoredQueryRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var copy = await response.ReadAsync<StoredQueryDto>();
        var original = await (await _client.GetAsync("/api/queries/artefact-list")).ReadAsync<StoredQueryDto>();

        var edited = await _client.PutJsonAsync($"/api/queries/{copy.Id}", new ChangeStoredQueryRequest(Document: "query { language }"));

        Assert.Equal((name, StoredQueryKind.User, original.Document), (copy.Name, copy.Kind, copy.Document));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal("query { language }", (await edited.ReadAsync<StoredQueryDto>()).Document);
    }

    [Fact]
    public async Task Renaming_to_a_taken_name_is_a_validation_error_on_the_name()
    {
        var taken = HttpExtensions.Unique("Taken");
        await SaveAsync(taken);
        var other = await SaveAsync();

        var response = await _client.PutJsonAsync($"/api/queries/{other.Id}", new ChangeStoredQueryRequest($"  {taken.ToUpperInvariant()} "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("name", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task A_query_can_be_renamed_without_sending_its_document()
    {
        var saved = await SaveAsync();
        var name = HttpExtensions.Unique("Renamed");

        var renamed = await (await _client.PutJsonAsync($"/api/queries/{saved.Id}", new ChangeStoredQueryRequest(Name: name))).ReadAsync<StoredQueryDto>();

        Assert.Equal((name, GiftsDocument), (renamed.Name, renamed.Document));
    }

    [Fact]
    public async Task A_deleted_query_is_no_longer_listed_and_running_it_reports_that_it_does_not_exist()
    {
        var saved = await SaveAsync();

        var deleted = await _client.DeleteAsync($"/api/queries/{saved.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.DoesNotContain(await (await _client.GetAsync("/api/queries")).ReadAsync<List<StoredQuerySummary>>(), q => q.Id == saved.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/queries/{saved.Id}")).StatusCode);
        Assert.Contains((await _client.RunStoredAsync(saved.Id)).Errors(), e => e.Contains("does not exist"));
    }

    [Fact]
    public async Task A_saved_query_runs_by_its_identifier_and_follows_its_edits()
    {
        var saved = await SaveAsync(document: "query { language }");
        Assert.Equal("en", (await _client.RunStoredAsync(saved.Id, lang: "en")).Data().GetProperty("language").GetString());

        await _client.PutJsonAsync($"/api/queries/{saved.Id}", new ChangeStoredQueryRequest(Document: "query { artefacts { total } }"));
        var after = (await _client.RunStoredAsync(saved.Id)).Data();

        Assert.False(after.TryGetProperty("language", out _));
        Assert.True(after.GetProperty("artefacts").GetProperty("total").GetInt32() >= 0);
    }
}
