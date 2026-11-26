using System.Text.Json;
using Artefacts.Application.Contracts;
using Artefacts.Application.Imports;
using Artefacts.Domain.Imports;
using Artefacts.Domain.Measures;

namespace Artefacts.Application.Tests;

public class DimensionsParserTests
{
    public static readonly Guid Mm = SeededUnits.Millimetre.Value, Cm = SeededUnits.Centimetre.Value, M = SeededUnits.Metre.Value, In = SeededUnits.Inch.Value;

    public static readonly IReadOnlyList<ParserUnit> Units =
    [
        new(Mm, "mm", ["Millimetre", "millimetro"]),
        new(Cm, "cm", ["Centimetre", "centimetro"]),
        new(M, "m", ["Metre", "metro"]),
        new(In, "in", ["Inch", "pollice"]),
    ];

    private static readonly DimensionsOptions DefaultCm = new(Cm);
    private static readonly DimensionsOptions HeightByWidth = new(Cm, [MeasureKind.Height, MeasureKind.Width]);

    private static ParsedDimensions? Parse(string text, DimensionsOptions? options = null) => DimensionsParser.Parse(text, Units, options);

    [Fact]
    public void Labelled_values_with_a_written_unit()
    {
        var parsed = Parse("diam 17,6 x h 7 cm")!;

        Assert.Equal(new MeasurementInput(17.6m, Cm), parsed.Of(MeasureKind.Diameter));
        Assert.Equal(new MeasurementInput(7m, Cm), parsed.Of(MeasureKind.Height));
        Assert.Equal(2, parsed.Measurements.Count);
    }

    [Fact]
    public void Default_unit_for_numbers_without_one()
    {
        var parsed = Parse("h 30 x l 45", DefaultCm)!;

        Assert.Equal(new MeasurementInput(30m, Cm), parsed.Of(MeasureKind.Height));
        Assert.Equal(new MeasurementInput(45m, Cm), parsed.Of(MeasureKind.Length));
    }

    [Fact]
    public void Unlabelled_values_follow_the_mapping_order()
    {
        var parsed = Parse("82 x 64 cm", HeightByWidth)!;

        Assert.Equal(new MeasurementInput(82m, Cm), parsed.Of(MeasureKind.Height));
        Assert.Equal(new MeasurementInput(64m, Cm), parsed.Of(MeasureKind.Width));
    }

    [Theory]
    [InlineData("chiuso 12 x 8; aperto 24 x 8")]
    [InlineData("82 x 64 cm")]
    [InlineData("h 7 x h 9")]
    [InlineData("h 7 x 9")]
    [InlineData("misure varie")]
    [InlineData("h 0")]
    [InlineData("h 7 x l")]
    public void Unclear_texts(string text) => Assert.Null(Parse(text, DefaultCm));

    [Fact]
    public void Missing_unit_without_default_is_unclear() => Assert.Null(Parse("diam 17,6 x h 7"));

    [Theory]
    [InlineData("Ø 17,6", MeasureKind.Diameter, 17.6)]
    [InlineData("ø17.6", MeasureKind.Diameter, 17.6)]
    [InlineData("diametro 30", MeasureKind.Diameter, 30)]
    [InlineData("alt. 12", MeasureKind.Height, 12)]
    [InlineData("altezza 12", MeasureKind.Height, 12)]
    [InlineData("lungh. 20", MeasureKind.Length, 20)]
    [InlineData("larg. 15", MeasureKind.Width, 15)]
    [InlineData("H 7", MeasureKind.Height, 7)]
    public void Labels_in_italian_and_english(string text, MeasureKind kind, double value) =>
        Assert.Equal(new MeasurementInput((decimal)value, Cm), Parse(text, DefaultCm)!.Of(kind));

    [Theory]
    [InlineData("h 25 in", 25)]
    [InlineData("h 25\"", 25)]
    [InlineData("h 25 inches", 25)]
    [InlineData("h 25 pollici", 25)]
    public void Inch_spellings(string text, double value) =>
        Assert.Equal(new MeasurementInput((decimal)value, In), Parse(text, DefaultCm)!.Of(MeasureKind.Height));

    [Fact]
    public void Each_number_takes_the_unit_written_after_it()
    {
        var parsed = Parse("h 820 mm x w 25 in")!;

        Assert.Equal(new MeasurementInput(820m, Mm), parsed.Of(MeasureKind.Height));
        Assert.Equal(new MeasurementInput(25m, In), parsed.Of(MeasureKind.Width));
    }

