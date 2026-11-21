using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Parties;

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
        var group = await _client.AddListEntryAsync("collection-groups", HttpExtensions.Unique("CINA"));
        var dynasty = await _client.AddListEntryAsync("dynasties", HttpExtensions.Unique("Sung"));
        var location = await _client.AddListEntryAsync("storage-locations", HttpExtensions.Unique("MI F3"));
        var sothebys = await _client.AddPartyAsync(HttpExtensions.Unique("Sotheby's London"));
        var rossetti = await _client.AddPartyAsync(HttpExtensions.Unique("Rossetti Fine Arts srl"));
        var request = new ArtefactWriteRequest(HttpExtensions.Unique("CER"), "Coppa Celadon",
            Acquisition: new AcquisitionInput(AcquisitionMethod.Purchase, new DateOnly(1990, 6, 12), sothebys.Id,
                new MoneyDto(350_000_000m, "ITL"), Lot: "170", Note: "acquistato fuori asta"),
            ObjectTypeId: type.Id,
            CollectionGroupId: group.Id,
            PlaceOfProduction: new PlaceOfProductionInput("Cina", china.Id),
            DynastyId: dynasty.Id,
            Exhibitions: ["Arte Ottomana 1450-1600, Genova, 1999", "Exhibition of Persian Art, New York, 1940"],
            CatalogueEntryEn: "A JUNYAO BOWL\nJIN DYNASTY (1115-1234)",
            HolderId: rossetti.Id,
            StorageLocationId: location.Id,
            Valuation: new MoneyDto(200_000m, "GBP"));

        var created = await _client.CreateArtefactAsync(request);

        Assert.Equal(type.Id, created.ObjectType!.Id);
        Assert.Equal(type.Name, created.ObjectType.Name);
        Assert.Equal(new NamedRefDto(group.Id, group.Name), created.CollectionGroup);
        Assert.Equal("Cina", created.PlaceOfProduction!.Text);
        Assert.Equal(china.Name, created.PlaceOfProduction.Region!.Name);
        Assert.Equal(new NamedRefDto(dynasty.Id, dynasty.Name), created.Dynasty);
        Assert.Equal(["Arte Ottomana 1450-1600, Genova, 1999", "Exhibition of Persian Art, New York, 1940"], created.Exhibitions);
        Assert.Equal("A JUNYAO BOWL\nJIN DYNASTY (1115-1234)", created.CatalogueEntryEn);
        Assert.Equal(new PartyRefDto(rossetti.Id, rossetti.Name, PartyKind.Organisation), created.Holder);
        Assert.Equal(new NamedRefDto(location.Id, location.Name), created.StorageLocation);
        Assert.Equal(sothebys.Name, created.Acquisition!.Source!.Name);
        Assert.Equal(new MoneyDto(200_000m, "GBP"), created.Valuation);
        Assert.Equal("170", created.Acquisition!.Lot);
        Assert.Equal("acquistato fuori asta", created.Acquisition.Note);
        Assert.Equal(new MoneyDto(350_000_000m, "ITL"), created.Acquisition.Price);

        var updated = await (await _client.PutJsonAsync($"/api/artefacts/{created.Id}", request with
        {
            PlaceOfProduction = new PlaceOfProductionInput("Anatolia o Asia Centrale?"),
            Exhibitions = ["Only one"],
            Valuation = null,
            HolderId = null,
        })).ReadAsync<ArtefactDetail>();

        Assert.Equal("Anatolia o Asia Centrale?", updated.PlaceOfProduction!.Text);
        Assert.Null(updated.PlaceOfProduction.Region);
        Assert.Equal(["Only one"], updated.Exhibitions);
        Assert.Null(updated.Valuation);
        // Without a holder the collection itself holds the artefact.
        Assert.Null(updated.Holder);
    }

    [Fact]
    public async Task Unknown_list_entries_and_parties_are_field_errors()
    {
        var response = await _client.PostJsonAsync("/api/artefacts", new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "T",
            Acquisition: new AcquisitionInput(AcquisitionMethod.Purchase, null, Guid.NewGuid(), null),
            CollectionGroupId: Guid.NewGuid(), DynastyId: Guid.NewGuid(), HolderId: Guid.NewGuid(), StorageLocationId: Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        foreach (var field in new[] { "dynastyId", "collectionGroupId", "holderId", "storageLocationId", "acquisition.sourceId" })
            Assert.True(errors.TryGetProperty(field, out _), field);
    }

    [Fact]
    public async Task An_entry_of_another_list_is_not_accepted()
    {
        var location = await _client.AddListEntryAsync("storage-locations", HttpExtensions.Unique("Beta"));

        var response = await _client.PostJsonAsync("/api/artefacts", new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "T",
            DynastyId: location.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("dynastyId", out _));
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
        var location = await _client.AddListEntryAsync("storage-locations", $"{token} 16");
        // A dynasty matches on its name in any language, not only the reader's.
        var dynasty = await _client.AddListEntryAsync("dynasties",
            new Dictionary<string, string?> { ["en"] = HttpExtensions.Unique("Ottoman"), ["it"] = $"Ottomana {token}" });
        var source = await _client.AddPartyAsync($"Galerie {token}");
        var holder = await _client.AddPartyAsync($"Holder {token}");
        var byLocation = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "A", StorageLocationId: location.Id));
        var byDynasty = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "B", DynastyId: dynasty.Id));
        var byPlace = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "C",
            PlaceOfProduction: new PlaceOfProductionInput($"Iran {token}")));
        var bySource = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "D",
            Acquisition: new AcquisitionInput(AcquisitionMethod.Purchase, null, source.Id, null)));
        var byHolder = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "E", HolderId: holder.Id));

        var result = await (await _client.GetAsync($"/api/artefacts?search={token.ToUpperInvariant()}&lang=en")).ReadAsync<ArtefactListPage>();

        Assert.Equal(new[] { byLocation.Id, byDynasty.Id, byPlace.Id, bySource.Id, byHolder.Id }.Order(), result.Items.Select(i => i.Id).Order());
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
    public async Task Grouping_by_collection_group_groups_by_entry()
    {
        var token = HttpExtensions.Unique("cg");
        var cina = await _client.AddListEntryAsync("collection-groups", $"CINA {token}");
        var turchia = await _client.AddListEntryAsync("collection-groups", $"TURCHIA {token}");
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"One {token}", CollectionGroupId: turchia.Id));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"Two {token}", CollectionGroupId: cina.Id));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"Three {token}", CollectionGroupId: cina.Id));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"Four {token}"));

        var result = await (await _client.GetAsync($"/api/artefacts?search={token}&group=collectionGroup")).ReadAsync<ArtefactListPage>();

        Assert.Equal([(cina.Id.ToString(), cina.Name, 2), (turchia.Id.ToString(), turchia.Name, 1), ((string?)null, (string?)null, 1)],
            result.Groups.Select(g => (g.Key, g.Label, g.Count)));
        Assert.Equal([cina.Name, cina.Name, turchia.Name, null], result.Items.Select(i => i.GroupLabel));
        Assert.Equal(cina.Name, result.Items[0].CollectionGroup);
    }

    [Theory]
    [InlineData("en", "Safavid")]
    [InlineData("it", "Safavide")]
    public async Task Grouping_by_dynasty_uses_the_readers_language(string lang, string label)
    {
        var token = HttpExtensions.Unique("dy");
        var dynasty = await _client.AddListEntryAsync("dynasties",
            new Dictionary<string, string?> { ["it"] = $"Safavide {token}", ["en"] = $"Safavid {token}" });
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"One {token}", DynastyId: dynasty.Id));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"Two {token}", DynastyId: dynasty.Id));

        var result = await (await _client.GetAsync($"/api/artefacts?search={token}&group=dynasty&lang={lang}")).ReadAsync<ArtefactListPage>();

        var group = Assert.Single(result.Groups);
        Assert.Equal((dynasty.Id.ToString(), $"{label} {token}", 2), (group.Key, group.Label, group.Count));
        Assert.All(result.Items, i => Assert.Equal($"{label} {token}", i.Dynasty));
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
        var dynasty = Assert.Single(artefact.Fields, f => f.Id == "dynasty");
        Assert.Null(dynasty.Sensitivity);
        Assert.Equal(FieldType.Reference, dynasty.Type);
        foreach (var link in new[] { "collectionGroup", "storageLocation", "holder", "acquisitionSource" })
            Assert.Equal(FieldType.Reference, Assert.Single(artefact.Fields, f => f.Id == link).Type);
    }
}
