using Artefacts.Application.Imports;
using Artefacts.Domain.Imports;

namespace Artefacts.Application.Tests;

public class InventoryNumberTests
{
    private static InventoryNumberValues Values(string? group, string? item, int row = 14, string? catalogue = null) =>
        new(group, item, row, f => f == ImportField.CatalogueNumber ? catalogue : null);

    [Fact]
    public void Pattern_from_group_and_item() =>
        Assert.Equal("CER-01-02", InventoryNumbers.Build(new InventoryNumberPattern("CER-{group:2}-{item:2}"), Values("1", "2")));

    [Fact]
    public void Catalogue_number_preferred() =>
        Assert.Equal("W.389", InventoryNumbers.Build(
            new InventoryNumberPattern("PER-{group:2}-{item:2}", ImportField.CatalogueNumber), Values("1", "2", catalogue: "W.389")));

    [Fact]
    public void Pattern_used_when_the_preferred_field_is_empty() =>
        Assert.Equal("PER-01-02", InventoryNumbers.Build(
            new InventoryNumberPattern("PER-{group:2}-{item:2}", ImportField.CatalogueNumber), Values("01", "02")));

    [Fact]
    public void Missing_values_give_no_number() =>
        Assert.Null(InventoryNumbers.Build(new InventoryNumberPattern("CER-{group:2}-{item:2}"), Values("1", null)));

    [Theory]
    [InlineData("2a", 2, "02a")]
    [InlineData("12", 2, "12")]
    [InlineData("x", 3, "x")]
    public void Padding_applies_to_the_leading_number(string value, int width, string expected) =>
        Assert.Equal(expected, InventoryNumbers.Pad(value, width));

    [Fact]
    public void Sheet_token_gives_each_workbook_its_prefix() =>
        Assert.Equal("TES-01-02", InventoryNumbers.Build(new InventoryNumberPattern("{sheet}-{group:2}-{item:2}"),
            Values("1", "2") with { Sheet = MappingSuggestion.Prefix("TESSUTI") }));

    [Fact]
    public void Row_and_field_tokens() =>
        Assert.Equal("R14-W.389", InventoryNumbers.Build(new InventoryNumberPattern("R{row}-{field:catalogueNumber}"), Values(null, null, catalogue: "W.389")));

    [Theory]
    [InlineData("CER-{grup}", 1)]
    [InlineData("CER-{field:colour}", 1)]
    [InlineData("CER-{group", 1)]
    [InlineData("CER-{group:2}-{item}", 0)]
    public void Validates_tokens(string pattern, int errors) =>
        Assert.Equal(errors, InventoryNumbers.Validate(new InventoryNumberPattern(pattern)).Count);
}

public class PlaceMatcherTests
{
    private static readonly Guid China = Guid.NewGuid(), Turkey = Guid.NewGuid(), Egypt = Guid.NewGuid(), Anatolia = Guid.NewGuid();

    private static readonly PlaceCandidate[] Regions =
    [
        new(China, "China"), new(Turkey, "Turkey"), new(Turkey, "Turchia"), new(Egypt, "Egypt"), new(Egypt, "Egitto"),
        new(Anatolia, "Anatolia"),
    ];

    [Fact]
    public void Italian_name_matches_exactly()
    {
        var first = PlaceMatcher.Suggest("Turchia", Regions)[0];
        Assert.Equal(Turkey, first.RegionId);
        Assert.True(first.Exact);
    }

    [Fact]
    public void Exact_match_ignores_case_accents_and_question_marks() =>
        Assert.True(PlaceMatcher.Suggest(" turchìa ? ", Regions)[0].Exact);

    [Fact]
    public void Similar_region_suggested()
    {
        var suggestions = PlaceMatcher.Suggest("Cina", Regions);
        var china = Assert.Single(suggestions, s => s.RegionId == China);
        Assert.False(china.Exact);
        Assert.True(china.Score >= 0.85, $"Score was {china.Score}");
    }