    [Fact]
    public void Unit_written_first_applies_to_the_numbers_after_it()
    {
        var parsed = Parse("cm 30 x 40", HeightByWidth with { DefaultUnitId = null })!;

        Assert.Equal(new MeasurementInput(30m, Cm), parsed.Of(MeasureKind.Height));
        Assert.Equal(new MeasurementInput(40m, Cm), parsed.Of(MeasureKind.Width));
    }

    [Fact]
    public void Unit_by_full_name() =>
        Assert.Equal(new MeasurementInput(2m, M), Parse("l 2 metro")!.Of(MeasureKind.Length));
}

public class DiscardRuleTests
{
    private static readonly Dictionary<int, string> Headings = new() { [1] = "Nome", [2] = "Provenienza" };

    private static MappedRecord Map(ColumnRule rule, string cell) =>
        MappingEngine.Map(new Dictionary<int, string> { [rule.Column] = cell }, new ImportMapping([rule], new InventoryNumberPattern("X-{row}")), Headings);

    [Fact]
    public void Line_discarded_by_position()
    {
        var rule = MappingSuggestion.Rules.Title(1, "Nome") with
        {
            Lines = [new(null, Position: 2), .. MappingSuggestion.Rules.Title(1, "Nome").Lines!],
        };

        var mapped = Map(rule, "01. Coppa Celadon\ndiam 17,6 x h 7\nvedi foto");

        Assert.Equal("Coppa Celadon", mapped.First(ImportField.Title)!.Text);
        Assert.Equal("diam 17,6 x h 7", mapped.First(ImportField.Dimensions)!.Text);
        Assert.DoesNotContain(mapped.Values, v => v.Text.Contains("vedi foto"));
        Assert.Contains("vedi foto", Assert.Single(mapped.Discarded!));
        Assert.Empty(mapped.Dropped);
    }

    [Fact]
    public void Line_discarded_by_content()
    {
        var acquisition = MappingSuggestion.Rules.Acquisition(2, "Provenienza");
        var rule = acquisition with { Lines = [new(null, DetectorIds.Money), .. acquisition.Lines!] };

        var mapped = Map(rule, "Sotheby's London\n12 06 1990\n£ 7.865");

        Assert.Null(mapped.First(ImportField.AcquisitionPrice));
        Assert.DoesNotContain(mapped.Values, v => v.Field == ImportField.AcquisitionNote);
        Assert.Equal("Sotheby's London", mapped.First(ImportField.AcquisitionSource)!.Text);
        Assert.Single(mapped.Discarded!);
    }

    [Fact]
    public void Old_template_json_still_loads_and_rules_keep_their_targets()
    {
        const string json = """{"columns":[{"column":2,"heading":"Nome","lines":[{"target":"title","detector":"itemTitle","continues":true}],"rest":"description"}],"inventoryNumber":{"pattern":"X-{row}"}}""";

        var mapping = ImportJson.Read<ImportMapping>(json);

        var line = Assert.Single(mapping.Columns.Single().Lines!);
        Assert.Equal(ImportField.Title, line.Target);
        Assert.False(line.Discards);
        Assert.Null(mapping.Dimensions);
    }

    [Fact]
    public void Discarding_rule_round_trips_through_json()
    {
        var mapping = new ImportMapping([new ColumnRule(1, "Nome", Lines: [new(null, Position: 0)])], new InventoryNumberPattern("X-{row}"),
            Dimensions: new DimensionsOptions(DimensionsParserTests.Cm, [MeasureKind.Height, MeasureKind.Width]));

        var read = ImportJson.Read<ImportMapping>(ImportJson.Write(mapping));

        Assert.True(read.Columns.Single().Lines!.Single().Discards);
        Assert.Equal(DimensionsParserTests.Cm, read.Dimensions!.DefaultUnitId);
        Assert.Equal([MeasureKind.Height, MeasureKind.Width], read.Dimensions.UnlabelledOrder);
    }

    [Fact]
    public void Suggestion_never_discards_and_proposes_centimetres()
    {
        var mapping = MappingSuggestion.Suggest(Headings, [new SheetRecord(2, "01", "CINA", new Dictionary<int, string> { [1] = "01. Coppa\ndiam 30" }, [], [])], "CERAMICHE");

        Assert.DoesNotContain(mapping.Columns.SelectMany(c => c.Lines ?? []), l => l.Discards);
        Assert.Equal(SeededUnits.Centimetre.Value, mapping.Dimensions!.DefaultUnitId);
        Assert.Empty(mapping.Dimensions.UnlabelledOrder ?? []);
    }
}

