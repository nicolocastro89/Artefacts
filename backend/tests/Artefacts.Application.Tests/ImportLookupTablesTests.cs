using Artefacts.Application.Imports;

namespace Artefacts.Application.Tests;

public class ImportLookupTablesTests
{
    private static readonly LookupTables BuiltIn = LookupTables.BuiltIn;

    /// <summary>The identifiers users' table entries are stored under: renaming one orphans them.</summary>
    [Fact]
    public void Built_in_tables_keep_their_identifiers() =>
        Assert.Equal(["months", "currenciesBeforeAmount", "currenciesAfterAmount", "measureLabels", "inchAliases"], BuiltIn.Tables.Select(t => t.Id));

    [Fact]
    public void Built_in_entries_are_read_ignoring_case()
    {
        Assert.Equal(9, BuiltIn.Month("Sept"));
        Assert.Equal(5, BuiltIn.Month("MAGGIO"));
        Assert.Equal("ITL", BuiltIn.Currency("Lire"));
        Assert.Equal(MeasureKind.Diameter, BuiltIn.MeasureLabel("Ø"));
        Assert.Null(BuiltIn.Month("nope"));
    }

    [Fact]
    public void A_user_month_extends_the_built_in_ones()
    {
        Assert.Null(Detectors.CalendarDate("12 Mai 1990"));

        var tables = BuiltIn.With([new LookupTable(LookupTableIds.Months, "Month names", ("mai", "5"))]);
        var detection = Detectors.CalendarDate("12 Mai 1990", tables);

        Assert.Equal(new DateOnly(1990, 5, 12), detection!.Parsed);
        Assert.Equal(5, tables.Month("maggio"));
    }

    [Fact]
    public void A_user_entry_wins_over_a_built_in_one()
    {
        var tables = BuiltIn.With([new LookupTable(LookupTableIds.CurrenciesBeforeAmount, "Currencies", ("l", "GBP"))]);

        Assert.Equal("ITL", Assert.IsType<ParsedMoney>(Detectors.Money("L. 1.000")!.Parsed).Currency);
        Assert.Equal("GBP", Assert.IsType<ParsedMoney>(Detectors.Money("L. 1.000", tables)!.Parsed).Currency);
    }

    [Fact]
    public void A_user_currency_word_is_recognised_before_and_after_an_amount()
    {
        Assert.Null(Detectors.Money("kr 500"));
        var tables = BuiltIn.With(
        [
            new LookupTable(LookupTableIds.CurrenciesBeforeAmount, "Before", ("kr", "SEK")),
            new LookupTable(LookupTableIds.CurrenciesAfterAmount, "After", ("kr", "SEK")),
        ]);

        Assert.Equal("SEK", Assert.IsType<ParsedMoney>(Detectors.Money("kr 500", tables)!.Parsed).Currency);
        Assert.Equal("SEK", Assert.IsType<ParsedMoney>(Detectors.Money("500 kr", tables)!.Parsed).Currency);
        Assert.Equal("GBP", Assert.IsType<ParsedMoney>(Detectors.Money("£ 7.865", tables)!.Parsed).Currency);
    }

    [Fact]
    public void A_user_measurement_label_is_read_by_the_dimensions_parser()
    {
        var options = new DimensionsOptions(DimensionsParserTests.Cm);
        Assert.Null(DimensionsParser.Parse("haut 30 x larg 45", DimensionsParserTests.Units, options));

        var tables = BuiltIn.With([new LookupTable(LookupTableIds.MeasureLabels, "Labels", ("haut", "height"))]);
        var parsed = DimensionsParser.Parse("haut 30 x larg 45", DimensionsParserTests.Units, options, tables);

        Assert.Equal(30m, parsed!.Of(MeasureKind.Height)!.Value);
        Assert.Equal(45m, parsed.Of(MeasureKind.Width)!.Value);
    }

    [Fact]
    public void A_user_table_the_application_does_not_ship_is_kept_and_a_missing_one_is_empty()
    {
        var tables = BuiltIn.With([new LookupTable("custom", "Custom", ("a", "b"))]);

        Assert.Equal("b", tables.Get("custom").Find("A"));
        Assert.Empty(BuiltIn.Get("custom").Entries);
        Assert.Equal(BuiltIn.Tables.Count + 1, tables.Tables.Count);
    }

    [Fact]
    public void A_value_that_does_not_fit_its_table_is_ignored()
    {
        var tables = BuiltIn.With(
        [
            new LookupTable(LookupTableIds.Months, "Months", ("xx", "13")),
            new LookupTable(LookupTableIds.MeasureLabels, "Labels", ("xx", "colour")),
        ]);

        Assert.Null(tables.Month("xx"));
        Assert.Null(tables.MeasureLabel("xx"));
    }
}
