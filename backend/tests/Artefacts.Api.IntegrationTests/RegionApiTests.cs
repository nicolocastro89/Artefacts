using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class RegionApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Search_matches_part_of_name_ignoring_case()
    {
        var name = HttpExtensions.Unique("Flanders");
        await _client.AddRegionAsync(name);

        var found = await (await _client.GetAsync($"/api/regions?search={name[..3].ToLowerInvariant()}{name[3..].ToUpperInvariant()}"))
            .ReadAsync<List<RegionDto>>();

        Assert.Contains(found, r => r.Name == name);
    }

    [Fact]
    public async Task Adding_new_region_creates_it()
    {
        var name = HttpExtensions.Unique("Tuscany");
        var response = await _client.PostJsonAsync("/api/regions", new AddRegionRequest(name));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(name, (await response.ReadAsync<RegionDto>()).Name);
    }

    [Fact]
    public async Task Adding_existing_name_returns_existing_region()
    {
        var name = HttpExtensions.Unique("Flanders");
        var existing = await _client.AddRegionAsync(name);

        var response = await _client.PostJsonAsync("/api/regions", new AddRegionRequest($"  {name.ToLowerInvariant()}  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(existing.Id, (await response.ReadAsync<RegionDto>()).Id);
        var all = await (await _client.GetAsync($"/api/regions?search={name}")).ReadAsync<List<RegionDto>>();
        Assert.Single(all);
    }

    [Fact]
    public async Task Rename_propagates_to_provenance()
    {
        var region = await _client.AddRegionAsync(HttpExtensions.Unique("Holland"));
        var artefact = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "T",
            Provenance: [new ProvenanceEntryInput(region.Id, new HistoricalDateInput("1700"), null)]));
        var newName = HttpExtensions.Unique("Netherlands");

        (await _client.PutJsonAsync($"/api/regions/{region.Id}", new RenameRegionRequest(newName))).EnsureSuccessStatusCode();

        var read = await (await _client.GetAsync($"/api/artefacts/{artefact.Id}")).ReadAsync<ArtefactDetail>();
        Assert.Equal(newName, read.Provenance.Single().RegionName);
    }

    [Fact]
    public async Task Rename_to_existing_name_is_rejected()
    {
        var a = await _client.AddRegionAsync(HttpExtensions.Unique("A"));
        var b = await _client.AddRegionAsync(HttpExtensions.Unique("B"));

        var response = await _client.PutJsonAsync($"/api/regions/{b.Id}", new RenameRegionRequest(a.Name));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("name", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Parent_cycle_is_rejected()
    {
        var belgium = await _client.AddRegionAsync(HttpExtensions.Unique("Belgium"));
        var flanders = await _client.AddRegionAsync(HttpExtensions.Unique("Flanders"), belgium.Id);
        Assert.Equal(belgium.Id, flanders.ParentId);
        Assert.Equal(belgium.Name, flanders.ParentName);

        var response = await _client.PutJsonAsync($"/api/regions/{belgium.Id}/parent", new SetRegionParentRequest(flanders.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("parentId", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Deleting_region_used_by_provenance_is_rejected()
    {
        var region = await _client.AddRegionAsync(HttpExtensions.Unique("Used"));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "T",
            Provenance: [new ProvenanceEntryInput(region.Id, new HistoricalDateInput("1700"), null)]));

        var response = await _client.DeleteAsync($"/api/regions/{region.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("region_in_use", problem.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/regions/{region.Id}")).StatusCode);
    }

    [Fact]
    public async Task Deleting_region_with_children_is_rejected()
    {
        var parent = await _client.AddRegionAsync(HttpExtensions.Unique("Parent"));
        await _client.AddRegionAsync(HttpExtensions.Unique("Child"), parent.Id);

        var response = await _client.DeleteAsync($"/api/regions/{parent.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Unused_region_can_be_deleted()
    {
        var region = await _client.AddRegionAsync(HttpExtensions.Unique("Unused"));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/regions/{region.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/regions/{region.Id}")).StatusCode);
    }
}