    [Fact]
    public void A_part_of_the_text_can_match()
    {
        var suggestions = PlaceMatcher.Suggest("Cairo, Egitto", Regions);
        Assert.Equal(Egypt, suggestions[0].RegionId);
    }

    [Fact]
    public void Alias_from_a_template_ranks_first()
    {
        var withAlias = Regions.Append(new PlaceCandidate(Anatolia, "Anatolia occidentale", Alias: true)).ToList();

        var first = PlaceMatcher.Suggest("Anatolia occidentale", withAlias)[0];

        Assert.Equal(Anatolia, first.RegionId);
        Assert.True(first.Exact && first.Alias);
    }

    [Theory]
    [InlineData("Kairouan")]
    [InlineData("Iran")]
    [InlineData("Persia")]
    public void Unrelated_text_has_no_suggestions(string text) =>
        Assert.Empty(PlaceMatcher.Suggest(text, [.. Regions, new PlaceCandidate(Guid.NewGuid(), "France"), new PlaceCandidate(Guid.NewGuid(), "Istanbul")]));

    [Fact]
    public void At_most_three_suggestions()
    {
        var many = Enumerable.Range(1, 6).Select(i => new PlaceCandidate(Guid.NewGuid(), $"Persia {i}")).ToList();
        Assert.Equal(3, PlaceMatcher.Suggest("Persia", many).Count);
    }
}

public class RowEvaluatorTests
{
    private static readonly Dictionary<int, string> Headings = new() { [1] = "Gruppo e numero", [2] = "Nome File", [4] = "Zona di Produzione", [5] = "Provenienza", [7] = "Valore" };

    private static readonly ImportMapping Mapping = new(
        [
            MappingSuggestion.Rules.Holding(1, "Gruppo e numero"),
            MappingSuggestion.Rules.Title(2, "Nome File"),
            MappingSuggestion.Rules.Origin(4, "Zona di Produzione"),
            MappingSuggestion.Rules.Acquisition(5, "Provenienza"),
            new ColumnRule(7, "Valore", ImportField.Valuation),
        ],
        new InventoryNumberPattern("CER-{group:2}-{item:2}", ImportField.CatalogueNumber));

    private static StagedRow Row(int sheetRow, string title, string? origin = null, string? acquisition = null, string? value = null,
        params StagedPicture[] pictures)
    {
        var cells = new Dictionary<int, string> { [1] = "01.", [2] = title };
        if (origin is not null) cells[4] = origin;
        if (acquisition is not null) cells[5] = acquisition;
        if (value is not null) cells[7] = value;
        return new StagedRow(sheetRow, "01", "CINA", cells, pictures, []);
    }

    private static EvaluationContext Context(IEnumerable<string>? existing = null, Dictionary<string, DateResolution>? dates = null) =>
        new(Mapping, Headings, Guid.NewGuid(), new HashSet<string>(existing ?? []), _ => new ResolvedPlace(null, false, null),
            dates ?? new Dictionary<string, DateResolution>());

    [Fact]
    public void Ready_row_becomes_a_complete_request()
    {
        var row = Row(3, "02. Piatto Celadon\ndiam 30", "Cina\nfine XII inizio XIII\nSung", "Sotheby's London\n12 06 1990\nlot 170 £ 7.865");

        var result = Assert.Single(RowEvaluator.Evaluate([row], Context()));

        Assert.Equal(ImportRowStatus.Ready, result.Status);
        var r = result.Request!;
        Assert.Equal("CER-01-02", r.InventoryNumber);
        Assert.Equal("Piatto Celadon", r.Title);
        Assert.Equal("diam 30", r.Dimensions);
        Assert.Equal("CINA", r.CollectionGroup);
        Assert.Equal("Cina", r.PlaceOfProduction!.Text);
        Assert.Equal("fine XII inizio XIII", r.CreationDate!.Text);
        Assert.Equal("Sung", r.Dynasty);
        Assert.Equal("Sotheby's London", r.Acquisition!.Source);
        Assert.Equal(new DateOnly(1990, 6, 12), r.Acquisition.Date);
        Assert.Equal("170", r.Acquisition.Lot);
        Assert.Equal(7865m, r.Acquisition.Price!.Amount);
        // The separator was a guess: the price as written is kept in the note.
        Assert.Equal("£ 7.865", r.Acquisition.Note);
    }

