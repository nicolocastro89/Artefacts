using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class GraphNestedListTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Provenance_is_listed_in_order_with_region_names_and_periods()
    {
        var flanders = await _client.AddRegionAsync(HttpExtensions.Unique("Flanders"));
        var paris = await _client.AddRegionAsync(HttpExtensions.Unique("Paris"));
        var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Still life",
            Provenance: [new ProvenanceEntryInput(flanders.Id, new HistoricalDateInput("c. 1650"), "Workshop"), new ProvenanceEntryInput(paris.Id, new HistoricalDateInput("1780s"), null)]));

        var data = (await _client.QueryAsync("""
            query($id: UUID!) { artefact(id: $id) { provenance { region { id name } period { display earliestYear } note } } }
            """, new { id = created.Id })).Data();

        var entries = data.GetProperty("artefact").GetProperty("provenance").EnumerateArray().ToList();
        Assert.Equal([flanders.Name, paris.Name], entries.Select(e => e.GetProperty("region").GetProperty("name").GetString()));
        Assert.Equal(["c. 1650", "1780s"], entries.Select(e => e.GetProperty("period").GetProperty("display").GetString()));
        Assert.Equal("Workshop", entries[0].GetProperty("note").GetString());
        Assert.Equal(JsonValueKind.Null, entries[1].GetProperty("note").ValueKind);
    }

    [Fact]
    public async Task Images_exhibitions_and_descriptions_are_listed()
    {
        var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Vase",
            Exhibitions: ["Venice 1999", "Paris 2004"],
            Descriptions: new Dictionary<string, string?> { ["it"] = "Un vaso", ["en"] = "A vase" }));
        // One upload each: images of one upload are attached 100 ns apart, which the database stores as the same time.
        await _client.UploadAsync(created.Id, ("front.jpg", TestData.Jpeg()));
        await _client.UploadAsync(created.Id, ("back.jpg", TestData.Jpeg()));

        var data = (await _client.QueryAsync("""
            query($id: UUID!) {
              artefact(id: $id) {
                exhibitions description descriptionLanguage descriptions { language text }
                images { originalFileName format isPrimary urls { thumb web print } }
                primaryImage { originalFileName isPrimary caption urls { thumb } }
              }
            }
            """, new { id = created.Id }, "it")).Data().GetProperty("artefact");

        Assert.Equal(["Venice 1999", "Paris 2004"], data.GetProperty("exhibitions").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("Un vaso", data.GetProperty("description").GetString());
        Assert.Equal("it", data.GetProperty("descriptionLanguage").GetString());
        Assert.Equal(["en", "it"], data.GetProperty("descriptions").EnumerateArray().Select(d => d.GetProperty("language").GetString()).Order());
        var images = data.GetProperty("images").EnumerateArray().ToList();
        Assert.Equal(["front.jpg", "back.jpg"], images.Select(i => i.GetProperty("originalFileName").GetString()));
        Assert.Equal("JPEG", images[0].GetProperty("format").GetString());
        Assert.Equal([true, false], images.Select(i => i.GetProperty("isPrimary").GetBoolean()));
        Assert.StartsWith("http://imgproxy.test", images[0].GetProperty("urls").GetProperty("web").GetString());
        var primary = data.GetProperty("primaryImage");
        Assert.Equal("front.jpg", primary.GetProperty("originalFileName").GetString());
        Assert.True(primary.GetProperty("isPrimary").GetBoolean());
        Assert.StartsWith("http://imgproxy.test", primary.GetProperty("urls").GetProperty("thumb").GetString());
    }

    [Fact]
    public async Task Description_falls_back_to_a_language_that_has_one()
    {
        var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Vase",
            Descriptions: new Dictionary<string, string?> { ["it"] = "Un vaso" }));

        var data = (await _client.QueryAsync("query($id: UUID!) { artefact(id: $id) { description descriptionLanguage } }",
            new { id = created.Id }, "en")).Data().GetProperty("artefact");

        Assert.Equal("Un vaso", data.GetProperty("description").GetString());
        Assert.Equal("it", data.GetProperty("descriptionLanguage").GetString());
    }

    [Fact]
    public async Task Catalogue_entries_resolve_in_the_readers_language_like_descriptions()
    {
        var both = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Bowl",
            CatalogueEntries: new Dictionary<string, string?> { ["en"] = "A BOWL\nJIN", ["it"] = "UNA COPPA" }));
        var onlyEnglish = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Dish",
            CatalogueEntries: new Dictionary<string, string?> { ["en"] = "A DISH" }));
        const string query = "query($id: UUID!) { artefact(id: $id) { catalogueEntry catalogueEntryLanguage catalogueEntries { language text } } }";

        var italian = (await _client.QueryAsync(query, new { id = both.Id }, "it")).Data().GetProperty("artefact");
        var fallback = (await _client.QueryAsync(query, new { id = onlyEnglish.Id }, "it")).Data().GetProperty("artefact");

        Assert.Equal("UNA COPPA", italian.GetProperty("catalogueEntry").GetString());
        Assert.Equal("it", italian.GetProperty("catalogueEntryLanguage").GetString());
        Assert.Equal(["en", "it"], italian.GetProperty("catalogueEntries").EnumerateArray().Select(e => e.GetProperty("language").GetString()));
        Assert.Equal("A BOWL\nJIN", italian.GetProperty("catalogueEntries")[0].GetProperty("text").GetString());
        Assert.Equal("A DISH", fallback.GetProperty("catalogueEntry").GetString());
        Assert.Equal("en", fallback.GetProperty("catalogueEntryLanguage").GetString());
    }
}
