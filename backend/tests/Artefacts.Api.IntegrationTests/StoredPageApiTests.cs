using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class StoredPageApiTests(ApiFactory factory)
{
    private const string GiftsDefinition = """
        {
          "root": {
            "type": "container", "id": "gifts",
            "dataSource": { "kind": "query", "queryId": "artefact-list", "variables": { "page": 1 } },
            "children": [
              { "type": "layout", "id": "frame", "layout": "stack", "children": [
                { "type": "widget", "id": "pager", "widgetId": "pager", "bindings": { "total": "artefacts.total" } } ] }
            ]
          }
        }
        """;

    private readonly HttpClient _client = factory.CreateClient();

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private async Task<HttpResponseMessage> PostAsync(string? name = null, string? slug = null, string definition = GiftsDefinition, bool nav = false, string? label = null) =>
        await _client.PostJsonAsync("/api/pages", new CreateStoredPageRequest(name ?? HttpExtensions.Unique("Gifts"), slug ?? HttpExtensions.Unique("gifts"), Json(definition), nav, label));

    private async Task<StoredPageDto> SaveAsync(string? name = null, string? slug = null, string definition = GiftsDefinition, bool nav = false, string? label = null)
    {
        var response = await PostAsync(name, slug, definition, nav, label);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<StoredPageDto>();
    }

    private static bool SameJson(JsonElement a, JsonElement b) => JsonNode.DeepEquals(JsonNode.Parse(a.GetRawText()), JsonNode.Parse(b.GetRawText()));

    [Fact]
    public async Task A_valid_page_is_saved_and_read_back_by_id_and_by_slug_unchanged()
    {
        var slug = HttpExtensions.Unique("gifts");
        var saved = await SaveAsync(slug: slug);

        var byId = await (await _client.GetAsync($"/api/pages/{saved.Id}")).ReadAsync<StoredPageDto>();
        var bySlug = await (await _client.GetAsync($"/api/pages/by-slug/{slug.ToUpperInvariant()}")).ReadAsync<StoredPageDto>();

        Assert.StartsWith("p-", saved.Id);
        Assert.Equal(StoredPageKind.User, byId.Kind);
        Assert.True(SameJson(Json(GiftsDefinition), byId.Definition));
        Assert.Equal(saved.Id, bySlug.Id);
    }

    [Fact]
    public async Task The_order_of_keys_in_a_definition_is_kept_because_it_orders_a_widgets_fields()
    {
        const string definition = """
            { "root": { "type": "container", "id": "c", "dataSource": { "kind": "static", "data": {} },
              "children": [ { "type": "widget", "id": "w", "widgetId": "field-list", "bindings": { "zeta": "z", "a": "a", "mmm": "m", "bb": "b" } } ] } }
            """;
        var saved = await SaveAsync(definition: definition);

        var read = await (await _client.GetAsync($"/api/pages/{saved.Id}")).ReadAsync<StoredPageDto>();

        var bindings = read.Definition.GetProperty("root").GetProperty("children")[0].GetProperty("bindings");
        Assert.Equal(["zeta", "a", "mmm", "bb"], bindings.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task A_system_page_shows_its_fields_in_the_order_the_release_ships_them()
    {
        var detail = await (await _client.GetAsync("/api/pages/artefact-detail")).ReadAsync<StoredPageDto>();

        var raw = detail.Definition.GetRawText();
        var fields = raw[raw.IndexOf("\"catalogue-fields\"", StringComparison.Ordinal)..];
        string[] expected = ["inventoryNumber", "maker", "creationDate", "medium", "dimensions"];
        Assert.Equal(expected, expected.OrderBy(name => fields.IndexOf($"\"{name}\"", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_slug_taken_by_another_page_is_a_validation_error_on_the_slug_ignoring_case()
    {
        var slug = HttpExtensions.Unique("taken");
        await SaveAsync(slug: slug);

        var response = await PostAsync(slug: slug.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("slug", (await response.ValidationErrorsAsync()).Keys);
    }

    [Theory]
    [InlineData("My Page!")]
    [InlineData("-leading")]
    [InlineData("double--hyphen")]
    [InlineData("")]
    public async Task An_invalid_slug_is_a_validation_error_on_the_slug(string slug)
    {
        var response = await PostAsync(slug: slug == "" ? " " : slug);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("slug", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task A_definition_naming_a_stored_query_that_does_not_exist_is_rejected_with_the_query_and_its_path()
    {
        var response = await PostAsync(definition: GiftsDefinition.Replace("artefact-list", "no-such-query"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = Assert.Single((await response.ValidationErrorsAsync())["definition"]);
        Assert.Contains("no-such-query", message);
        Assert.StartsWith("root.dataSource.queryId", message);
    }

    [Fact]
    public async Task A_container_without_a_data_source_is_rejected_with_the_containers_path()
    {
        var response = await PostAsync(definition: """{ "root": { "type": "layout", "id": "l", "layout": "stack", "children": [ { "type": "container", "id": "c", "children": [] } ] } }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains((await response.ValidationErrorsAsync())["definition"], m => m.StartsWith("root.children[0].dataSource"));
    }

    [Theory]
    [InlineData("""{ "root": { "type": "carousel", "id": "x" } }""", "root.type")]
    [InlineData("""{ "nothing": 1 }""", "root")]
    [InlineData("""{ "root": { "type": "layout", "id": "l", "layout": "diagonal", "children": [] } }""", "root.layout")]
    public async Task A_definition_that_is_not_a_page_is_rejected_with_the_path_of_the_problem(string definition, string path)
    {
        var response = await PostAsync(definition: definition);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains((await response.ValidationErrorsAsync())["definition"], m => m.StartsWith(path));
    }

    [Fact]
    public async Task A_page_shown_in_navigation_needs_a_label()
    {
        var response = await PostAsync(nav: true, label: " ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("navigationLabel", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Widget_and_binding_problems_are_not_rejected_when_saving()
    {
        var saved = await SaveAsync(definition: """
            { "root": { "type": "container", "id": "c", "dataSource": { "kind": "static", "data": { "title": "x" } },
              "children": [ { "type": "widget", "id": "w", "widgetId": "no-such-widget", "bindings": { "src": "title" } } ] } }
            """);

        Assert.StartsWith("p-", saved.Id);
    }

    [Fact]
    public async Task The_navigation_list_has_only_the_pages_shown_in_navigation()
    {
        var shown = await SaveAsync(nav: true, label: "Gifts");
        var hidden = await SaveAsync();

        var all = await (await _client.GetAsync("/api/pages")).ReadAsync<List<StoredPageSummary>>();
        var navigation = await (await _client.GetAsync("/api/pages?navigation=true")).ReadAsync<List<StoredPageSummary>>();

        Assert.Contains(all, p => p.Id == hidden.Id);
        Assert.Contains(navigation, p => p.Id == shown.Id && p.NavigationLabel == "Gifts");
        Assert.DoesNotContain(navigation, p => p.Id == hidden.Id);
    }

    [Fact]
    public async Task The_list_shows_system_pages_first_then_user_pages_with_their_kind()
    {
        var mine = await SaveAsync();

        var list = await (await _client.GetAsync("/api/pages")).ReadAsync<List<StoredPageSummary>>();

        Assert.Equal(["artefact-detail", "artefact-list"], list.Where(p => p.Kind == StoredPageKind.System).Select(p => p.Id).Order());
        Assert.True(list.FindIndex(p => p.Id == mine.Id) > list.FindLastIndex(p => p.Kind == StoredPageKind.System));
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task A_system_page_cannot_be_changed_or_deleted(string method)
    {
        var before = await (await _client.GetAsync("/api/pages/artefact-list")).ReadAsync<StoredPageDto>();

        var response = method == "PUT"
            ? await _client.PutJsonAsync("/api/pages/artefact-list", new ChangeStoredPageRequest(Definition: Json(GiftsDefinition)))
            : await _client.DeleteAsync("/api/pages/artefact-list");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("system_page", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.True(SameJson(before.Definition, (await (await _client.GetAsync("/api/pages/artefact-list")).ReadAsync<StoredPageDto>()).Definition));
    }

    [Fact]
    public async Task A_system_page_can_be_duplicated_and_the_copy_edited_without_changing_the_original()
    {
        var name = HttpExtensions.Unique("My list");
        var slug = HttpExtensions.Unique("my-list");
        var original = await (await _client.GetAsync("/api/pages/artefact-list")).ReadAsync<StoredPageDto>();

        var response = await _client.PostJsonAsync("/api/pages/artefact-list/duplicate", new DuplicateStoredPageRequest(name, slug));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var copy = await response.ReadAsync<StoredPageDto>();
        var edited = await _client.PutJsonAsync($"/api/pages/{copy.Id}", new ChangeStoredPageRequest(Definition: Json(GiftsDefinition), ShowInNavigation: true, NavigationLabel: "Mine"));

        Assert.Equal((name, slug, StoredPageKind.User), (copy.Name, copy.Slug, copy.Kind));
        Assert.True(SameJson(original.Definition, copy.Definition));
        Assert.False(copy.ShowInNavigation);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.True(SameJson(original.Definition, (await (await _client.GetAsync("/api/pages/artefact-list")).ReadAsync<StoredPageDto>()).Definition));
    }

    [Fact]
    public async Task Renaming_to_a_taken_name_is_a_validation_error_on_the_name()
    {
        var taken = HttpExtensions.Unique("Taken");
        await SaveAsync(taken);
        var other = await SaveAsync();

        var response = await _client.PutJsonAsync($"/api/pages/{other.Id}", new ChangeStoredPageRequest($"  {taken.ToUpperInvariant()} "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("name", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task A_page_can_be_renamed_without_sending_its_definition()
    {
        var saved = await SaveAsync();
        var name = HttpExtensions.Unique("Renamed");

        var renamed = await (await _client.PutJsonAsync($"/api/pages/{saved.Id}", new ChangeStoredPageRequest(Name: name))).ReadAsync<StoredPageDto>();

        Assert.Equal(name, renamed.Name);
        Assert.True(SameJson(Json(GiftsDefinition), renamed.Definition));
    }

    [Fact]
    public async Task A_deleted_page_is_no_longer_listed_and_its_address_does_not_exist()
    {
        var slug = HttpExtensions.Unique("gone");
        var saved = await SaveAsync(slug: slug, nav: true, label: "Gone");

        var deleted = await _client.DeleteAsync($"/api/pages/{saved.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.DoesNotContain(await (await _client.GetAsync("/api/pages?navigation=true")).ReadAsync<List<StoredPageSummary>>(), p => p.Id == saved.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/pages/by-slug/{slug}")).StatusCode);
    }

    [Fact]
    public async Task A_new_release_replaces_the_stored_definition_of_a_system_page_at_start()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CollectionDbContext>();
            const string old = "{\"root\": {}}";
            await db.Database.ExecuteSqlAsync($"UPDATE stored_pages SET definition = {old}::text::jsonb, name = 'Old' WHERE id = 'artefact-detail'");
        }
        Assert.Equal("Old", (await (await _client.GetAsync("/api/pages/artefact-detail")).ReadAsync<StoredPageDto>()).Name);

        var sync = factory.Services.GetServices<IHostedService>().Single(s => s.GetType().Name == "SystemPageSync");
        await sync.StartAsync(CancellationToken.None);

        var restored = await (await _client.GetAsync("/api/pages/artefact-detail")).ReadAsync<StoredPageDto>();
        Assert.Equal("Artefact", restored.Name);
        Assert.Equal("artefact", restored.Definition.GetProperty("root").GetProperty("id").GetString());
    }
}
