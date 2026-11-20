using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ObjectTypeApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Search_matches_part_of_name_ignoring_case()
    {
        var name = HttpExtensions.Unique("Ceramics");
        await _client.AddObjectTypeAsync(name);

        var found = await (await _client.GetAsync($"/api/object-types?search={name.ToUpperInvariant()[..6]}{name[6..]}"))
            .ReadAsync<List<ObjectTypeDto>>();

        Assert.Contains(found, t => t.Name == name);
    }

    [Fact]
    public async Task Adding_existing_name_returns_existing_object_type()
    {
        var name = HttpExtensions.Unique("Carpets");
        var existing = await _client.AddObjectTypeAsync(name);

        var response = await _client.PostJsonAsync("/api/object-types", new AddObjectTypeRequest(TestData.En($"  {name.ToLowerInvariant()} ")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(existing.Id, (await response.ReadAsync<ObjectTypeDto>()).Id);
    }

    [Fact]
    public async Task Lists_usage_count_and_rename_propagates()
    {
        var type = await _client.AddObjectTypeAsync(HttpExtensions.Unique("Tappeti"));
        var artefact = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Rug", ObjectTypeId: type.Id));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Rug 2", ObjectTypeId: type.Id));

        var listed = Assert.Single(await (await _client.GetAsync($"/api/object-types?search={type.Name}")).ReadAsync<List<ObjectTypeDto>>());
        Assert.Equal(2, listed.ArtefactCount);

        var newName = HttpExtensions.Unique("Carpets");
        (await _client.PutJsonAsync($"/api/object-types/{type.Id}", new SetNamesRequest(TestData.En(newName)))).EnsureSuccessStatusCode();

        var read = await (await _client.GetAsync($"/api/artefacts/{artefact.Id}")).ReadAsync<ArtefactDetail>();
        Assert.Equal(newName, read.ObjectType!.Name);
    }

    [Fact]
    public async Task Rename_to_existing_name_is_rejected()
    {
        var a = await _client.AddObjectTypeAsync(HttpExtensions.Unique("Metalwork"));
        var b = await _client.AddObjectTypeAsync(HttpExtensions.Unique("Textiles"));

        var response = await _client.PutJsonAsync($"/api/object-types/{b.Id}", new SetNamesRequest(TestData.En(a.Name.ToUpperInvariant())));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task In_use_object_type_cannot_be_deleted()
    {
        var type = await _client.AddObjectTypeAsync(HttpExtensions.Unique("Miniatures"));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Page", ObjectTypeId: type.Id));

        var response = await _client.DeleteAsync($"/api/object-types/{type.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("object_type_in_use", problem.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/object-types/{type.Id}")).StatusCode);
    }

    [Fact]
    public async Task Unused_object_type_is_deleted()
    {
        var type = await _client.AddObjectTypeAsync(HttpExtensions.Unique("Parchments"));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/object-types/{type.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/object-types/{type.Id}")).StatusCode);
    }
}
