using System.Net;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Application.Imports;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ImportDetectorApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static SaveImportDetectorRequest Pattern(string name, string pattern = @"^ass\.\s*(?<value>.+)$") =>
        new(name, new DetectorDefinition(DetectorKind.Regex, pattern, Example: "ass. Lloyd's 1999"));

    private async Task<ImportDetectorDto> CreateAsync(string name) =>
        await (await _client.PostJsonAsync("/api/imports/detectors", Pattern(name))).ReadAsync<ImportDetectorDto>();

    [Fact]
    public async Task Built_in_and_user_detectors_are_listed_together()
    {
        var name = HttpExtensions.Unique("Insurance");
        var created = await CreateAsync(name);

        var list = await (await _client.GetAsync("/api/imports/detectors")).ReadAsync<List<ImportDetectorDto>>();

        Assert.Contains(list, d => d.Id == DetectorIds.Money && d.IsSystem && d.Definition is null);
        var user = Assert.Single(list, d => d.Id == created.Id);
        Assert.False(user.IsSystem);
        Assert.Equal(name, user.Label);
        Assert.Equal("ass. Lloyd's 1999", user.Example);
        Assert.StartsWith("user:", created.Id);
    }

    [Fact]
    public async Task A_user_detector_is_changed_duplicated_and_deleted()
    {
        var created = await CreateAsync(HttpExtensions.Unique("Insurance"));

        var renamed = HttpExtensions.Unique("Policy");
        var updated = await (await _client.PutJsonAsync($"/api/imports/detectors/{created.Id}", Pattern(renamed, @"^pol\.\s*(?<value>.+)$"))).ReadAsync<ImportDetectorDto>();
        Assert.Equal(renamed, updated.Label);
        Assert.Equal(@"^pol\.\s*(?<value>.+)$", updated.Definition!.Pattern);

        var copyName = HttpExtensions.Unique("Copy");
        var copy = await (await _client.PostJsonAsync($"/api/imports/detectors/{created.Id}/duplicate", new DuplicateImportDetectorRequest(copyName)))
            .ReadAsync<ImportDetectorDto>();
        Assert.NotEqual(created.Id, copy.Id);
        Assert.Equal(updated.Definition.Pattern, copy.Definition!.Pattern);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/imports/detectors/{copy.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/imports/detectors/{copy.Id}")).StatusCode);
    }

    [Fact]
    public async Task An_invalid_pattern_is_reported_against_the_pattern_and_names_must_be_unique()
    {
        var invalid = await _client.PostJsonAsync("/api/imports/detectors", Pattern(HttpExtensions.Unique("Broken"), "(unclosed"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("definition.pattern", (await invalid.ValidationErrorsAsync()).Keys);

        var name = HttpExtensions.Unique("Insurance");
        await CreateAsync(name);
        var duplicate = await _client.PostJsonAsync("/api/imports/detectors", Pattern(name.ToUpperInvariant()));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("name", (await duplicate.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Built_in_detectors_cannot_be_changed_deleted_or_copied()
    {
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PutJsonAsync($"/api/imports/detectors/{DetectorIds.Money}", Pattern("Money"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"/api/imports/detectors/{DetectorIds.Money}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await _client.PostJsonAsync($"/api/imports/detectors/{DetectorIds.Money}/duplicate", new DuplicateImportDetectorRequest("Copy"))).StatusCode);
    }

    [Fact]
    public async Task A_detector_used_by_a_template_is_not_deleted_and_a_mapping_can_only_name_known_detectors()
    {
        var detector = await CreateAsync(HttpExtensions.Unique("Insurance"));
        var mapping = new ImportMapping(
            [new ColumnRule(1, "Note", Lines: [new LineRule(ImportField.AcquisitionNote, detector.Id)], Rest: ImportField.Description)],
            new InventoryNumberPattern("X-{row}"), "en");
        var templateName = HttpExtensions.Unique("Notes");

        var template = await (await _client.PostJsonAsync("/api/import-templates", new SaveImportTemplateRequest(templateName, null, mapping)))
            .ReadAsync<ImportTemplateDto>();

        var blocked = await _client.DeleteAsync($"/api/imports/detectors/{detector.Id}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains(templateName, await blocked.Content.ReadAsStringAsync());

        await _client.DeleteAsync($"/api/import-templates/{template.Id}");
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/imports/detectors/{detector.Id}")).StatusCode);

        var unknown = await _client.PostJsonAsync("/api/import-templates", new SaveImportTemplateRequest(HttpExtensions.Unique("Gone"), null, mapping));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Trying_a_definition_reports_each_line_without_saving_anything()
    {
        var request = new TryImportDetectorRequest(
            new DetectorDefinition(DetectorKind.Regex, @"^ass\.\s*(?<value>.+?)(?:,\s*(?<remainder>.+))?$"),
            ["ass. Lloyd's, 1999", "something else"]);

        var result = await (await _client.PostJsonAsync("/api/imports/detectors/try", request)).ReadAsync<TryImportDetectorResult>();

        Assert.Collection(result.Lines,
            first =>
            {
                Assert.True(first.Matched);
                Assert.Equal("Lloyd's", first.Text);
                Assert.Equal("1999", first.Remainder);
            },
            second => Assert.False(second.Matched));
    }

    [Fact]
    public async Task Trying_an_invalid_definition_is_a_validation_error()
    {
        var response = await _client.PostJsonAsync("/api/imports/detectors/try", new TryImportDetectorRequest(new DetectorDefinition(DetectorKind.Regex, "(unclosed"), ["x"]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("definition.pattern", (await response.ValidationErrorsAsync()).Keys);
    }
}
