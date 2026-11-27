using System.Diagnostics;
using Artefacts.Application.Imports;
using Artefacts.Domain.Imports;

namespace Artefacts.Application.Tests;

public class ImportUserDetectorTests
{
    private static UserDetector Regex(string pattern, DetectorParse parse = DetectorParse.None, bool ignoreCase = true) =>
        new("user:test", "Test", new DetectorDefinition(DetectorKind.Regex, pattern, ignoreCase, parse));

    private static UserDetector Keywords(KeywordMatch match, params string[] words) =>
        new("user:test", "Test", new DetectorDefinition(DetectorKind.Keywords, Keywords: words, Match: match));

    private static Detection? Detect(UserDetector d, string line) => d.Detect(line, DetectionContext.Default);

    // ---------- pattern with named captures ----------

    [Fact]
    public void The_value_group_is_the_text_and_the_rest_of_the_line_is_left_over()
    {
        var detection = Detect(Regex(@"ass\.\s*(?<value>[\w' ]+?)\s*(?:\d{4})?$"), "ass. Lloyd's 1999");

        Assert.Equal("Lloyd's", detection!.Text);
    }

    [Fact]
    public void Without_a_value_group_the_whole_match_is_the_text_and_the_remainder_is_what_is_outside_it()
    {
        var detection = Detect(Regex(@"\binv\.?\s*\d+"), "Brocca inv. 12 restaurata");

        Assert.Equal("inv. 12", detection!.Text);
        Assert.Equal("Brocca restaurata", detection.Remainder);
    }

    [Fact]
    public void Remainder_and_item_groups_are_used_when_the_pattern_names_them()
    {
        var detection = Detect(Regex(@"^(?<item>\d+)\)\s*(?<value>\S.*)$"), "12) Coppa");

        Assert.Equal("Coppa", detection!.Text);
        Assert.Equal("12", detection.ItemNumber);

        var withRemainder = Detect(Regex(@"^(?<value>\d+) (?<remainder>.*)$"), "30 pezzi rotti");
        Assert.Equal("30", withRemainder!.Text);
        Assert.Equal("pezzi rotti", withRemainder.Remainder);
    }

    [Fact]
    public void Matching_ignores_case_unless_the_definition_says_otherwise()
    {
        Assert.NotNull(Detect(Regex("^lotto"), "LOTTO 4"));
        Assert.Null(Detect(Regex("^lotto", ignoreCase: false), "LOTTO 4"));
    }

    [Fact]
    public void A_line_the_pattern_does_not_match_is_not_recognised() => Assert.Null(Detect(Regex(@"^\d+$"), "Coppa"));

    // ---------- reading the recognised text ----------

    [Fact]
    public void Recognised_text_can_be_read_as_a_number_an_amount_or_a_date()
    {
        Assert.Equal(12.5m, Detect(Regex(@"peso\s*(?<value>[\d,.]+)", DetectorParse.Number), "peso 12,5 kg")!.Parsed);

        var money = Assert.IsType<ParsedMoney>(Detect(Regex(@"stima:\s*(?<value>.+)", DetectorParse.Money), "stima: £ 7.865")!.Parsed);
        Assert.Equal("GBP", money.Currency);

        Assert.Equal(new DateOnly(1990, 6, 12), Detect(Regex(@"fatt\.\s*(?<value>.+)", DetectorParse.CalendarDate), "fatt. 12 06 1990")!.Parsed);
    }

    [Fact]
    public void Text_that_cannot_be_read_as_asked_is_not_recognised()
    {
        Assert.Null(Detect(Regex(@"peso\s*(?<value>\S+)", DetectorParse.Number), "peso molto"));
        Assert.Null(Detect(Regex(@"stima:\s*(?<value>.+)", DetectorParse.Money), "stima: alta"));
    }

    // ---------- keywords ----------

    [Fact]
    public void Keywords_are_matched_ignoring_case_the_way_the_definition_says()
    {
        var contains = Keywords(KeywordMatch.Contains, "da montare");
        Assert.Equal("Piatto DA MONTARE subito", Detect(contains, "  Piatto DA MONTARE subito ")!.Text);
        Assert.Null(Detect(contains, "Piatto montato"));

        var starts = Keywords(KeywordMatch.StartsWith, "Nota:");
        Assert.NotNull(Detect(starts, "nota: fragile"));
        Assert.Null(Detect(starts, "vedi nota: fragile"));

        var whole = Keywords(KeywordMatch.Equals, "restaurato", "integro");
        Assert.NotNull(Detect(whole, "Restaurato."));
        Assert.Null(Detect(whole, "restaurato nel 1990"));
    }

    // ---------- validation ----------

    [Fact]
    public void A_valid_definition_has_no_problems()
    {
        Assert.Empty(new DetectorDefinition(DetectorKind.Regex, @"^\d+$").Problems());
        Assert.Empty(new DetectorDefinition(DetectorKind.Keywords, Keywords: ["a"]).Problems());
    }

