using System.Text.RegularExpressions;

namespace Artefacts.Domain.Dating;

/// <summary>Result of interpreting historical date text.</summary>
public sealed record ParsedHistoricalDate(int? Earliest, int? Latest, HistoricalDatePrecision Precision);

/// <summary>
/// Interprets the common curatorial date forms. Conventions (kept in one place so they are easy to adjust):
/// circa = ±<see cref="CircaSpan"/> years; early/mid/late split a century into thirds.
/// </summary>
public static partial class HistoricalDateParser
{
    public const int MinYear = 1;
    public const int MaxYear = 9999;
    public const int CircaSpan = 10;

    public static ParsedHistoricalDate? TryParse(string text)
    {
        var s = text.Trim().ToLowerInvariant().Replace('–', '-').Replace('—', '-');

        Match m;
        if ((m = YearRegex().Match(s)).Success)
        {
            var y = Year(m.Groups["y"]);
            return y is null ? null : new(y, y, HistoricalDatePrecision.Year);
        }

        if ((m = CircaRegex().Match(s)).Success)
        {
            var y = Year(m.Groups["y"]);
            return y is null
                ? null
                : new(Math.Max(MinYear, y.Value - CircaSpan), Math.Min(MaxYear, y.Value + CircaSpan),
                    HistoricalDatePrecision.Approximate);
        }

        if ((m = DecadeRegex().Match(s)).Success)
        {
            var y = Year(m.Groups["y"]);
            return y is null || y % 10 != 0 ? null : new(y, y + 9, HistoricalDatePrecision.Decade);
        }

        if ((m = CenturyRegex().Match(s)).Success)
        {
            var c = Century(m.Groups["c"], m.Groups["suffix"]);
            if (c is null) return null;
            var start = (c.Value - 1) * 100 + 1;
            var (from, to) = m.Groups["part"].Value switch
            {
                "early" => (start, start + 32),
                "mid" => (start + 33, start + 65),
                "late" => (start + 66, start + 99),
                _ => (start, start + 99),
            };
            return new(from, to, HistoricalDatePrecision.Century);
        }

        if ((m = RangeRegex().Match(s)).Success)
        {
            var from = Year(m.Groups["from"]);
            var to = Year(m.Groups["to"]);
            return from is null || to is null || from > to ? null : new(from, to, HistoricalDatePrecision.Year);
        }

        if ((m = BeforeRegex().Match(s)).Success)
        {
            var y = Year(m.Groups["y"]);
            return y is null or <= MinYear ? null : new(null, y - 1, HistoricalDatePrecision.Approximate);
        }

        if ((m = AfterRegex().Match(s)).Success)
        {
            var y = Year(m.Groups["y"]);
            return y is null or >= MaxYear ? null : new(y + 1, null, HistoricalDatePrecision.Approximate);
        }

        return null;
    }

    private static int? Year(Group g) =>
        int.TryParse(g.Value, out var y) && y is >= MinYear and <= MaxYear ? y : null;

    private static int? Century(Group number, Group suffix)
    {
        if (!int.TryParse(number.Value, out var c) || c is < 1 or > 99) return null;
        var expected = (c % 100) is 11 or 12 or 13
            ? "th"
            : (c % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return suffix.Value == expected ? c : null;
    }

    [GeneratedRegex(@"^(?<y>\d{1,4})$")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"^(?:c|ca|circa)\.?\s*(?<y>\d{1,4})$")]
    private static partial Regex CircaRegex();

    [GeneratedRegex(@"^(?<y>\d{2,4})'?s$")]
    private static partial Regex DecadeRegex();

    [GeneratedRegex(@"^(?:(?<part>early|mid|late)[\s-]+)?(?<c>\d{1,2})(?<suffix>st|nd|rd|th)\s+century$")]
    private static partial Regex CenturyRegex();

    [GeneratedRegex(@"^(?<from>\d{1,4})\s*-\s*(?<to>\d{1,4})$")]
    private static partial Regex RangeRegex();

    [GeneratedRegex(@"^before\s+(?<y>\d{1,4})$")]
    private static partial Regex BeforeRegex();

    [GeneratedRegex(@"^after\s+(?<y>\d{1,4})$")]
    private static partial Regex AfterRegex();
}
