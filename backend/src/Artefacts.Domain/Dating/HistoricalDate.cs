using Artefacts.Domain.Common;

namespace Artefacts.Domain.Dating;

public enum HistoricalDatePrecision
{
    Year = 0,
    Decade = 1,
    Century = 2,
    Approximate = 3,
}

/// <summary>
/// An imprecise historical date as curators write it ("c. 1650", "late 17th century"), kept as the
/// original display text plus a computed year range (for filtering and sorting) and a precision.
/// Either bound may be open (null), e.g. "before 1720" has no earliest year.
/// </summary>
public sealed class HistoricalDate : IEquatable<HistoricalDate>
{
    public const int MaxDisplayLength = 200;

    public string Display { get; private set; } = default!;
    public int? EarliestYear { get; private set; }
    public int? LatestYear { get; private set; }
    public HistoricalDatePrecision Precision { get; private set; }

    // For EF Core.
    private HistoricalDate()
    {
    }

    private HistoricalDate(string display, int? earliest, int? latest, HistoricalDatePrecision precision)
    {
        Display = display;
        EarliestYear = earliest;
        LatestYear = latest;
        Precision = precision;
    }

    /// <summary>
    /// Interprets <paramref name="text"/>. When <paramref name="earliestYear"/> or <paramref name="latestYear"/>
    /// is given, the explicit range overrides the interpretation; text that cannot be interpreted is only
    /// accepted together with both explicit years.
    /// </summary>
    /// <exception cref="DomainValidationException">Keyed by the empty field name; callers prefix it.</exception>
    public static HistoricalDate Create(string? text, int? earliestYear = null, int? latestYear = null)
    {
        var display = Normalize(text);
        if (display.Length == 0)
            throw new DomainValidationException("", "A date is required.");
        if (display.Length > MaxDisplayLength)
            throw new DomainValidationException("", $"Must be at most {MaxDisplayLength} characters.");

        var parsed = HistoricalDateParser.TryParse(display);

        if (earliestYear is not null || latestYear is not null)
        {
            if (parsed is null && (earliestYear is null || latestYear is null))
                throw new DomainValidationException("",
                    "This text could not be interpreted; enter both an earliest and a latest year.");
            ValidateYear(earliestYear);
            ValidateYear(latestYear);
            if (earliestYear > latestYear)
                throw new DomainValidationException("", "The earliest year must not be after the latest year.");
            return new HistoricalDate(display, earliestYear, latestYear,
                parsed?.Precision ?? HistoricalDatePrecision.Approximate);
        }

        if (parsed is null)
            throw new DomainValidationException("",
                $"\"{display}\" could not be interpreted. Use forms like 1654, c. 1650, 1680s, " +
                "17th century, late 17th century, XV secolo, seconda metà XV, 1650-1660, before 1720, " +
                "or enter an explicit range.");

        return new HistoricalDate(display, parsed.Earliest, parsed.Latest, parsed.Precision);
    }

    /// <summary>The key used for chronological sorting (earliest year, or latest when the start is open).</summary>
    public int? SortYear => EarliestYear ?? LatestYear;

    private static void ValidateYear(int? year)
    {
        if (year is < HistoricalDateParser.MinYear or > HistoricalDateParser.MaxYear)
            throw new DomainValidationException("",
                $"Years must be between {HistoricalDateParser.MinYear} and {HistoricalDateParser.MaxYear}.");
    }

    private static string Normalize(string? text) =>
        string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public bool Equals(HistoricalDate? other) =>
        other is not null && Display == other.Display && EarliestYear == other.EarliestYear &&
        LatestYear == other.LatestYear && Precision == other.Precision;

    public override bool Equals(object? obj) => Equals(obj as HistoricalDate);
    public override int GetHashCode() => HashCode.Combine(Display, EarliestYear, LatestYear, Precision);
    public override string ToString() => Display;
}
