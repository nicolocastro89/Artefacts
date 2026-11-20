using Artefacts.Application.Imports;

namespace Artefacts.Application.Tests;

public class ImportDetectorTests
{
    private static Detection? Detect(LineDetector detector, string line) => Detectors.Detect(detector, line);

    [Theory]
    [InlineData("01. Coppa Celadon", "01", "Coppa Celadon")]
    [InlineData("01.Corano di Mehmet II ", "01", "Corano di Mehmet II")]
    [InlineData("02a Frammento ", "02a", "Frammento")]
    [InlineData("12. Piatto", "12", "Piatto")]
    [InlineData("05. 2 piastrelle bianche e blu", "05", "2 piastrelle bianche e blu")]
    [InlineData("19, frammento di lustro", "19", "frammento di lustro")]
    [InlineData("03 b. coppa a lustro", "03b", "coppa a lustro")]
    [InlineData(" 18. 2 piastrelle Iznik", "18", "2 piastrelle Iznik")]
    [InlineData("08 Firmano del Sultano", "08", "Firmano del Sultano")]
    [InlineData("01-B3A mamelucco", "01", "B3A mamelucco")]
    [InlineData("29b-B20C sezione di tappeto", "29b", "B20C sezione di tappeto")]
    public void Item_title_gives_number_and_title(string line, string item, string title)
    {
        var d = Detect(LineDetector.ItemTitle, line)!;
        Assert.Equal(item, d.ItemNumber);
        Assert.Equal(title, d.Text);
    }

    [Theory]
    [InlineData("126 x 210")]
    [InlineData("27.5 x 30")]
    [InlineData("con circoli")]
    [InlineData("01.")]
    public void Item_title_ignores_measurements_and_plain_text(string line) =>
        Assert.Null(Detect(LineDetector.ItemTitle, line));

    [Theory]
    [InlineData("diam 17,6 x h 7")]
    [InlineData("chiuso 34 x 26  x 7")]
    [InlineData("27cm. x 8.5cm.")]
    [InlineData("h 21 diam base 19,4")]
    [InlineData("cm. 13,9 x 19,9")]
    [InlineData("diam 34,6")]
    [InlineData("27,9 x 36 cm")]
    public void Dimensions_are_recognised(string line) => Assert.NotNull(Detect(LineDetector.Dimensions, line));

    [Theory]
    [InlineData("con circoli")]
    [InlineData("DA MONTARE")]
    [InlineData("Sotheby's London")]
    public void Plain_text_is_not_dimensions(string line) => Assert.Null(Detect(LineDetector.Dimensions, line));

    [Theory]
    [InlineData("12 06 1990", 1990, 6, 12, null)]
    [InlineData("27-11-2018", 2018, 11, 27, null)]
    [InlineData("8 aprile 1975", 1975, 4, 8, null)]
    [InlineData("Christie's 2 Maggio 2019", 2019, 5, 2, "Christie's")]
    [InlineData("07 10 2008 lot 287", 2008, 10, 7, "lot 287")]
    [InlineData("4 aprile 2006, lot 3", 2006, 4, 4, "lot 3")]
    [InlineData("21 02 14", 2014, 2, 21, null)]
    public void Calendar_dates_are_found_anywhere_and_leave_the_rest(string line, int y, int m, int d, string? rest)
    {
        var detection = Detect(LineDetector.CalendarDate, line)!;
        Assert.Equal(new DateOnly(y, m, d), detection.Parsed);
        Assert.Equal(rest, detection.Remainder);
    }

    [Theory]
    [InlineData("31 02 1990")]
    [InlineData("Carbon dating AD 527-621")]
    [InlineData("£ 7.865")]
    public void Impossible_or_absent_dates_are_not_calendar_dates(string line) =>
        Assert.Null(Detect(LineDetector.CalendarDate, line));

    [Theory]
    [InlineData("lot 170 £ 7.865", "170", "£ 7.865")]
    [InlineData("lot.147", "147", null)]
    [InlineData("lotto 12", "12", null)]
    [InlineData("lot 112  Fr. 62.000", "112", "Fr. 62.000")]
    public void Lot_passes_on_the_rest_of_the_line(string line, string lot, string? rest)
    {
        var d = Detect(LineDetector.Lot, line)!;
        Assert.Equal(lot, d.Text);
        Assert.Equal(rest, d.Remainder);
    }

    [Theory]
    [InlineData("£ 7.865", 7865, "GBP", true)]
    [InlineData("€ 5.196,00", 5196, "EUR", false)]
    [InlineData("£ 85,400", 85400, "GBP", true)]
    [InlineData("lire 350.000.000", 350000000, "ITL", false)]
    [InlineData("£ 8,645.00", 8645, "GBP", false)]
    [InlineData("Fr. 62.000", 62000, "FRF", true)]
    [InlineData("$ 12,5", 12.5, "USD", false)]
    [InlineData("1.200 euro", 1200, "EUR", true)]
    public void Money_reads_currency_and_separators(string line, double amount, string currency, bool guessed)
    {
        var d = Detect(LineDetector.Money, line)!;
        Assert.Equal(new ParsedMoney((decimal)amount, currency), d.Parsed);
        Assert.Equal(guessed, d.Guessed);
    }

    [Theory]
    [InlineData("Galleria C4486")]
    [InlineData("Carbon dating AD 527-621")]
    [InlineData("acquistato fuori asta")]
    public void Text_without_a_currency_is_not_money(string line) => Assert.Null(Detect(LineDetector.Money, line));

    [Theory]
    [InlineData("W.389", "W.389")]
    [InlineData("W. 390", "W.390")]
    public void Catalogue_numbers_are_recognised(string line, string expected) =>
        Assert.Equal(expected, Detect(LineDetector.CatalogueNumber, line)!.Text);

    [Theory]
    [InlineData("MI F3", true)]
    [InlineData("GE", true)]
    [InlineData("cella 16", true)]
    [InlineData("Beta2", true)]
    [InlineData("Rossetti", false)]
    [InlineData("AB", false)]
    public void Location_codes_follow_the_patterns(string line, bool expected) =>
        Assert.Equal(expected, Detect(LineDetector.LocationCode, line) is not null);

    [Theory]
    [InlineData("fine XII inizio XIII", true)]
    [InlineData("19 Dhu'l hijja 797", false)]
    [InlineData("datato 1491", false)]
    [InlineData("Joumada I 966", false)]
    public void Historical_date_lines_include_ones_the_rules_do_not_understand(string line, bool understood)
    {
        var d = Detect(LineDetector.HistoricalDate, line)!;
        Assert.Equal(understood, ((ParsedDateText)d.Parsed!).Interpretation is not null);
    }

    [Theory]
    [InlineData("Cina")]
    [InlineData("Anatolia o Asia Centrale?")]
    [InlineData("Qinghai Dulan VIII")]
    [InlineData("Sung")]
    [InlineData("Dominicus Custos")]
    public void Places_and_dynasties_are_not_dates(string line) => Assert.Null(Detect(LineDetector.HistoricalDate, line));

    [Theory]
    [InlineData("01.")]
    [InlineData("01. ")]
    [InlineData("1")]
    public void Group_numbers_are_recognised(string line) => Assert.Equal(line.Trim().TrimEnd('.'), Detect(LineDetector.GroupNumber, line)!.Text);
}
