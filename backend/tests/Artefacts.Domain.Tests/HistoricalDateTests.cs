using Artefacts.Domain.Common;
using Artefacts.Domain.Dating;

namespace Artefacts.Domain.Tests;

public class HistoricalDateTests
{
    [Theory]
    [InlineData("1654", 1654, 1654, HistoricalDatePrecision.Year)]
    [InlineData("c. 1650", 1640, 1660, HistoricalDatePrecision.Approximate)]
    [InlineData("circa 1650", 1640, 1660, HistoricalDatePrecision.Approximate)]
    [InlineData("ca. 1650", 1640, 1660, HistoricalDatePrecision.Approximate)]
    [InlineData("c1650", 1640, 1660, HistoricalDatePrecision.Approximate)]
    [InlineData("1680s", 1680, 1689, HistoricalDatePrecision.Decade)]
    [InlineData("17th century", 1601, 1700, HistoricalDatePrecision.Century)]
    [InlineData("1st century", 1, 100, HistoricalDatePrecision.Century)]
    [InlineData("21st century", 2001, 2100, HistoricalDatePrecision.Century)]
    [InlineData("early 17th century", 1601, 1633, HistoricalDatePrecision.Century)]
    [InlineData("mid 17th century", 1634, 1666, HistoricalDatePrecision.Century)]
    [InlineData("mid-17th century", 1634, 1666, HistoricalDatePrecision.Century)]
    [InlineData("late 17th century", 1667, 1700, HistoricalDatePrecision.Century)]
    [InlineData("Late 17th Century", 1667, 1700, HistoricalDatePrecision.Century)]
    [InlineData("1650-1660", 1650, 1660, HistoricalDatePrecision.Year)]
    [InlineData("1650 – 1660", 1650, 1660, HistoricalDatePrecision.Year)]
    [InlineData("before 1720", null, 1719, HistoricalDatePrecision.Approximate)]
    [InlineData("after 1800", 1801, null, HistoricalDatePrecision.Approximate)]
    public void Interprets_supported_forms(string text, int? earliest, int? latest, HistoricalDatePrecision precision)
    {
        var date = HistoricalDate.Create(text);

        Assert.Equal(earliest, date.EarliestYear);
        Assert.Equal(latest, date.LatestYear);
        Assert.Equal(precision, date.Precision);
    }

    [Theory]
    // Circa after the year
    [InlineData("1470 circa", 1460, 1480, HistoricalDatePrecision.Approximate)]
    // Roman-numeral centuries, with or without "secolo"/"sec."
    [InlineData("XV", 1401, 1500, HistoricalDatePrecision.Century)]
    [InlineData("XV secolo", 1401, 1500, HistoricalDatePrecision.Century)]
    [InlineData("IX sec.", 801, 900, HistoricalDatePrecision.Century)]
    [InlineData("XIII sec", 1201, 1300, HistoricalDatePrecision.Century)]
    [InlineData("XIX", 1801, 1900, HistoricalDatePrecision.Century)]
    // Italian parts of a century
    [InlineData("inizio XVII", 1601, 1633, HistoricalDatePrecision.Century)]
    [InlineData("metà XVI", 1534, 1566, HistoricalDatePrecision.Century)]
    [InlineData("fine XVI", 1567, 1600, HistoricalDatePrecision.Century)]
    [InlineData("Fine XIII", 1267, 1300, HistoricalDatePrecision.Century)]
    [InlineData("prima metà XVI", 1501, 1550, HistoricalDatePrecision.Century)]
    [InlineData("seconda metà XV", 1451, 1500, HistoricalDatePrecision.Century)]
    [InlineData("seconda metà XV sec", 1451, 1500, HistoricalDatePrecision.Century)]
    [InlineData("inizio XIV sec.", 1301, 1333, HistoricalDatePrecision.Century)]
    // Spans of centuries and century parts
    [InlineData("XI-XII", 1001, 1200, HistoricalDatePrecision.Century)]
    [InlineData("XI - XII", 1001, 1200, HistoricalDatePrecision.Century)]
    [InlineData("IX/X", 801, 1000, HistoricalDatePrecision.Century)]
    [InlineData("X/XI secolo", 901, 1100, HistoricalDatePrecision.Century)]
    [InlineData("XV XVI", 1401, 1600, HistoricalDatePrecision.Century)]
    [InlineData("XVI - XVII secolo", 1501, 1700, HistoricalDatePrecision.Century)]
    [InlineData("fine XII inizio XIII", 1167, 1233, HistoricalDatePrecision.Century)]
    [InlineData("fine X-inizio XI", 967, 1033, HistoricalDatePrecision.Century)]
    [InlineData("seconda metà VII - VIII", 651, 800, HistoricalDatePrecision.Century)]
    // Shortened year ranges
    [InlineData("1450 - 75", 1450, 1475, HistoricalDatePrecision.Year)]
    [InlineData("1463-4", 1463, 1464, HistoricalDatePrecision.Year)]
    [InlineData("1480-81", 1480, 1481, HistoricalDatePrecision.Year)]
    [InlineData("1530- 35", 1530, 1535, HistoricalDatePrecision.Year)]
    [InlineData("1330 -1340", 1330, 1340, HistoricalDatePrecision.Year)]
    // A century refined by a year expression
    [InlineData("XV (1440-1446)", 1440, 1446, HistoricalDatePrecision.Year)]
    [InlineData("XIII (1260/70)", 1260, 1270, HistoricalDatePrecision.Year)]
    [InlineData("inizio XIV (1330)", 1330, 1330, HistoricalDatePrecision.Year)]
    [InlineData("Fine XVI (1580 circa)", 1570, 1590, HistoricalDatePrecision.Approximate)]
    [InlineData("XV 1450", 1450, 1450, HistoricalDatePrecision.Year)]
    [InlineData("seconda metà XIV 1386", 1386, 1386, HistoricalDatePrecision.Year)]
    public void Interprets_italian_and_roman_forms(string text, int? earliest, int? latest, HistoricalDatePrecision precision)
    {
        var date = HistoricalDate.Create(text);

        Assert.Equal(text, date.Display);
        Assert.Equal(earliest, date.EarliestYear);
        Assert.Equal(latest, date.LatestYear);
        Assert.Equal(precision, date.Precision);
    }

