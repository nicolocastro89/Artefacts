using System.Net;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Application.Imports;
using Artefacts.Domain.Imports;
using Artefacts.Domain.Measures;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ImportDimensionsApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly Guid Cm = SeededUnits.Centimetre.Value;

    private async Task<Guid> ObjectTypeAsync() => (await _client.AddObjectTypeAsync(HttpExtensions.Unique("Ceramics"))).Id;

    private async Task<List<ImportDimensionsDto>> DimensionsAsync(Guid id) =>
        await (await _client.GetAsync($"/api/imports/{id}/dimensions")).ReadAsync<List<ImportDimensionsDto>>();

    private async Task<List<ImportDimensionsDto>> SetDimensionsAsync(Guid id, params DimensionResolution[] resolutions) =>
        await (await _client.PutJsonAsync($"/api/imports/{id}/dimensions", new SetDimensionResolutionsRequest(resolutions))).ReadAsync<List<ImportDimensionsDto>>();

    [Fact]
    public async Task Unclear_texts_are_listed_and_resolved_by_measurements_or_kept_as_text()
    {
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(
            ("01. Scatola\nchiuso 12 x 8; aperto 24 x 8", "Cina\nXV", null),
            ("02. Scatola\nchiuso 12 x 8; aperto 24 x 8", "Cina\nXV", null),
            ("03. Coppa\ndiam 17,6 x h 7", "Cina\nXV", null)).Build(), await ObjectTypeAsync());
        await _client.UseUniqueNumbersAsync(job);

        var unclear = Assert.Single(await DimensionsAsync(job.Id));
        Assert.Equal([3, 4], unclear.SheetRows);
        Assert.Null(unclear.Resolution);
        var held = await _client.ReviewAsync(job.Id);
        Assert.Equal((1, 2), (held.Counts.Ready, held.Counts.Problems));
        Assert.Contains(held.Rows.Single(r => r.SheetRow == 3).Problems, p => p.Field == "dimensions" && p.Text == unclear.Text);
        // A clear text is read without a decision, and the review shows it next to the text.
        Assert.Contains(held.Rows.Single(r => r.SheetRow == 5).Fields, f => f.Field == ImportField.Dimensions && f.Value == "diam 17,6 x h 7 → H 7 cm · Ø 17.6 cm");

        var resolved = await SetDimensionsAsync(job.Id,
            new DimensionResolution(unclear.Text, Height: new MeasurementInput(12, Cm), Width: new MeasurementInput(8, Cm)));

        Assert.Equal(12m, Assert.Single(resolved).Resolution!.Height!.Value);
        var review = await _client.ReviewAsync(job.Id);
        Assert.Equal(3, review.Counts.Ready);
        Assert.Contains(review.Rows.Single(r => r.SheetRow == 3).Fields, f => f.Value == "chiuso 12 x 8; aperto 24 x 8 → H 12 cm · W 8 cm");

        await SetDimensionsAsync(job.Id, new DimensionResolution(unclear.Text, KeepTextOnly: true));
        Assert.Contains((await _client.ReviewAsync(job.Id)).Rows.Single(r => r.SheetRow == 3).Fields, f => f.Value.EndsWith("(text only)"));
    }

    [Fact]
    public async Task Resolutions_are_validated()
    {
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(("01. Scatola\nmisure varie", "Cina\nXV", null)).Build(), await ObjectTypeAsync());

        var empty = await _client.PutJsonAsync($"/api/imports/{job.Id}/dimensions", new SetDimensionResolutionsRequest([new DimensionResolution("misure varie")]));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var badUnit = await _client.PutJsonAsync($"/api/imports/{job.Id}/dimensions", new SetDimensionResolutionsRequest(
            [new DimensionResolution("misure varie", Height: new MeasurementInput(3, Guid.NewGuid()))]));
        Assert.Contains("dimensions[0].height.unitId", (await badUnit.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Mapping_rejects_a_continuing_discard_rule_and_an_unknown_default_unit()
    {
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(("01. Coppa", "Cina\nXV", null)).Build(), await ObjectTypeAsync());
        var title = job.Mapping!.Columns.Single(c => c.Column == 2);
        var mapping = job.Mapping with
        {
            Columns = [.. job.Mapping.Columns.Where(c => c != title), title with { Lines = [new LineRule(null, Position: 1, Continues: true), .. title.Lines!] }],
            Dimensions = new DimensionsOptions(Guid.NewGuid()),
        };

        var response = await _client.PutJsonAsync($"/api/imports/{job.Id}/mapping", mapping);

        var errors = await response.ValidationErrorsAsync();
        Assert.Contains(errors.Keys, k => k.EndsWith(".continues"));
        Assert.Contains("mapping.dimensions.defaultUnitId", errors.Keys);
    }

    [Fact]
    public async Task Discarded_line_and_measurements_from_workbook_to_artefact()
    {
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(
            ("01. Coppa Celadon\ndiam 17,6 x h 7\nvedi foto", "Cina\nXV", null),
            ("02. Dipinto\n82 x 64 cm", "Cina\nXV", null)).Build(), await ObjectTypeAsync());
        await _client.UseUniqueNumbersAsync(job, m =>
        {
            var title = m.Columns.Single(c => c.Column == 2);
            return m with
            {
                Columns = [.. m.Columns.Where(c => c != title), title with { Lines = [new LineRule(null, Position: 2), .. title.Lines!] }],
                Dimensions = new DimensionsOptions(Cm, [MeasureKind.Height, MeasureKind.Width]),
            };
        });

        var review = await _client.ReviewAsync(job.Id);
        Assert.Equal(2, review.Counts.Ready);
        Assert.Contains("vedi foto", Assert.Single(review.Rows.Single(r => r.SheetRow == 3).Discarded!));
        Assert.Empty(await DimensionsAsync(job.Id));

        await _client.CommitAndWaitAsync(job.Id);
        var report = await (await _client.GetAsync($"/api/imports/{job.Id}/report")).ReadAsync<ImportReportDto>();
        var coppa = await (await _client.GetAsync($"/api/artefacts/{report.Rows.Single(r => r.SheetRow == 3).ArtefactId}")).ReadAsync<ArtefactDetail>();
        Assert.Equal("diam 17,6 x h 7", coppa.Dimensions);
        Assert.Equal(17.6m, coppa.Diameter!.Value);
        Assert.Equal(7m, coppa.Height!.Value);
        Assert.Equal("cm", coppa.Height.Unit.Abbreviation);
        Assert.Null(coppa.Description);
        var dipinto = await (await _client.GetAsync($"/api/artefacts/{report.Rows.Single(r => r.SheetRow == 4).ArtefactId}")).ReadAsync<ArtefactDetail>();
        Assert.Equal((82m, 64m), (dipinto.Height!.Value, dipinto.Width!.Value));
    }

    [Fact]
    public async Task Template_carries_dimensions_decisions_and_options_to_the_next_import()
    {
        // Recognised as a dimensions line but unclear ("chiuso" is not a label); the number keeps the text unique to the test.
        var text = $"chiuso {Random.Shared.Next(100, 99_999)} x 8";
        var type = await ObjectTypeAsync();
        var seed = await _client.StartImportAsync(ImportTestData.Ceramics(("01. Coppa", "Cina\nXV", null)).Build(), type);
        await _client.UseUniqueNumbersAsync(seed, m => m with { Dimensions = new DimensionsOptions(Cm, [MeasureKind.Height]) });
        var template = await (await _client.PostJsonAsync("/api/import-templates",
            new SaveImportTemplateRequest(HttpExtensions.Unique("Template"), seed.Id))).ReadAsync<ImportTemplateDto>();

        var first = await _client.StartImportAsync(ImportTestData.Ceramics(($"01. Scatola\n{text}", "Cina\nXV", null)).Build(), type, templateId: template.Id);
        await _client.UseUniqueNumbersAsync(first);
        Assert.Single(await DimensionsAsync(first.Id));
        await SetDimensionsAsync(first.Id, new DimensionResolution(text, Length: new MeasurementInput(12, Cm)));
        Assert.Equal(1, (await _client.CommitAndWaitAsync(first.Id)).ImportedRows);

        var second = await _client.StartImportAsync(ImportTestData.Ceramics(($"01. Vaso\n{text}", "Cina\nXV", null)).Build(), type, "tessuti.xlsx", template.Id);

        Assert.Equal([MeasureKind.Height], second.Mapping!.Dimensions!.UnlabelledOrder);
        var decided = Assert.Single(await DimensionsAsync(second.Id));
        Assert.Equal(12m, decided.Resolution!.Length!.Value);
    }
}
