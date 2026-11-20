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
