using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Lists;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ListApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Dynasty_has_a_name_per_language()
    {
        var token = HttpExtensions.Unique("x");
        var dynasty = await _client.AddListEntryAsync("dynasties",
            new Dictionary<string, string?> { ["it"] = $"Safavide {token}", ["en"] = $"Safavid {token}" });

        var en = await (await _client.GetAsync($"/api/lists/dynasties/{dynasty.Id}?lang=en")).ReadAsync<ReferenceEntryDto>();
        var it = await (await _client.GetAsync($"/api/lists/dynasties/{dynasty.Id}?lang=it")).ReadAsync<ReferenceEntryDto>();

        Assert.Equal(($"Safavid {token}", "en"), (en.Name, en.NameLanguage));
        Assert.Equal(($"Safavide {token}", "it"), (it.Name, it.NameLanguage));
        Assert.Equal(ListKind.Dynasty, en.Kind);
        Assert.Equal(2, en.Names!.Count);
    }

    [Fact]
    public async Task Storage_location_has_one_name_in_every_language()
    {
        var name = HttpExtensions.Unique("MI F3");
        var location = await _client.AddListEntryAsync("storage-locations", name);

        foreach (var lang in new[] { "en", "it" })
        {
            var read = await (await _client.GetAsync($"/api/lists/storage-locations/{location.Id}?lang={lang}")).ReadAsync<ReferenceEntryDto>();
            Assert.Equal(name, read.Name);
            Assert.Null(read.Names);
        }
    }

    [Fact]
    public async Task The_same_name_is_accepted_in_two_lists()
    {
        var name = HttpExtensions.Unique("Ottomana");
        var dynasty = await _client.AddListEntryAsync("dynasties", name);
        var group = await _client.AddListEntryAsync("collection-groups", name);

        Assert.NotEqual(dynasty.Id, group.Id);
        Assert.Equal(ListKind.CollectionGroup, group.Kind);
    }

    [Fact]
    public async Task Search_matches_a_name_in_another_language()
    {
        var token = HttpExtensions.Unique("x");
        var dynasty = await _client.AddListEntryAsync("dynasties",
            new Dictionary<string, string?> { ["it"] = $"Safavide {token}", ["en"] = $"Safavid {token}" });

        var found = await (await _client.GetAsync($"/api/lists/dynasties?search={Uri.EscapeDataString($"safavide {token}")}&lang=en"))
            .ReadAsync<List<ReferenceEntryDto>>();

        Assert.Equal($"Safavid {token}", Assert.Single(found, e => e.Id == dynasty.Id).Name);
    }

    [Fact]
    public async Task Adding_an_existing_location_returns_it()
    {
        var name = HttpExtensions.Unique("MI F3");
        var existing = await _client.AddListEntryAsync("storage-locations", name);

        var response = await _client.PostJsonAsync("/api/lists/storage-locations", new ReferenceEntryNamesRequest(Name: $"  {name.ToLowerInvariant()} "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(existing.Id, (await response.ReadAsync<ReferenceEntryDto>()).Id);
        var all = await (await _client.GetAsync($"/api/lists/storage-locations?search={Uri.EscapeDataString(name)}")).ReadAsync<List<ReferenceEntryDto>>();
        Assert.Single(all);
    }

    [Fact]
    public async Task Usage_count_is_the_number_of_artefacts_using_the_entry()
    {
        var location = await _client.AddListEntryAsync("storage-locations", HttpExtensions.Unique("Beta2"));
        for (var i = 0; i < 4; i++)
            await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Stored", StorageLocationId: location.Id));

        var listed = await (await _client.GetAsync($"/api/lists/storage-locations?search={Uri.EscapeDataString(location.Name)}")).ReadAsync<List<ReferenceEntryDto>>();

        Assert.Equal(4, Assert.Single(listed).ArtefactCount);
    }

    [Fact]
    public async Task Renaming_propagates_to_every_artefact()
    {
        var token = HttpExtensions.Unique("x");
        var dynasty = await _client.AddListEntryAsync("dynasties", $"Ilkahnide {token}");
        var artefact = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Bowl", DynastyId: dynasty.Id));

        var renamed = await (await _client.PutJsonAsync($"/api/lists/dynasties/{dynasty.Id}?lang=en", new ReferenceEntryNamesRequest(
            new Dictionary<string, string?> { ["it"] = $"Ilkhanide {token}", ["en"] = $"Ilkhanid {token}" }))).ReadAsync<ReferenceEntryDto>();

        Assert.Equal($"Ilkhanid {token}", renamed.Name);
        var read = await (await _client.GetAsync($"/api/artefacts/{artefact.Id}?lang=it")).ReadAsync<ArtefactDetail>();
        Assert.Equal($"Ilkhanide {token}", read.Dynasty!.Name);
    }

    [Fact]
    public async Task Renaming_to_another_entrys_name_is_a_validation_error()
    {
        var first = await _client.AddListEntryAsync("collection-groups", HttpExtensions.Unique("CINA"));
        var second = await _client.AddListEntryAsync("collection-groups", HttpExtensions.Unique("TURCHIA"));
        var location = await _client.AddListEntryAsync("storage-locations", HttpExtensions.Unique("Beta"));
        var other = await _client.AddListEntryAsync("storage-locations", HttpExtensions.Unique("Gamma"));

        var group = await _client.PutJsonAsync($"/api/lists/collection-groups/{second.Id}",
            new ReferenceEntryNamesRequest(new Dictionary<string, string?> { ["it"] = first.Name }));
        var single = await _client.PutJsonAsync($"/api/lists/storage-locations/{other.Id}", new ReferenceEntryNamesRequest(Name: location.Name));

        Assert.Equal(HttpStatusCode.BadRequest, group.StatusCode);
        Assert.Contains("names.it", (await group.ValidationErrorsAsync()).Keys);
        Assert.Equal(HttpStatusCode.BadRequest, single.StatusCode);
        Assert.Contains("name", (await single.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Entry_in_use_cannot_be_deleted_and_an_unused_one_can()
    {
        var used = await _client.AddListEntryAsync("collection-groups", HttpExtensions.Unique("CINA"));
        var unused = await _client.AddListEntryAsync("collection-groups", HttpExtensions.Unique("VARIE"));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Vase", CollectionGroupId: used.Id));

        var conflict = await _client.DeleteAsync($"/api/lists/collection-groups/{used.Id}");
        var deleted = await _client.DeleteAsync($"/api/lists/collection-groups/{unused.Id}");

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("entry_in_use", (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/lists/collection-groups/{unused.Id}")).StatusCode);
    }

    [Fact]
    public async Task An_entry_is_found_only_in_its_own_list_and_unknown_lists_are_not_found()
    {
        var location = await _client.AddListEntryAsync("storage-locations", HttpExtensions.Unique("Beta"));

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/lists/dynasties/{location.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/lists/colours")).StatusCode);
    }

    [Fact]
    public async Task Names_are_required()
    {
        var translated = await _client.PostJsonAsync("/api/lists/dynasties", new ReferenceEntryNamesRequest(new Dictionary<string, string?> { ["it"] = " " }));
        var single = await _client.PostJsonAsync("/api/lists/storage-locations", new ReferenceEntryNamesRequest(Name: "  "));

        Assert.Contains("names", (await translated.ValidationErrorsAsync()).Keys);
        Assert.Contains("name", (await single.ValidationErrorsAsync()).Keys);
    }
}

[Collection(ApiCollection.Name)]
public class PartyApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Organisation_is_added_and_an_existing_name_returns_it()
    {
        var name = HttpExtensions.Unique("Sotheby's");
        var created = await _client.PostJsonAsync("/api/parties", new PartyRequest(name, Domain.Parties.PartyKind.Organisation));
        var again = await _client.PostJsonAsync("/api/parties", new PartyRequest($"  {name.ToUpperInvariant()} "));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var party = await created.ReadAsync<PartyDto>();
        Assert.Equal((name, Domain.Parties.PartyKind.Organisation), (party.Name, party.Kind));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(party.Id, (await again.ReadAsync<PartyDto>()).Id);
    }

    [Fact]
    public async Task Usage_counts_and_kind_filter()
    {
        var christies = await _client.AddPartyAsync(HttpExtensions.Unique("Christie's"));
        for (var i = 0; i < 3; i++)
            await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Bought",
                Acquisition: new AcquisitionInput(AcquisitionMethod.Purchase, null, christies.Id, null)));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Held", HolderId: christies.Id));
        var search = Uri.EscapeDataString(christies.Name);

        var listed = Assert.Single(await (await _client.GetAsync($"/api/parties?search={search}")).ReadAsync<List<PartyDto>>());
        var people = await (await _client.GetAsync($"/api/parties?search={search}&kind=person")).ReadAsync<List<PartyDto>>();

        Assert.Equal((3, 1), (listed.SourceCount, listed.HolderCount));
        Assert.Empty(people);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/parties?kind=robot")).StatusCode);
    }

    [Fact]
    public async Task Rename_and_change_kind_and_find_artefacts_by_the_new_name()
    {
        var token = HttpExtensions.Unique("x");
        var party = await _client.AddPartyAsync($"Christies {token}", Domain.Parties.PartyKind.Person);
        var artefact = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Bought",
            Acquisition: new AcquisitionInput(AcquisitionMethod.Purchase, null, party.Id, null)));

        var updated = await (await _client.PutJsonAsync($"/api/parties/{party.Id}",
            new PartyRequest($"Christie's {token}", Domain.Parties.PartyKind.Organisation))).ReadAsync<PartyDto>();

        Assert.Equal(($"Christie's {token}", Domain.Parties.PartyKind.Organisation), (updated.Name, updated.Kind));
        var found = await (await _client.GetAsync($"/api/artefacts?search={Uri.EscapeDataString($"Christie's {token}")}")).ReadAsync<ArtefactListPage>();
        Assert.Equal(artefact.Id, Assert.Single(found.Items).Id);
    }

    [Fact]
    public async Task Renaming_to_another_partys_name_is_a_validation_error()
    {
        var first = await _client.AddPartyAsync(HttpExtensions.Unique("Bonhams"));
        var second = await _client.AddPartyAsync(HttpExtensions.Unique("Dorotheum"));

        var response = await _client.PutJsonAsync($"/api/parties/{second.Id}", new PartyRequest(first.Name));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("name", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Party_in_use_cannot_be_deleted()
    {
        var source = await _client.AddPartyAsync(HttpExtensions.Unique("Drouot"));
        var unused = await _client.AddPartyAsync(HttpExtensions.Unique("Nobody"));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Bought",
            Acquisition: new AcquisitionInput(AcquisitionMethod.Purchase, null, source.Id, null)));

        var conflict = await _client.DeleteAsync($"/api/parties/{source.Id}");

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("party_in_use", (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/parties/{unused.Id}")).StatusCode);
    }
}
