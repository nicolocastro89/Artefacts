using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Catalogue;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class GraphListTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> SeedAsync()
    {
        var token = HttpExtensions.Unique("lemons");
        var plates = await _client.AddObjectTypeAsync($"Plate {token}");
        var bowls = await _client.AddObjectTypeAsync($"Bowl {token}");
        var gifter = await _client.AddPartyAsync($"Donor {token}");
        for (var i = 0; i < 7; i++)
        {
            var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"Still life {token} {i:D2}",
                ObjectTypeId: i % 3 == 0 ? null : i % 2 == 0 ? plates.Id : bowls.Id,
                Acquisition: new AcquisitionInput(i < 3 ? AcquisitionMethod.Gift : AcquisitionMethod.Purchase, null, i < 3 ? gifter.Id : null, null)));
            if (i % 2 == 1) await _client.UploadAsync(created.Id, ("a.jpg", TestData.Jpeg()));
        }

        return token;
    }

    private static string Items(JsonElement page, string field) =>
        string.Join("|", page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty(field).GetString()));

    [Fact]
    public async Task List_matches_the_rest_list_for_search_group_sort_and_page()
    {
        var token = await SeedAsync();
        var rest = await (await _client.GetAsync($"/api/artefacts?search={token}&group=objectType&sort=title&pageSize=3&page=2")).ReadAsync<ArtefactListPage>();

        var page = (await _client.QueryAsync("""
            query($search: String) {
              artefacts(search: $search, group: OBJECT_TYPE, sort: TITLE, pageSize: 3, page: 2) {
                total page pageSize
                groups { key label count }
                items { id title groupKey groupLabel }
              }
            }
            """, new { search = token })).Data().GetProperty("artefacts");

        Assert.Equal(rest.Total, page.GetProperty("total").GetInt32());
        Assert.Equal(2, page.GetProperty("page").GetInt32());
        Assert.Equal(string.Join("|", rest.Items.Select(i => i.Id)), Items(page, "id"));
        Assert.Equal(string.Join("|", rest.Items.Select(i => i.GroupKey)), Items(page, "groupKey"));
        Assert.Equal(string.Join("|", rest.Items.Select(i => i.GroupLabel)), Items(page, "groupLabel"));
        Assert.Equal(
            rest.Groups.Select(g => (g.Key, g.Label, g.Count)),
            page.GetProperty("groups").EnumerateArray().Select(g => (
                g.GetProperty("key").GetString(), g.GetProperty("label").GetString(), g.GetProperty("count").GetInt32())));
    }

    [Fact]
    public async Task Where_filters_on_catalog_fields_before_grouping()
    {
        var token = await SeedAsync();

        var page = (await _client.QueryAsync("""
            query($search: String) {
              artefacts(search: $search, group: OBJECT_TYPE,
                        where: { acquisitionMethod: { eq: "Gift" }, imageCount: { gt: 0 } }) {
                total
                groups { count }
                items { title }
              }
            }
            """, new { search = token })).Data().GetProperty("artefacts");

        // Gifts are artefacts 0-2; of those only 1 has an image.
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal(1, page.GetProperty("groups").EnumerateArray().Sum(g => g.GetProperty("count").GetInt32()));
        Assert.Equal($"Still life {token} 01", Items(page, "title"));
    }

    [Fact]
    public async Task Order_replaces_sort_and_direction()
    {
        var token = await SeedAsync();

        var page = (await _client.QueryAsync("""
            query($search: String) { artefacts(search: $search, order: [{ title: DESC }], pageSize: 2) { items { title } } }
            """, new { search = token })).Data().GetProperty("artefacts");

        Assert.Equal($"Still life {token} 06|Still life {token} 05", Items(page, "title"));
    }

    [Fact]
    public async Task Filtering_on_a_field_that_is_not_filterable_is_an_error_naming_it()
    {
        var response = await _client.QueryAsync("""{ artefacts(where: { primaryImage: { eq: "x" } }) { total } }""");

        Assert.Contains(response.Errors(), e => e.Contains("primaryImage"));
    }

    [Fact]
    public async Task Page_size_over_the_maximum_is_an_error_stating_it()
    {
        var response = await _client.QueryAsync("{ artefacts(pageSize: 500) { total } }");

        Assert.Contains(response.Errors(), e => e.Contains("100"));
    }

    [Fact]
    public async Task Database_queries_do_not_grow_with_the_number_of_artefacts()
    {
        var token = HttpExtensions.Unique("batch");
        var region = await _client.AddRegionAsync($"Kashan {token}");
        var objectType = await _client.AddObjectTypeAsync($"Bowl {token}");
        var dynasty = await _client.AddListEntryAsync("dynasties", new Dictionary<string, string?> { ["en"] = $"Seljuk {token}" });
        var holder = await _client.AddPartyAsync($"Lender {token}");
        for (var i = 0; i < 25; i++)
        {
            var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"Bowl {token} {i:D2}",
                ObjectTypeId: objectType.Id, DynastyId: dynasty.Id, HolderId: holder.Id,
                Provenance: [new ProvenanceEntryInput(region.Id, new HistoricalDateInput("1650"), null)],
                Exhibitions: ["Venice"], Descriptions: new Dictionary<string, string?> { ["en"] = "A bowl" },
                CatalogueEntries: new Dictionary<string, string?> { ["en"] = "A BOWL" }));
            await _client.UploadAsync(created.Id, ("a.jpg", TestData.Jpeg()));
        }

        async Task<IReadOnlyList<string>> CommandsAsync(int size)
        {
            JsonElement response = default;
            var commands = await factory.Sql.CaptureAsync(async () => response = await _client.QueryAsync("""
                query($search: String, $size: Int) {
                  artefacts(search: $search, pageSize: $size) {
                    items {
                      objectType { name } dynasty { name } holder { name }
                      provenance { region { name } } images { urls { thumb } } exhibitions description catalogueEntry primaryImage { urls { thumb } }
                    }
                  }
                }
                """, new { search = token, size }));
            Assert.Equal(size, response.Data().GetProperty("artefacts").GetProperty("items").GetArrayLength());
            // Everything read to resolve a page: its rows, the names, parties and the four lists.
            return commands.Where(c => c.Contains("v_artefact") || c.Contains("unnest(") || c.Contains("FROM parties")).ToList();
        }

        var one = await CommandsAsync(1);
        var many = await CommandsAsync(25);

        Assert.Equal(one.Count, many.Count);
        Assert.True(many.Count <= 12, $"A page of 25 artefacts used {many.Count} database commands.");
    }
}
