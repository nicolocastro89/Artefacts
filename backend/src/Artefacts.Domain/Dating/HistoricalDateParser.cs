using System.Globalization;
using System.Text.RegularExpressions;

namespace Artefacts.Domain.Dating;

/// <summary>Result of interpreting historical date text.</summary>
public sealed record ParsedHistoricalDate(int? Earliest, int? Latest, HistoricalDatePrecision Precision);

/// <summary>
/// Interprets the common curatorial date forms, in English and in the Italian forms used by the collection's
/// workbooks. Conventions (kept in one place so they are easy to adjust):
/// <list type="bullet">
/// <item>circa = ±<see cref="CircaSpan"/> years, written before or after the year ("c. 1650", "1470 circa");</item>
/// <item>early/mid/late and inizio/metà/fine split a century into thirds; prima/seconda metà split it in halves;</item>
/// <item>centuries are ordinal ("17th century") or Roman numerals, with or without "secolo"/"sec." ("XV secolo");</item>
/// <item>two centuries or century parts form a span ("XI-XII", "IX/X", "XV XVI", "fine XII inizio XIII");</item>
/// <item>a shortened second year takes the first year's leading digits ("1450 - 75" is 1450 to 1475);</item>
/// <item>a century followed by a year expression, bracketed or not, is refined to that expression ("XV (1440-1446)").</item>
/// </list>
/// </summary>
public static partial class HistoricalDateParser
{
    public const int MinYear = 1;
    public const int MaxYear = 9999;
    public const int CircaSpan = 10;

    public static ParsedHistoricalDate? TryParse(string text)
    {
        var s = Clean(text);
        if (s.Length == 0) return null;

        return YearExpression(s)
               ?? CenturyExpression(s)
               ?? Refined(s)
               ?? Bounded(s);
    }

    private static string Clean(string text) =>
        WhitespaceRegex().Replace(text.Trim().ToLowerInvariant().Replace('–', '-').Replace('—', '-'), " ");

    // ---------- years: 1654, c. 1650, 1470 circa, 1680s, 1650-1660, 1450 - 75 ----------

    private static ParsedHistoricalDate? YearExpression(string s)
    {
        Match m;
        if ((m = YearRegex().Match(s)).Success)
        {
            var y = Year(m.Groups["y"].Value);
            return y is null ? null : new(y, y, HistoricalDatePrecision.Year);
        }

        if ((m = CircaRegex().Match(s)).Success || (m = CircaAfterRegex().Match(s)).Success)
        {
            var y = Year(m.Groups["y"].Value);
            return y is null
                ? null
                : new(Math.Max(MinYear, y.Value - CircaSpan), Math.Min(MaxYear, y.Value + CircaSpan),
                    HistoricalDatePrecision.Approximate);
        }

        if ((m = DecadeRegex().Match(s)).Success)
        {
            var y = Year(m.Groups["y"].Value);
            return y is null || y % 10 != 0 ? null : new(y, y + 9, HistoricalDatePrecision.Decade);
        }

        if ((m = RangeRegex().Match(s)).Success)
        {
            var fromText = m.Groups["from"].Value;
            var toText = m.Groups["to"].Value;
            // A shortened second year takes the leading digits of the first: 1450-75, 1463-4.
            if (toText.Length < fromText.Length)
                toText = fromText[..^toText.Length] + toText;
            var from = Year(fromText);
            var to = Year(toText);
            return from is null || to is null || from > to ? null : new(from, to, HistoricalDatePrecision.Year);
        }

        return null;
    }

    // ---------- centuries: 17th century, late 17th century, XV secolo, seconda metà XV, XI-XII ----------

    private static ParsedHistoricalDate? CenturyExpression(string s)
    {
        Match m;
        if ((m = OrdinalCenturyRegex().Match(s)).Success)
        {
            var c = OrdinalCentury(m.Groups["c"].Value, m.Groups["suffix"].Value);
            var range = c is null ? null : Part(c.Value, m.Groups["part"].Value);
            return range is { } r ? new(r.From, r.To, HistoricalDatePrecision.Century) : null;
        }

        if ((m = RomanCenturyRegex().Match(s)).Success)
        {
            var first = RomanPart(m.Groups["p1"].Value, m.Groups["n1"].Value);
            if (first is null) return null;
            if (!m.Groups["n2"].Success)
                return new(first.Value.From, first.Value.To, HistoricalDatePrecision.Century);

            var second = RomanPart(m.Groups["p2"].Value, m.Groups["n2"].Value);
            if (second is null || second.Value.To < first.Value.From) return null;
            return new(first.Value.From, second.Value.To, HistoricalDatePrecision.Century);
        }

        return null;
    }

