using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Measures;
using Artefacts.Infrastructure.Read;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class UnitOfMeasureApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly Guid Cm = SeededUnits.Centimetre.Value;
    private static readonly Guid Mm = SeededUnits.Millimetre.Value;
    private static readonly Guid Inch = SeededUnits.Inch.Value;

    private async Task<UnitOfMeasureDto> AddUnitAsync(string name, string abbreviation, decimal factor)
    {
        var response = await _client.PostJsonAsync("/api/units", new UnitOfMeasureRequest(TestData.En(name), abbreviation, factor));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<UnitOfMeasureDto>();
    }

    [Fact]
    public async Task Fresh_database_has_the_seeded_units_in_italian()
    {
        var units = await (await _client.GetAsync("/api/units?lang=it")).ReadAsync<List<UnitOfMeasureDto>>();

        var seeded = units.Where(u => SeededUnits.All.Any(s => s.Id.Value == u.Id)).ToList();
        Assert.Equal(["millimetro", "centimetro", "pollice", "metro"], seeded.Select(u => u.Name));
        Assert.Equal(["mm", "cm", "in", "m"], seeded.Select(u => u.Abbreviation));
        Assert.Equal([0.001m, 0.01m, 0.0254m, 1m], seeded.Select(u => u.Factor));
    }

    [Fact]
    public async Task Added_unit_is_listed_by_factor()
    {
        var foot = await AddUnitAsync(HttpExtensions.Unique("Foot"), HttpExtensions.Unique("ft")[..10], 0.3048m);

        var units = await (await _client.GetAsync("/api/units")).ReadAsync<List<UnitOfMeasureDto>>();

        var ids = units.Select(u => u.Id).ToList();
        Assert.True(ids.IndexOf(Cm) < ids.IndexOf(foot.Id));
        Assert.True(ids.IndexOf(foot.Id) < ids.IndexOf(SeededUnits.Metre.Value));
    }

    [Fact]
    public async Task Duplicate_abbreviation_ignoring_case_is_rejected()
    {
        var response = await _client.PostJsonAsync("/api/units", new UnitOfMeasureRequest(TestData.En(HttpExtensions.Unique("Centi")), " CM ", 0.01m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("abbreviation", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Factor_must_be_positive()
    {
        var response = await _client.PostJsonAsync("/api/units", new UnitOfMeasureRequest(TestData.En(HttpExtensions.Unique("Nothing")), HttpExtensions.Unique("n")[..10], 0m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("factor", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Unit_in_use_cannot_be_deleted_and_unused_unit_can()
    {
        var used = await AddUnitAsync(HttpExtensions.Unique("Span"), HttpExtensions.Unique("sp")[..10], 0.2286m);
        var unused = await AddUnitAsync(HttpExtensions.Unique("Cubit"), HttpExtensions.Unique("cu")[..10], 0.4572m);
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Rug", Height: new MeasurementInput(3, used.Id)));

        var conflict = await _client.DeleteAsync($"/api/units/{used.Id}");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var problem = await conflict.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unit_in_use", problem.GetProperty("code").GetString());
        Assert.Contains("1 artefact", problem.GetProperty("detail").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/units/{unused.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/units/{unused.Id}")).StatusCode);
    }

    [Fact]
    public async Task Text_and_measurements_are_kept_together()
    {
        var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Coppa",
            Dimensions: "diam 17,6 x h 7", Diameter: new MeasurementInput(17.6m, Cm), Height: new MeasurementInput(7, Cm)));

        var read = await (await _client.GetAsync($"/api/artefacts/{created.Id}?lang=en")).ReadAsync<ArtefactDetail>();

        Assert.Equal("diam 17,6 x h 7", read.Dimensions);
        Assert.Equal(17.6m, read.Diameter!.Value);
        Assert.Equal(0.176m, read.Diameter.InMetres);
        Assert.Equal("cm", read.Diameter.Unit.Abbreviation);
        Assert.Equal("Centimetre", read.Diameter.Unit.Name);
        Assert.Equal(0.07m, read.Height!.InMetres);
        Assert.Null(read.Width);
        Assert.Null(read.Length);
    }

    [Fact]
    public async Task Each_measurement_has_its_own_unit()
    {
        var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Panel",
            Height: new MeasurementInput(820, Mm), Width: new MeasurementInput(25, Inch)));

        Assert.Equal(0.82m, created.Height!.InMetres);
        Assert.Equal("mm", created.Height.Unit.Abbreviation);
        Assert.Equal(0.635m, created.Width!.InMetres);
        Assert.Equal("in", created.Width.Unit.Abbreviation);
    }

    [Fact]
    public async Task Measurement_without_unit_or_with_unknown_unit_is_rejected()
    {
        var noUnit = await _client.PostJsonAsync("/api/artefacts",
            new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Bowl", Height: new MeasurementInput(7, null)));
        Assert.Equal(HttpStatusCode.BadRequest, noUnit.StatusCode);
        Assert.Contains("height.unitId", (await noUnit.ValidationErrorsAsync()).Keys);

        var unknown = await _client.PostJsonAsync("/api/artefacts",
            new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Bowl", Width: new MeasurementInput(7, Guid.NewGuid())));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("width.unitId", (await unknown.ValidationErrorsAsync()).Keys);

        var zero = await _client.PostJsonAsync("/api/artefacts",
            new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Bowl", Length: new MeasurementInput(0, Cm)));
        Assert.Contains("length.value", (await zero.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Text_only_and_measurement_removed_on_update()
    {
        var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Painting",
            Dimensions: "82 × 64 cm (framed 96 × 78 cm)", Width: new MeasurementInput(64, Cm)));

        var updated = await (await _client.PutJsonAsync($"/api/artefacts/{created.Id}",
            new ArtefactWriteRequest(created.InventoryNumber, created.Title, Dimensions: created.Dimensions))).ReadAsync<ArtefactDetail>();

        Assert.Equal("82 × 64 cm (framed 96 × 78 cm)", updated.Dimensions);
        Assert.Null(updated.Width);
    }

    [Fact]
    public async Task Read_view_metres_follow_a_changed_factor()
    {
        var unit = await AddUnitAsync(HttpExtensions.Unique("Palm"), HttpExtensions.Unique("pa")[..10], 0.1m);
        var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Box", Height: new MeasurementInput(7, unit.Id)));

        async Task<ArtefactRow> RowAsync()
        {
            using var scope = factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ReadDbContext>().Artefacts.SingleAsync(a => a.Id == created.Id);
        }

        var before = await RowAsync();
        Assert.Equal(7m, before.HeightValue);
        Assert.Equal(unit.Id, before.HeightUnitId);
        Assert.Equal(0.7m, before.HeightMetres);

        (await _client.PutJsonAsync($"/api/units/{unit.Id}", new UnitOfMeasureRequest(unit.Names.ToDictionary(n => n.Key, n => (string?)n.Value), unit.Abbreviation, 0.075m)))
            .EnsureSuccessStatusCode();

        var after = await RowAsync();
        Assert.Equal(7m, after.HeightValue);
        Assert.Equal(0.525m, after.HeightMetres);
    }

    [Fact]
    public async Task GraphQL_measurement_carries_its_unit_in_the_readers_language()
    {
        var created = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Bowl", Height: new MeasurementInput(7, Cm)));
        const string query = "query($id: UUID!) { artefact(id: $id) { height { value inMetres unit { name abbreviation factor } } width { value } } }";

        var artefact = (await _client.QueryAsync(query, new { id = created.Id }, "it")).Data().GetProperty("artefact");

        var height = artefact.GetProperty("height");
        Assert.Equal(7m, height.GetProperty("value").GetDecimal());
        Assert.Equal(0.07m, height.GetProperty("inMetres").GetDecimal());
        Assert.Equal("centimetro", height.GetProperty("unit").GetProperty("name").GetString());
        Assert.Equal("cm", height.GetProperty("unit").GetProperty("abbreviation").GetString());
        Assert.Equal(0.01m, height.GetProperty("unit").GetProperty("factor").GetDecimal());
        Assert.Equal(JsonValueKind.Null, artefact.GetProperty("width").ValueKind);
    }
}
