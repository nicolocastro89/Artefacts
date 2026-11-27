using System.Globalization;
using System.Text.RegularExpressions;
using Artefacts.Domain.Dating;

namespace Artefacts.Application.Imports;

/// <summary>
/// A recognised line: the recognised text, the parsed value when there is one, and the text left over (before and
/// after the match) for further rules. <see cref="Guessed"/> marks a parse that relied on a convention, such as a
/// single separator followed by three digits read as thousands.
/// </summary>
public sealed record Detection(string Text, object? Parsed = null, string? Remainder = null, string? ItemNumber = null, bool Guessed = false);

/// <summary>An amount of money read from text.</summary>
public sealed record ParsedMoney(decimal Amount, string Currency);

/// <summary>A historical date read from text: its display text and the interpretation, when the rules have one.</summary>
public sealed record ParsedDateText(string Text, ParsedHistoricalDate? Interpretation);

/// <summary>The line recognisers used by column line rules. Pure functions, so preview, review and import agree.</summary>
public static partial class Detectors
{
    public static readonly IReadOnlyList<string> DefaultLocationPatterns =
    [
        @"^(MI|GE|FI|RM|TO|VE|MO|BO)(\s+[A-Z]?\d+[A-Z]?)?$",
        @"^cella\s*\d+$",
        @"^Beta\s*\d+$",
    ];

    // The per-kind recognisers below are registered as detectors in DetectorRegistry.BuiltIn.

    public static Detection? GroupNumber(string line) =>
        GroupNumberRegex().IsMatch(line) ? new Detection(line.Trim().TrimEnd('.').Trim()) : null;

    public static Detection? Dimensions(string line) => IsDimensions(line) ? new Detection(line.Trim()) : null;

    // ---------- item title: "01. Coppa Celadon", "02a Frammento", "01.Corano" ----------

    public static Detection? ItemTitle(string line)
    {
        var m = ItemTitleRegex().Match(line.Trim());
        // "27.5 x 30" and "3 x 4" are measurements, not item 27 or 3x; "05. 2 piastrelle" is item 5.
        if (!m.Success || IsDimensions(line)) return null;
        return new Detection(m.Groups["title"].Value.Trim(), ItemNumber: m.Groups["n"].Value + m.Groups["l"].Value.ToLowerInvariant());
    }

    // ---------- dimensions ----------

    public static bool IsDimensions(string line)
    {
        var s = line.Trim();
        return TimesRegex().IsMatch(s) || MeasureWordRegex().IsMatch(s) || CentimetreRegex().IsMatch(s);
    }

    // ---------- historical dates ----------

    /// <summary>
    /// A line the date rules interpret, or one that looks like a date they do not know (it starts with a number, a
    /// Roman numeral or a date word, or is a short line with a year in it), so that it reaches the dates step.
    /// </summary>
    public static Detection? HistoricalDate(string line)
    {
        var text = line.Trim();
        var parsed = HistoricalDateParser.TryParse(text);
        if (parsed is null && !LooksLikeDate(text)) return null;
        return new Detection(text, new ParsedDateText(text, parsed));
    }

    public static bool LooksLikeDate(string text)
    {
        if (DateStartRegex().IsMatch(text)) return true;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return words <= 4 && YearInTextRegex().IsMatch(text);
    }

    // ---------- calendar dates ----------

    public static Detection? CalendarDate(string line, LookupTables? tables = null)
    {
        tables ??= LookupTables.BuiltIn;
        foreach (Match m in NumericDateRegex().Matches(line))
        {
            if (ToDate(m.Groups["d"].Value, int.Parse(m.Groups["m"].Value), m.Groups["y"].Value) is { } date)
                return Found(line, m, date);
        }

        foreach (Match m in NamedMonthDateRegex().Matches(line))
        {
            if (tables.Month(m.Groups["mon"].Value.TrimEnd('.')) is { } month &&
                ToDate(m.Groups["d"].Value, month, m.Groups["y"].Value) is { } date)
                return Found(line, m, date);
        }

        return null;
    }

    private static DateOnly? ToDate(string day, int month, string year)
    {
        if (!int.TryParse(day, out var d) || !int.TryParse(year, out var y)) return null;
        if (year.Length == 2)
        {
            // Two-digit years: up to this year's last two digits are this century, otherwise the last one.
            var pivot = DateTime.UtcNow.Year % 100;
            y += y <= pivot ? 2000 : 1900;
        }

        if (month is < 1 or > 12 || y is < 1 or > 9999 || d < 1 || d > DateTime.DaysInMonth(y, month)) return null;
        return new DateOnly(y, month, d);
    }

    // ---------- lot ----------

    public static Detection? Lot(string line)
    {
        var m = LotRegex().Match(line);
        return m.Success ? Found(line, m, null, m.Groups["n"].Value) : null;
    }

    // ---------- money ----------

    public static Detection? Money(string line, LookupTables? tables = null)
    {
        tables ??= LookupTables.BuiltIn;
        var m = tables.MoneyBefore.Match(line);
        if (!m.Success) m = tables.MoneyAfter.Match(line);
        if (!m.Success) return null;
        if (tables.Currency(m.Groups["cur"].Value.TrimEnd('.')) is not { } currency) return null;
        var amount = ParseAmount(m.Groups["amt"].Value, out var guessed);
        return amount is null ? null : Found(line, m, new ParsedMoney(amount.Value, currency), guessed: guessed);
    }

