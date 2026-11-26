using Artefacts.Application.Imports;

namespace Artefacts.Application.Tests;

public class ImportDetectorRegistryTests
{
    /// <summary>The identifiers mappings and templates stored when detectors were an enum: renaming one orphans them.</summary>
    [Fact]
    public void Built_in_detectors_keep_their_stored_identifiers() =>
        Assert.Equal(
            ["itemTitle", "groupNumber", "dimensions", "historicalDate", "calendarDate", "lot", "money", "catalogueNumber", "locationCode"],
            DetectorRegistry.BuiltIn.Ids);

    [Fact]
    public void An_unknown_detector_recognises_nothing()
    {
        Assert.False(DetectorRegistry.BuiltIn.Contains("nope"));
        Assert.Null(DetectorRegistry.BuiltIn.Detect("nope", "lot 12"));
    }

    [Fact]
    public void Location_codes_use_the_patterns_of_the_context()
    {
        var registry = DetectorRegistry.BuiltIn;

        Assert.Null(registry.Detect(DetectorIds.LocationCode, "Sala 4"));
        Assert.NotNull(registry.Detect(DetectorIds.LocationCode, "Sala 4", new DetectionContext([@"^Sala\s*\d+$"])));
    }

    private sealed class InsuranceDetector : ILineDetector
    {
        public string Id => "insurance";

        public Detection? Detect(string line, DetectionContext context) =>
            line.StartsWith("ass.", StringComparison.OrdinalIgnoreCase) ? new Detection(line[4..].Trim()) : null;
    }

    /// <summary>A detector the application does not ship is used by a line rule like any other.</summary>
    [Fact]
    public void A_detector_added_to_the_registry_is_used_by_line_rules()
    {
        var registry = new DetectorRegistry([.. DetectorRegistry.BuiltIn.Detectors, new InsuranceDetector()]);
        var mapping = new ImportMapping(
            [new ColumnRule(1, "Note", Lines: [new LineRule(ImportField.AcquisitionNote, "insurance")], Rest: ImportField.Description)],
            new InventoryNumberPattern("X-{row}"));
        var headings = new Dictionary<int, string> { [1] = "Note" };

        var mapped = MappingEngine.Map(new Dictionary<int, string> { [1] = "ass. Lloyd's 1999\nuna coppa" }, mapping, headings, registry);

        Assert.Equal("Lloyd's 1999", mapped.First(ImportField.AcquisitionNote)!.Text);
        Assert.Equal("una coppa", mapped.First(ImportField.Description)!.Text);
    }
}
