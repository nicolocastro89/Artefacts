using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Catalogue;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CatalogueFieldsApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task New_fields_round_trip_through_create_and_update()
    {
        var type = await _client.AddObjectTypeAsync(HttpExtensions.Unique("Ceramics"));
        var china = await _client.AddRegionAsync(HttpExtensions.Unique("China"));
        var request = new ArtefactWriteRequest(HttpExtensions.Unique("CER"), "Coppa Celadon",
            Acquisition: new AcquisitionInput(AcquisitionMethod.Purchase, new DateOnly(1990, 6, 12), "Sotheby's London",
                new MoneyDto(350_000_000m, "ITL"), Lot: "170", Note: "acquistato fuori asta"),
            ObjectTypeId: type.Id,
            CollectionGroup: " CINA ",
            PlaceOfProduction: new PlaceOfProductionInput("Cina", china.Id),
            Dynasty: "Sung",
            Exhibitions: ["Arte Ottomana 1450-1600, Genova, 1999", "Exhibition of Persian Art, New York, 1940"],
            CatalogueEntryEn: "A JUNYAO BOWL\nJIN DYNASTY (1115-1234)",
            Holder: "Rossetti Fine Arts srl",
            StorageLocation: "MI F3",
            Valuation: new MoneyDto(200_000m, "GBP"));

        var created = await _client.CreateArtefactAsync(request);

        Assert.Equal(type.Id, created.ObjectType!.Id);
        Assert.Equal(type.Name, created.ObjectType.Name);
        Assert.Equal("CINA", created.CollectionGroup);
        Assert.Equal("Cina", created.PlaceOfProduction!.Text);
        Assert.Equal(china.Name, created.PlaceOfProduction.Region!.Name);
        Assert.Equal("Sung", created.Dynasty);
        Assert.Equal(["Arte Ottomana 1450-1600, Genova, 1999", "Exhibition of Persian Art, New York, 1940"], created.Exhibitions);
        Assert.Equal("A JUNYAO BOWL\nJIN DYNASTY (1115-1234)", created.CatalogueEntryEn);
        Assert.Equal("Rossetti Fine Arts srl", created.Holder);
        Assert.Equal("MI F3", created.StorageLocation);
        Assert.Equal(new MoneyDto(200_000m, "GBP"), created.Valuation);
        Assert.Equal("170", created.Acquisition!.Lot);
        Assert.Equal("acquistato fuori asta", created.Acquisition.Note);
        Assert.Equal(new MoneyDto(350_000_000m, "ITL"), created.Acquisition.Price);

        var updated = await (await _client.PutJsonAsync($"/api/artefacts/{created.Id}", request with
        {
            PlaceOfProduction = new PlaceOfProductionInput("Anatolia o Asia Centrale?"),
            Exhibitions = ["Only one"],
            Valuation = null,
        })).ReadAsync<ArtefactDetail>();

        Assert.Equal("Anatolia o Asia Centrale?", updated.PlaceOfProduction!.Text);
        Assert.Null(updated.PlaceOfProduction.Region);
        Assert.Equal(["Only one"], updated.Exhibitions);
        Assert.Null(updated.Valuation);
    }

    [Fact]
    public async Task Unknown_object_type_and_place_region_are_field_errors()
    {
        var response = await _client.PostJsonAsync("/api/artefacts", new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "T",
            ObjectTypeId: Guid.NewGuid(), PlaceOfProduction: new PlaceOfProductionInput("Somewhere", Guid.NewGuid()),
            Valuation: new MoneyDto(1m, "XYZ")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("objectTypeId", out _));
        Assert.True(errors.TryGetProperty("placeOfProduction.regionId", out _));
        Assert.True(errors.TryGetProperty("valuation.currency", out _));
    }

    [Fact]
    public async Task Region_used_as_place_of_production_cannot_be_deleted()
    {
        var region = await _client.AddRegionAsync(HttpExtensions.Unique("Anatolia"));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Candlestick",
            PlaceOfProduction: new PlaceOfProductionInput("Anatolia", region.Id)));

        var response = await _client.DeleteAsync($"/api/regions/{region.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("region_in_use", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Search_matches_storage_location_dynasty_and_place()
    {
        var token = HttpExtensions.Unique("cella");
        var byLocation = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "A", StorageLocation: $"{token} 16"));
        var byDynasty = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "B", Dynasty: $"Ottomana {token}"));
        var byPlace = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "C",
            PlaceOfProduction: new PlaceOfProductionInput($"Iran {token}")));

        var result = await (await _client.GetAsync($"/api/artefacts?search={token.ToUpperInvariant()}")).ReadAsync<ArtefactListPage>();

        Assert.Equal(new[] { byLocation.Id, byDynasty.Id, byPlace.Id }.Order(), result.Items.Select(i => i.Id).Order());
    }

    [Fact]
    public async Task Grouping_by_object_type_orders_by_group_and_counts_across_pages()
    {
        var token = HttpExtensions.Unique("grp");
        var bowls = await _client.AddObjectTypeAsync($"A {token}");
        var carpets = await _client.AddObjectTypeAsync($"B {token}");
        foreach (var (title, type) in new[] { ("Zeta", carpets), ("Alpha", bowls), ("Gamma", (ObjectTypeDto?)null), ("Beta", carpets), ("Delta", bowls) })
            await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"{title} {token}", ObjectTypeId: type?.Id));

        var page1 = await (await _client.GetAsync($"/api/artefacts?search={token}&group=objectType&sort=title&pageSize=3")).ReadAsync<ArtefactListPage>();
        var page2 = await (await _client.GetAsync($"/api/artefacts?search={token}&group=objectType&sort=title&pageSize=3&page=2")).ReadAsync<ArtefactListPage>();

        Assert.Equal(new[] { "Alpha", "Delta", "Beta" }, page1.Items.Select(i => i.Title.Split(' ')[0]));
        Assert.Equal(new[] { "Zeta", "Gamma" }, page2.Items.Select(i => i.Title.Split(' ')[0]));
        Assert.Equal(bowls.Name, page1.Items[0].GroupLabel);
        Assert.Null(page2.Items[1].GroupKey);

        Assert.Equal(page1.Groups, page2.Groups);
        Assert.Equal([(bowls.Name, 2), (carpets.Name, 2), ((string?)null, 1)], page1.Groups.Select(g => (g.Label, g.Count)));
    }

    [Fact]
    public async Task Grouping_by_century_puts_undated_artefacts_last()
    {
        var token = HttpExtensions.Unique("cent");
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"Undated {token}"));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"Late {token}",
            CreationDate: new HistoricalDateInput("late 17th century")));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"Early {token}",
            CreationDate: new HistoricalDateInput("1520")));

        var result = await (await _client.GetAsync($"/api/artefacts?search={token}&group=century")).ReadAsync<ArtefactListPage>();

        Assert.Equal(new[] { "Early", "Late", "Undated" }, result.Items.Select(i => i.Title.Split(' ')[0]));
        Assert.Equal([("16", "16th century", 1), ("17", "17th century", 1), ((string?)null, (string?)null, 1)],
            result.Groups.Select(g => (g.Key, g.Label, g.Count)));
    }

    [Fact]
    public async Task Grouping_by_collection_group_ignores_case_and_spacing()
    {
        var token = HttpExtensions.Unique("cg");
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"One {token}", CollectionGroup: "CINA"));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"Two {token}", CollectionGroup: "cina"));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"Three {token}", CollectionGroup: "  CINA "));

        var result = await (await _client.GetAsync($"/api/artefacts?search={token}&group=collectionGroup")).ReadAsync<ArtefactListPage>();

        var group = Assert.Single(result.Groups);
        Assert.Equal(3, group.Count);
        Assert.Equal("cina", group.Key);
        // Labelled with the most frequent spelling as written.
        Assert.Equal("CINA", group.Label);
        Assert.All(result.Items, i => Assert.Equal("CINA", i.GroupLabel));
    }

    [Fact]
    public async Task Unknown_group_value_is_a_validation_error_on_the_field()
    {
        var response = await _client.GetAsync("/api/artefacts?group=colour");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("group", out _));
    }

    [Fact]
    public async Task Field_catalog_tags_valuation_as_financial()
    {
        var catalog = await (await _client.GetAsync("/api/catalog")).ReadAsync<FieldCatalogDto>();
        var artefact = Assert.Single(catalog.Entities, e => e.Id == "Artefact");

        var valuation = Assert.Single(artefact.Fields, f => f.Id == "valuation");
        Assert.Equal(FieldType.Money, valuation.Type);
        Assert.Equal("financial", valuation.Sensitivity);
        Assert.Equal(["valuation_amount", "valuation_currency"], valuation.Columns);
        Assert.Null(Assert.Single(artefact.Fields, f => f.Id == "dynasty").Sensitivity);
    }
}
