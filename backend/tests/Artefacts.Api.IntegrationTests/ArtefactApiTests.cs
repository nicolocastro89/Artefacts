using System.Net;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Dating;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ArtefactApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_endpoint_responds()
    {
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Valid_artefact_is_created()
    {
        var inv = HttpExtensions.Unique("INV-2026");
        var response = await _client.PostJsonAsync("/api/artefacts", new ArtefactWriteRequest(inv, "Still Life with Lemons"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<ArtefactDetail>();
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(inv, created.InventoryNumber);
        Assert.Equal($"/api/artefacts/{created.Id}", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Duplicate_inventory_number_is_rejected()
    {
        var inv = HttpExtensions.Unique("DUP");
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(inv, "First"));

        var response = await _client.PostJsonAsync("/api/artefacts", new ArtefactWriteRequest(inv, "Second"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("inventoryNumber", (await response.ValidationErrorsAsync()).Keys);
        var list = await _client.ListArtefactsAsync(search: inv);
        Assert.Equal(1, list.Total);
    }

    [Fact]
    public async Task Missing_title_is_rejected()
    {
        var response = await _client.PostJsonAsync("/api/artefacts", new ArtefactWriteRequest(HttpExtensions.Unique("INV"), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("title", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Title_change_is_persisted()
    {
        var created = await _client.CreateArtefactAsync("Old title");

        var response = await _client.PutJsonAsync($"/api/artefacts/{created.Id}",
            new ArtefactWriteRequest(created.InventoryNumber, "New title"));
        response.EnsureSuccessStatusCode();

        var read = await (await _client.GetAsync($"/api/artefacts/{created.Id}")).ReadAsync<ArtefactDetail>();
        Assert.Equal("New title", read.Title);
    }

    [Fact]
    public async Task Updating_unknown_artefact_is_not_found()
    {
        var response = await _client.PutJsonAsync($"/api/artefacts/{Guid.NewGuid()}", new ArtefactWriteRequest("X", "Y"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Getting_unknown_artefact_is_not_found()
    {
        var response = await _client.GetAsync($"/api/artefacts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Purchase_with_price_is_recorded()
    {
        var galerie = await _client.AddPartyAsync(HttpExtensions.Unique("Galerie Example"));
        var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(
            HttpExtensions.Unique("INV"), "Purchased",
            Acquisition: new AcquisitionInput(AcquisitionMethod.Purchase, new DateOnly(2019, 5, 2), galerie.Id,
                new MoneyDto(12500.00m, "EUR"))));

        Assert.NotNull(created.Acquisition);
        Assert.Equal(AcquisitionMethod.Purchase, created.Acquisition.Method);
        Assert.Equal(new DateOnly(2019, 5, 2), created.Acquisition.Date);
        Assert.Equal(galerie.Name, created.Acquisition.Source!.Name);
        Assert.Equal(12500.00m, created.Acquisition.Price!.Amount);
        Assert.Equal("EUR", created.Acquisition.Price.Currency);
    }

    [Fact]
    public async Task Invalid_currency_is_rejected()
    {
        var response = await _client.PostJsonAsync("/api/artefacts", new ArtefactWriteRequest(
            HttpExtensions.Unique("INV"), "Bad currency",
            Acquisition: new AcquisitionInput(AcquisitionMethod.Purchase, null, null, new MoneyDto(10m, "XYZ"))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("acquisition.price.currency", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Creation_date_is_interpreted()
    {
        var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(
            HttpExtensions.Unique("INV"), "Dated", CreationDate: new HistoricalDateInput("late 17th century")));

        Assert.Equal("late 17th century", created.CreationDate!.Display);
        Assert.Equal(1667, created.CreationDate.EarliestYear);
        Assert.Equal(1700, created.CreationDate.LatestYear);
    }

    [Fact]
    public async Task Invalid_creation_date_is_reported_on_its_field()
    {
        var response = await _client.PostJsonAsync("/api/artefacts", new ArtefactWriteRequest(
            HttpExtensions.Unique("INV"), "Bad date", CreationDate: new HistoricalDateInput("unknown era")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("creationDate", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Provenance_is_returned_in_order_with_region_names()
    {
        var flanders = await _client.AddRegionAsync(HttpExtensions.Unique("Flanders"));
        var paris = await _client.AddRegionAsync(HttpExtensions.Unique("Paris"));

        var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(
            HttpExtensions.Unique("INV"), "With provenance",
            Provenance:
            [
                new ProvenanceEntryInput(flanders.Id, new HistoricalDateInput("c. 1650"), null),
                new ProvenanceEntryInput(paris.Id, new HistoricalDateInput("1780s"), "Sold"),
            ]));

        var read = await (await _client.GetAsync($"/api/artefacts/{created.Id}")).ReadAsync<ArtefactDetail>();
        Assert.Collection(read.Provenance,
            p => { Assert.Equal(flanders.Name, p.RegionName); Assert.Equal("c. 1650", p.Period.Display); },
            p => { Assert.Equal(paris.Name, p.RegionName); Assert.Equal("1780s", p.Period.Display); Assert.Equal("Sold", p.Note); });
    }

    [Fact]
    public async Task Unknown_region_in_provenance_is_rejected()
    {
        var response = await _client.PostJsonAsync("/api/artefacts", new ArtefactWriteRequest(
            HttpExtensions.Unique("INV"), "Bad provenance",
            Provenance: [new ProvenanceEntryInput(Guid.NewGuid(), new HistoricalDateInput("1700"), null)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("provenance[0].regionId", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Parse_endpoint_interprets_date()
    {
        var response = await _client.PostJsonAsync("/api/historical-dates/parse", new HistoricalDateInput("late 17th century"));

        response.EnsureSuccessStatusCode();
        var parsed = await response.ReadAsync<HistoricalDateDto>();
        Assert.Equal(1667, parsed.EarliestYear);
        Assert.Equal(1700, parsed.LatestYear);
        Assert.Equal(HistoricalDatePrecision.Century, parsed.Precision);
    }

    [Fact]
    public async Task Parse_endpoint_rejects_uninterpretable_text()
    {
        var response = await _client.PostJsonAsync("/api/historical-dates/parse", new HistoricalDateInput("unknown era"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