    [Fact]
    public void An_invalid_pattern_is_reported_against_the_pattern()
    {
        var problems = new DetectorDefinition(DetectorKind.Regex, "(unclosed").Problems();

        Assert.Contains("definition.pattern", problems.Keys);
        Assert.StartsWith("Not a valid pattern", problems["definition.pattern"]);
    }

    [Fact]
    public void A_pattern_that_matches_empty_text_or_is_too_long_is_rejected()
    {
        Assert.Contains("definition.pattern", new DetectorDefinition(DetectorKind.Regex, @"\d*").Problems().Keys);
        Assert.Contains("definition.pattern", new DetectorDefinition(DetectorKind.Regex, new string('a', DetectorDefinition.MaxPatternLength + 1)).Problems().Keys);
        Assert.Contains("definition.pattern", new DetectorDefinition(DetectorKind.Regex, " ").Problems().Keys);
    }

    [Fact]
    public void Keywords_are_limited_and_required()
    {
        Assert.Contains("definition.keywords", new DetectorDefinition(DetectorKind.Keywords, Keywords: []).Problems().Keys);
        Assert.Contains("definition.keywords", new DetectorDefinition(DetectorKind.Keywords, Keywords: [" "]).Problems().Keys);
        Assert.Contains("definition.keywords",
            new DetectorDefinition(DetectorKind.Keywords, Keywords: [.. Enumerable.Repeat("a", DetectorDefinition.MaxKeywords + 1)]).Problems().Keys);
        Assert.Contains("definition.keywords",
            new DetectorDefinition(DetectorKind.Keywords, Keywords: [new string('a', DetectorDefinition.MaxKeywordLength + 1)]).Problems().Keys);
    }

    // ---------- safety ----------

    /// <summary>A pattern that backtracks catastrophically gives up after the timeout: the line is not recognised, the import goes on.</summary>
    [Fact]
    public void A_pattern_that_takes_too_long_recognises_nothing_instead_of_hanging()
    {
        var detector = Regex(@"^(a+)+$");
        var line = new string('a', 40) + "!";

        var watch = Stopwatch.StartNew();
        var detection = Detect(detector, line);
        watch.Stop();

        Assert.Null(detection);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Took {watch.Elapsed}");
    }

    // ---------- stored detectors and the registry ----------

    [Fact]
    public void A_stored_detector_is_identified_by_user_and_its_id()
    {
        var stored = ImportDetector.Create("Assicurazione", ImportJson.Write(new DetectorDefinition(DetectorKind.Regex, @"^ass\.")), DateTimeOffset.UnixEpoch);

        Assert.Equal("user:" + stored.Id.ToString("N"), stored.Identifier);
        Assert.Equal(stored.Id, ImportDetector.IdOf(stored.Identifier));
        Assert.Null(ImportDetector.IdOf("money"));
        Assert.Null(ImportDetector.IdOf("user:nope"));
    }

    [Fact]
    public void A_stored_detector_joins_the_registry_and_is_used_by_line_rules()
    {
        var stored = ImportDetector.Create("Assicurazione", ImportJson.Write(new DetectorDefinition(DetectorKind.Regex, @"^ass\.\s*(?<value>.+)$")), DateTimeOffset.UnixEpoch);
        var registry = DetectorRegistry.BuiltIn.With([UserDetector.From(stored)!]);
        var mapping = new ImportMapping(
            [new ColumnRule(1, "Note", Lines: [new LineRule(ImportField.AcquisitionNote, stored.Identifier)], Rest: ImportField.Description)],
            new InventoryNumberPattern("X-{row}"));

        var mapped = MappingEngine.Map(new Dictionary<int, string> { [1] = "ass. Lloyd's 1999\nuna coppa" }, mapping, new Dictionary<int, string> { [1] = "Note" }, registry);

        Assert.True(registry.Contains(stored.Identifier));
        Assert.Equal("Lloyd's 1999", mapped.First(ImportField.AcquisitionNote)!.Text);
        Assert.Equal("una coppa", mapped.First(ImportField.Description)!.Text);
    }

    [Fact]
    public void A_stored_definition_that_no_longer_holds_is_left_out_and_a_built_in_id_cannot_be_taken()
    {
        var broken = ImportDetector.Create("Broken", ImportJson.Write(new DetectorDefinition(DetectorKind.Regex, "(unclosed")), DateTimeOffset.UnixEpoch);
        var impostor = new UserDetector(DetectorIds.Money, "Impostor", new DetectorDefinition(DetectorKind.Keywords, Keywords: ["x"]));

        Assert.Null(UserDetector.From(broken));
        Assert.Equal(DetectorRegistry.BuiltIn.Ids, DetectorRegistry.BuiltIn.With([impostor]).Ids);
    }
}