    /// <summary>A century followed by a year expression refines it: "XV (1440-1446)", "XV 1450".</summary>
    private static ParsedHistoricalDate? Refined(string s)
    {
        var m = RefinedRegex().Match(s);
        if (!m.Success) return null;
        var century = CenturyExpression(m.Groups["c"].Value.Trim());
        if (century is null) return null;
        var years = m.Groups["b"].Success ? m.Groups["b"].Value : m.Groups["y"].Value;
        return YearExpression(Clean(years.Replace('/', '-')));
    }

    private static ParsedHistoricalDate? Bounded(string s)
    {
        Match m;
        if ((m = BeforeRegex().Match(s)).Success)
        {
            var y = Year(m.Groups["y"].Value);
            return y is null or <= MinYear ? null : new(null, y - 1, HistoricalDatePrecision.Approximate);
        }

        if ((m = AfterRegex().Match(s)).Success)
        {
            var y = Year(m.Groups["y"].Value);
            return y is null or >= MaxYear ? null : new(y + 1, null, HistoricalDatePrecision.Approximate);
        }

        return null;
    }

    private static (int From, int To)? Part(int century, string part)
    {
        var start = (century - 1) * 100 + 1;
        return part switch
        {
            "" => (start, start + 99),
            "early" or "inizio" => (start, start + 32),
            "mid" or "metà" or "meta" => (start + 33, start + 65),
            "late" or "fine" or "tardo" => (start + 66, start + 99),
            "prima metà" or "prima meta" => (start, start + 49),
            "seconda metà" or "seconda meta" => (start + 50, start + 99),
            _ => null,
        };
    }

    private static (int From, int To)? RomanPart(string part, string numeral) =>
        Roman(numeral) is { } c ? Part(c, WhitespaceRegex().Replace(part.Trim(), " ")) : null;

    private static int? Year(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var y) && y is >= MinYear and <= MaxYear
            ? y
            : null;

    private static int? OrdinalCentury(string number, string suffix)
    {
        if (!int.TryParse(number, out var c) || c is < 1 or > 99) return null;
        var expected = (c % 100) is 11 or 12 or 13
            ? "th"
            : (c % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return suffix == expected ? c : null;
    }

    /// <summary>Roman numerals I to XXXIX, in canonical form only.</summary>
    private static int? Roman(string numeral)
    {
        if (!CanonicalRomanRegex().IsMatch(numeral)) return null;
        int value = 0, previous = 0;
        for (var i = numeral.Length - 1; i >= 0; i--)
        {
            var v = numeral[i] switch { 'i' => 1, 'v' => 5, 'x' => 10, _ => 0 };
            value += v < previous ? -v : v;
            previous = Math.Max(previous, v);
        }

        return value is >= 1 and <= 39 ? value : null;
    }

    private const string RomanPartPattern = @"(?:prima\s+met[àa]|seconda\s+met[àa]|inizio|met[àa]|fine|tardo)";
    private const string CenturyWord = @"(?:\s*(?:secolo|sec\.?))?";

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^(?<y>\d{1,4})$")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"^(?:c|ca|circa)\.?\s*(?<y>\d{1,4})$")]
    private static partial Regex CircaRegex();

    [GeneratedRegex(@"^(?<y>\d{1,4})\s+(?:circa|ca\.?|c\.)$")]
    private static partial Regex CircaAfterRegex();

    [GeneratedRegex(@"^(?<y>\d{2,4})'?s$")]
    private static partial Regex DecadeRegex();

    [GeneratedRegex(@"^(?:(?<part>early|mid|late)[\s-]+)?(?<c>\d{1,2})(?<suffix>st|nd|rd|th)\s+century$")]
    private static partial Regex OrdinalCenturyRegex();

    [GeneratedRegex(
        @"^(?:(?<p1>" + RomanPartPattern + @")\s*)?(?<n1>[ivx]+)" + CenturyWord +
        @"(?:(?:\s*[-/]\s*|\s+)(?:(?<p2>" + RomanPartPattern + @")\s*)?(?<n2>[ivx]+)" + CenturyWord + @")?$")]
    private static partial Regex RomanCenturyRegex();

    [GeneratedRegex(@"^(?<c>.*?[a-zà.])\s*(?:\((?<b>[^()]+)\)|\s(?<y>\d[\d\s\-]*(?:\s*circa)?))$")]
    private static partial Regex RefinedRegex();

    [GeneratedRegex(@"^x{0,3}(?:ix|iv|v?i{0,3})$")]
    private static partial Regex CanonicalRomanRegex();

    [GeneratedRegex(@"^(?<from>\d{1,4})\s*-\s*(?<to>\d{1,4})$")]
    private static partial Regex RangeRegex();

    [GeneratedRegex(@"^before\s+(?<y>\d{1,4})$")]
    private static partial Regex BeforeRegex();

    [GeneratedRegex(@"^after\s+(?<y>\d{1,4})$")]
    private static partial Regex AfterRegex();
}