public class DimensionsEvaluationTests
{
    private static readonly Dictionary<int, string> Headings = new() { [1] = "Nome", [2] = "Misure" };

    private static ImportMapping Mapping(DimensionsOptions? options) => new(
        [new ColumnRule(1, "Nome", ImportField.Title), new ColumnRule(2, "Misure", ImportField.Dimensions)],
        new InventoryNumberPattern("X-{row}"), Dimensions: options);

    private static EvaluatedRow Evaluate(string dimensions, DimensionsOptions? options = null, params DimensionResolution[] resolutions)
    {
        var context = new EvaluationContext(Mapping(options ?? new DimensionsOptions(DimensionsParserTests.Cm)), Headings, null, new HashSet<string>(),
            _ => new ResolvedPlace(null, false, null), new Dictionary<string, DateResolution>(),
            Units: DimensionsParserTests.Units,
            DimensionResolutions: resolutions.ToDictionary(r => RowEvaluator.DateKey(r.Text)));
        return RowEvaluator.Evaluate(new StagedRow(3, null, null, new Dictionary<int, string> { [1] = "Coppa", [2] = dimensions }, [], []), context);
    }

    [Fact]
    public void Clear_text_fills_the_measurements_and_keeps_the_text()
    {
        var row = Evaluate("diam 17,6 x h 7");

        Assert.Equal(ImportRowStatus.Ready, row.Status);
        Assert.Equal("diam 17,6 x h 7", row.Request!.Dimensions);
        Assert.Equal(new MeasurementInput(17.6m, DimensionsParserTests.Cm), row.Request.Diameter);
        Assert.Equal(new MeasurementInput(7m, DimensionsParserTests.Cm), row.Request.Height);
        Assert.False(row.DimensionsUnclear);
    }

    [Fact]
    public void Undecided_unclear_text_is_a_problem_naming_the_text()
    {
        var row = Evaluate("chiuso 12 x 8; aperto 24 x 8");

        Assert.Equal(ImportRowStatus.Problem, row.Status);
        Assert.True(row.DimensionsUnclear);
        var problem = Assert.Single(row.Problems);
        Assert.Equal("dimensions", problem.Field);
        Assert.Equal("chiuso 12 x 8; aperto 24 x 8", problem.Text);
    }

    [Fact]
    public void Entered_measurements_apply_to_the_text()
    {
        var resolution = new DimensionResolution("Chiuso 12 x 8;  aperto 24 x 8",
            Height: new MeasurementInput(12, DimensionsParserTests.Cm), Width: new MeasurementInput(8, DimensionsParserTests.Cm));

        var row = Evaluate("chiuso 12 x 8; aperto 24 x 8", null, resolution);

        Assert.Equal(ImportRowStatus.Ready, row.Status);
        Assert.Equal("chiuso 12 x 8; aperto 24 x 8", row.Request!.Dimensions);
        Assert.Equal(12m, row.Request.Height!.Value);
        Assert.Equal(8m, row.Request.Width!.Value);
    }

    [Fact]
    public void Kept_as_text_only()
    {
        var row = Evaluate("misure varie", null, new DimensionResolution("misure varie", KeepTextOnly: true));

        Assert.Equal(ImportRowStatus.Ready, row.Status);
        Assert.Equal("misure varie", row.Request!.Dimensions);
        Assert.Null(row.Request.Height);
    }

    [Fact]
    public void Resolution_with_a_deleted_unit_counts_as_undecided()
    {
        var row = Evaluate("misure varie", null, new DimensionResolution("misure varie", Height: new MeasurementInput(3, Guid.NewGuid())));

        Assert.Equal(ImportRowStatus.Problem, row.Status);
    }

    [Fact]
    public void Decisions_merge_per_text()
    {
        var older = ImportDecisions.Empty with { Dimensions = [new DimensionResolution("a", KeepTextOnly: true), new DimensionResolution("b", KeepTextOnly: true)] };
        var newer = ImportDecisions.Empty with { Dimensions = [new DimensionResolution("A", Height: new MeasurementInput(1, DimensionsParserTests.Cm))] };

        var merged = older.MergedWith(newer).Dimensions!;

        Assert.Equal(2, merged.Count);
        Assert.False(merged.Single(d => d.Text == "A").KeepTextOnly);
        Assert.Contains(merged, d => d.Text == "b");
    }
}