    [Fact]
    public void Repeated_inventory_number_is_a_problem_naming_the_first_row()
    {
        var rows = new[] { Row(14, "02. Piatto"), Row(15, "02. Piatto bis") };

        var results = RowEvaluator.Evaluate(rows, Context());

        Assert.Equal(ImportRowStatus.Ready, results[0].Status);
        Assert.Equal(ImportRowStatus.Problem, results[1].Status);
        Assert.Contains("Row 14", Assert.Single(results[1].Problems).Reason);
    }

    [Fact]
    public void Missing_title_and_number_are_problems()
    {
        var result = RowEvaluator.Evaluate([Row(3, "diam 30")], Context())[0];

        Assert.Equal(ImportRowStatus.Problem, result.Status);
        Assert.Contains(result.Problems, p => p.Field == "title");
        Assert.Contains(result.Problems, p => p.Field == "inventoryNumber");
    }

    [Fact]
    public void Unresolved_date_is_a_problem_until_a_range_is_given()
    {
        var row = Row(3, "01. Bacile", "Egitto\n19 Dhu'l hijja 797");

        var unresolved = RowEvaluator.Evaluate([row], Context())[0];
        Assert.Equal(ImportRowStatus.Problem, unresolved.Status);
        Assert.True(unresolved.DateUnresolved);
        Assert.Equal("creationDate", Assert.Single(unresolved.Problems).Field);

        var resolution = new DateResolution("19 Dhu'l hijja 797", 1395, 1395);
        var resolved = RowEvaluator.Evaluate([row], Context(dates: new() { [RowEvaluator.DateKey(resolution.Text)] = resolution }))[0];
        Assert.Equal(ImportRowStatus.Ready, resolved.Status);
        Assert.Equal(new Contracts.HistoricalDateInput("19 Dhu'l hijja 797", 1395, 1395), resolved.Request!.CreationDate);
    }

    [Fact]
    public void Invalid_money_is_a_problem() =>
        Assert.Contains(RowEvaluator.Evaluate([Row(3, "01. Bacile", value: "da stimare")], Context())[0].Problems, p => p.Field == "valuation");

    [Fact]
    public void Row_already_in_the_catalogue_is_skipped() =>
        Assert.Equal(ImportRowStatus.Skipped, RowEvaluator.Evaluate([Row(3, "01. Bacile")], Context(existing: ["CER-01-01"]))[0].Status);

    [Fact]
    public void Unsupported_picture_is_a_warning_not_a_problem()
    {
        var picture = new StagedPicture(1, "Immagine 1", "imports/x/pictures/1.wdp", "image/vnd.ms-photo", 16, null);

        var result = RowEvaluator.Evaluate([Row(3, "01. Bacile", pictures: picture)], Context())[0];

        Assert.Equal(ImportRowStatus.Ready, result.Status);
        Assert.Contains("Immagine 1", Assert.Single(result.Warnings));
    }

    [Fact]
    public void Linked_place_carries_its_region()
    {
        var region = Guid.NewGuid();
        var context = Context() with { Place = text => new ResolvedPlace(text == "Cina" ? region : null, false, PlaceAction.Link) };

        var result = RowEvaluator.Evaluate([Row(3, "01. Coppa", "Cina\nXV")], context)[0];

        Assert.Equal(region, result.Request!.PlaceOfProduction!.RegionId);
    }
}