    [Fact]
    public void Circa_after_the_year_matches_circa_before()
    {
        var after = HistoricalDate.Create("1470 circa");
        var before = HistoricalDate.Create("c. 1470");
        Assert.Equal((before.EarliestYear, before.LatestYear, before.Precision), (after.EarliestYear, after.LatestYear, after.Precision));
    }

    [Theory]
    [InlineData("XIIII")] // not a canonical numeral
    [InlineData("XII - X")] // span runs backwards
    [InlineData("II secolo a.C.")] // years before 1 are not supported
    [InlineData("X Fatimide")]
    [InlineData("1475 - 50")] // shortened end before the start
    [InlineData("Qinghai Dulan VIII")]
    public void Rejects_unsupported_italian_forms(string text)
    {
        Assert.Throws<DomainValidationException>(() => HistoricalDate.Create(text));
    }

    [Fact]
    public void Keeps_display_text_as_entered_with_whitespace_collapsed()
    {
        var date = HistoricalDate.Create("  late   17th century ");
        Assert.Equal("late 17th century", date.Display);
    }

    [Fact]
    public void Circa_date_brackets_the_year()
    {
        var date = HistoricalDate.Create("c. 1650");
        Assert.Equal("c. 1650", date.Display);
        Assert.True(date.EarliestYear < 1650 && date.LatestYear > 1650);
        Assert.Equal(HistoricalDatePrecision.Approximate, date.Precision);
    }

    [Theory]
    [InlineData("unknown era")]
    [InlineData("1660-1650")]
    [InlineData("1685s")]
    [InlineData("17nd century")]
    [InlineData("0")]
    public void Rejects_uninterpretable_text_without_explicit_range(string text)
    {
        Assert.Throws<DomainValidationException>(() => HistoricalDate.Create(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_empty_text(string? text)
    {
        Assert.Throws<DomainValidationException>(() => HistoricalDate.Create(text));
    }

    [Fact]
    public void Accepts_uninterpretable_text_with_explicit_range()
    {
        var date = HistoricalDate.Create("during the reign of Louis XIV", 1643, 1715);

        Assert.Equal("during the reign of Louis XIV", date.Display);
        Assert.Equal(1643, date.EarliestYear);
        Assert.Equal(1715, date.LatestYear);
    }

    [Fact]
    public void Uninterpretable_text_requires_both_explicit_bounds()
    {
        Assert.Throws<DomainValidationException>(() => HistoricalDate.Create("reign of Louis XIV", 1643, null));
    }

    [Fact]
    public void Explicit_range_overrides_interpretation()
    {
        var date = HistoricalDate.Create("c. 1650", 1645, 1652);
        Assert.Equal(1645, date.EarliestYear);
        Assert.Equal(1652, date.LatestYear);
        Assert.Equal(HistoricalDatePrecision.Approximate, date.Precision);
    }

    [Fact]
    public void Explicit_range_must_be_ordered()
    {
        Assert.Throws<DomainValidationException>(() => HistoricalDate.Create("odd", 1700, 1600));
    }

    [Fact]
    public void Sort_year_uses_latest_when_start_is_open()
    {
        Assert.Equal(1719, HistoricalDate.Create("before 1720").SortYear);
        Assert.Equal(1601, HistoricalDate.Create("17th century").SortYear);
    }
}
