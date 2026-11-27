using Artefacts.Application.Imports;

namespace Artefacts.Application.Tests;

/// <summary>A built-in detector that offers a definition must recognise what the code detector does, so a copy starts out behaving the same.</summary>
public class ImportBuiltInDefinitionTests
{
    public static TheoryData<string, string> Samples => new()
    {
        { DetectorIds.GroupNumber, "01." },
        { DetectorIds.GroupNumber, " 7 " },
        { DetectorIds.GroupNumber, "123" },
        { DetectorIds.GroupNumber, "1234" },
        { DetectorIds.GroupNumber, "01. Coppa" },
        { DetectorIds.Lot, "lot 170" },
        { DetectorIds.Lot, "Lotto n. 42b" },
        { DetectorIds.Lot, "Sotheby's lot 202" },
        { DetectorIds.Lot, "lot 12 estimate" },
        { DetectorIds.Lot, "plot 5" },
        { DetectorIds.Lot, "no number here" },
        { DetectorIds.CatalogueNumber, "W.389" },
        { DetectorIds.CatalogueNumber, " AB.12a " },
        { DetectorIds.CatalogueNumber, "w.389" },
        { DetectorIds.CatalogueNumber, "W.123456" },
        { DetectorIds.CatalogueNumber, "W.389 extra" },
        { DetectorIds.CatalogueNumber, "WXYZ.1" },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void The_definition_recognises_what_the_code_detector_recognises(string id, string line)
    {
        var builtIn = DetectorRegistry.BuiltIn.Detectors.Single(d => d.Id == id);
        var copy = new UserDetector("user:copy", "Copy", builtIn.Definition!);

        var expected = builtIn.Detect(line, DetectionContext.Default);
        var actual = copy.Detect(line, DetectionContext.Default);

        Assert.Equal(expected?.Text, actual?.Text);
        Assert.Equal(expected?.Remainder, actual?.Remainder);
    }

    [Fact]
    public void A_spaced_catalogue_number_keeps_its_space_in_a_copy()
    {
        // The code detector writes "W. 389" as "W.389"; a pattern cannot rewrite the text it recognises.
        var builtIn = DetectorRegistry.BuiltIn.Detectors.Single(d => d.Id == DetectorIds.CatalogueNumber);
        var copy = new UserDetector("user:copy", "Copy", builtIn.Definition!);

        Assert.Equal("W.389", builtIn.Detect("W. 389", DetectionContext.Default)!.Text);
        Assert.Equal("W. 389", copy.Detect("W. 389", DetectionContext.Default)!.Text);
    }

    [Fact]
    public void Every_definition_is_valid_and_recognises_its_own_example()
    {
        foreach (var detector in DetectorRegistry.BuiltIn.Detectors.Where(d => d.Definition is not null))
        {
            Assert.Empty(detector.Definition!.Problems());
            Assert.NotNull(new UserDetector("user:copy", "Copy", detector.Definition).Detect(detector.Example!, DetectionContext.Default));
        }
    }
}