    /// <summary>
    /// Reads an amount with mixed separators. When both "." and "," appear, the last one is the decimal separator.
    /// When only one kind appears and every occurrence is followed by exactly three digits, it separates thousands;
    /// a single such separator is a guess ("7.865" is 7865). Otherwise it is the decimal separator.
    /// </summary>
    public static decimal? ParseAmount(string text, out bool guessed)
    {
        guessed = false;
        var s = text.Replace(" ", "").Replace("'", "").Replace(" ", "");
        var lastDot = s.LastIndexOf('.');
        var lastComma = s.LastIndexOf(',');
        string normalised;
        if (lastDot >= 0 && lastComma >= 0)
        {
            var (decimalSep, thousandsSep) = lastDot > lastComma ? ('.', ',') : (',', '.');
            normalised = s.Replace(thousandsSep.ToString(), "").Replace(decimalSep, '.');
        }
        else if (lastDot >= 0 || lastComma >= 0)
        {
            var sep = lastDot >= 0 ? '.' : ',';
            var groups = s.Split(sep);
            var thousands = groups.Skip(1).All(g => g.Length == 3) && groups[0].Length is >= 1 and <= 3;
            if (thousands)
            {
                guessed = groups.Length == 2;
                normalised = string.Concat(groups);
            }
            else
            {
                if (groups.Length > 2) return null;
                normalised = s.Replace(sep, '.');
            }
        }
        else
        {
            normalised = s;
        }

        return decimal.TryParse(normalised, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    // ---------- catalogue number and location codes ----------

    public static Detection? CatalogueNumber(string line)
    {
        var m = CatalogueNumberRegex().Match(line.Trim());
        return m.Success ? new Detection($"{m.Groups["p"].Value}.{m.Groups["n"].Value}") : null;
    }

    public static Detection? LocationCode(string line, IReadOnlyList<string> patterns)
    {
        var text = line.Trim();
        foreach (var pattern in patterns)
        {
            try
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                    return new Detection(text);
            }
            catch (ArgumentException)
            {
                // An invalid pattern never matches; the mapping validation reports it.
            }
        }

        return null;
    }

    public static bool IsValidPattern(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // ---------- helpers ----------

    /// <summary>What is left of a line outside a match, tidied.</summary>
    internal static string RemainderOf(string line, Match m) => Tidy(line[..m.Index] + " " + line[(m.Index + m.Length)..]);

    private static Detection Found(string line, Match m, object? parsed, string? text = null, bool guessed = false)
    {
        var remainder = RemainderOf(line, m);
        return new Detection(text ?? m.Value.Trim(), parsed, remainder.Length == 0 ? null : remainder, Guessed: guessed);
    }

    /// <summary>Collapses whitespace and removes punctuation left dangling at either end.</summary>
    public static string Tidy(string text) => DanglingRegex().Replace(WhitespaceRegex().Replace(text, " "), "").Trim();

    // "01. Coppa", "19, frammento", "01-B3A mamelucco", "02a Frammento", "29b-B20C sezione", "03 b. coppa",
    // and "08 Firmano" (a number, a space and a word).
    [GeneratedRegex(@"^(?<n>\d{1,3})(?:\s*[.,]\s*|-\s*|\s?(?<l>[a-zA-Z])(?:\s*[.-]\s*|\s+)|\s+(?=\p{L}))(?<title>\S.*)$")]
    private static partial Regex ItemTitleRegex();

    [GeneratedRegex(@"^\s*\d{1,3}\s*\.?\s*$")]
    private static partial Regex GroupNumberRegex();

    [GeneratedRegex(@"\d+(?:[.,]\d+)?\s*(?:cm\.?)?\s*[x×]\s*(?:cm\.?\s*)?\d", RegexOptions.IgnoreCase)]
    private static partial Regex TimesRegex();

    [GeneratedRegex(@"^(?:cm\.?|diam\.?|diametro|h\.?|alt\.?|altezza|larg\.?|larghezza|lungh?\.?|lunghezza|chiuso|aperto|montato)\s*\d", RegexOptions.IgnoreCase)]
    private static partial Regex MeasureWordRegex();

    [GeneratedRegex(@"^\d+(?:[.,]\d+)?\s*cm\b", RegexOptions.IgnoreCase)]
    private static partial Regex CentimetreRegex();

    [GeneratedRegex(@"^\(?\s*(?:\d{1,4}\b|[ivx]{1,6}\b|datato|dated|ah\b|circa\b|c\.|ca\.|inizio|fine\b|met[àa]\b|prima\s+met|seconda\s+met|tardo|ultimo|early|mid\b|late\b|before|after|secolo|sec\b)", RegexOptions.IgnoreCase)]
    private static partial Regex DateStartRegex();

    [GeneratedRegex(@"(?<![\d.,])\d{3,4}(?![\d.,]\d)")]
    private static partial Regex YearInTextRegex();

    [GeneratedRegex(@"(?<!\d)(?<d>\d{1,2})[\s./-]+(?<m>\d{1,2})[\s./-]+(?<y>\d{4}|\d{2})(?![\d])")]
    private static partial Regex NumericDateRegex();

    [GeneratedRegex(@"(?<!\d)(?<d>\d{1,2})\s+(?<mon>[a-zA-Z]{3,9}\.?)\s+(?<y>\d{4})(?!\d)")]
    private static partial Regex NamedMonthDateRegex();

    [GeneratedRegex(@"\b(?:lotto|lot)\b\.?\s*(?:n[.°]?\s*)?(?<n>\d+[a-zA-Z]?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LotRegex();

    [GeneratedRegex(@"^(?<p>[A-Z]{1,3})\.\s?(?<n>\d{1,5}[a-z]?)$")]
    private static partial Regex CatalogueNumberRegex();

    [GeneratedRegex(@"^[\s,;:.\-–/]+|[\s,;:\-–/]+$")]
    private static partial Regex DanglingRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
