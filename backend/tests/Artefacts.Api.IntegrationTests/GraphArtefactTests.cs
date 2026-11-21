using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Parties;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class GraphArtefactTests(ApiFactory factory)
{
    private const string ByIdQuery = """
        query($id: UUID!) {
          language
          artefact(id: $id) {
            title
            creationDate { display earliestYear latestYear precision }
            valuation { amount currency }
            acquisition { method date lot source { name kind } price { amount currency } }
            objectType { name }
            dynasty { name }
            placeOfProduction { text region { name } }
            holder { name kind }
            primaryImage { urls { thumb } }
          }
        }
        """;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Artefact_exposes_its_fields_and_linked_records()
    {
        var region = await _client.AddRegionAsync("Kashan");
        var objectType = await _client.AddObjectTypeAsync("Bowl");
        var dynasty = await _client.AddListEntryAsync("dynasties", new Dictionary<string, string?> { ["en"] = "Seljuk" });
        var source = await _client.AddPartyAsync(HttpExtensions.Unique("Bonhams"), PartyKind.Organisation);
        var holder = await _client.AddPartyAsync(HttpExtensions.Unique("Lender"), PartyKind.Person);
        var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Lustre bowl",
            CreationDate: new HistoricalDateInput("c. 1200", 1190, 1210),
            Acquisition: new AcquisitionInput(AcquisitionMethod.Purchase, new DateOnly(1999, 5, 4), source.Id, new MoneyDto(1250.5m, "EUR"), "Lot 7"),
            ObjectTypeId: objectType.Id, DynastyId: dynasty.Id, HolderId: holder.Id,
            PlaceOfProduction: new PlaceOfProductionInput("Kashan, Iran", region.Id),
            Valuation: new MoneyDto(9000m, "EUR")));

        var artefact = (await _client.QueryAsync(ByIdQuery, new { id = created.Id }, "en")).Data().GetProperty("artefact");

        Assert.Equal("Lustre bowl", artefact.GetProperty("title").GetString());
        var date = artefact.GetProperty("creationDate");
        Assert.Equal("c. 1200", date.GetProperty("display").GetString());
        Assert.Equal(1190, date.GetProperty("earliestYear").GetInt32());
        Assert.Equal(1210, date.GetProperty("latestYear").GetInt32());
        Assert.Equal(9000m, artefact.GetProperty("valuation").GetProperty("amount").GetDecimal());
        Assert.Equal("EUR", artefact.GetProperty("valuation").GetProperty("currency").GetString());
        var acquisition = artefact.GetProperty("acquisition");
        Assert.Equal("PURCHASE", acquisition.GetProperty("method").GetString());
        Assert.Equal("1999-05-04", acquisition.GetProperty("date").GetString());
        Assert.Equal("Lot 7", acquisition.GetProperty("lot").GetString());
        Assert.Equal(source.Name, acquisition.GetProperty("source").GetProperty("name").GetString());
        Assert.Equal("ORGANISATION", acquisition.GetProperty("source").GetProperty("kind").GetString());
        Assert.Equal(1250.5m, acquisition.GetProperty("price").GetProperty("amount").GetDecimal());
        Assert.Equal("Bowl", artefact.GetProperty("objectType").GetProperty("name").GetString());
        Assert.Equal("Seljuk", artefact.GetProperty("dynasty").GetProperty("name").GetString());
        Assert.Equal("Kashan, Iran", artefact.GetProperty("placeOfProduction").GetProperty("text").GetString());
        Assert.Equal("Kashan", artefact.GetProperty("placeOfProduction").GetProperty("region").GetProperty("name").GetString());
        Assert.Equal(holder.Name, artefact.GetProperty("holder").GetProperty("name").GetString());
        Assert.Equal("PERSON", artefact.GetProperty("holder").GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, artefact.GetProperty("primaryImage").ValueKind);
    }

    [Fact]
    public async Task Names_follow_the_readers_language()
    {
        var dynasty = await _client.AddListEntryAsync("dynasties", new Dictionary<string, string?> { ["en"] = "Safavid", ["it"] = "Safavide" });
        var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Tile", DynastyId: dynasty.Id));
        const string query = "query($id: UUID!) { language artefact(id: $id) { dynasty { name } } }";

        var english = (await _client.QueryAsync(query, new { id = created.Id }, "en")).Data();
        var italian = (await _client.QueryAsync(query, new { id = created.Id }, "it")).Data();

        Assert.Equal("en", english.GetProperty("language").GetString());
        Assert.Equal("Safavid", english.GetProperty("artefact").GetProperty("dynasty").GetProperty("name").GetString());
        Assert.Equal("it", italian.GetProperty("language").GetString());
        Assert.Equal("Safavide", italian.GetProperty("artefact").GetProperty("dynasty").GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_name_missing_in_the_readers_language_falls_back()
    {
        var objectType = await _client.AddObjectTypeAsync(new Dictionary<string, string?> { ["it"] = "Ciotola" });
        var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Bowl", ObjectTypeId: objectType.Id));

        var data = (await _client.QueryAsync("query($id: UUID!) { artefact(id: $id) { objectType { name } } }", new { id = created.Id }, "en")).Data();

        Assert.Equal("Ciotola", data.GetProperty("artefact").GetProperty("objectType").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Artefact_held_by_the_collection_has_no_holder()
    {
        var created = await _client.CreateArtefactAsync("Plate");

        var data = (await _client.QueryAsync("query($id: UUID!) { artefact(id: $id) { holder { name } } }", new { id = created.Id })).Data();

        Assert.Equal(JsonValueKind.Null, data.GetProperty("artefact").GetProperty("holder").ValueKind);
    }
}
