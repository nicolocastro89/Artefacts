using System.Text.RegularExpressions;

namespace Artefacts.Application.Imports;

/// <summary>
/// A table of words the parsers look up: month names, currency words, measurement labels, spellings of the inch. Keys
/// are matched ignoring case; values are text whose meaning depends on the table (a month number, a currency code, a
/// measure kind). Built-in tables ship in code; user tables extend or override them entry by entry (see
/// <see cref="LookupTables.With"/>).
/// </summary>
public sealed record LookupTable(string Id, string Label, IReadOnlyDictionary<string, string> Entries)
{
    public LookupTable(string id, string label, params (string Word, string Value)[] entries)
        : this(id, label, entries.ToDictionary(e => e.Word, e => e.Value, StringComparer.OrdinalIgnoreCase))
    {
    }

    public string? Find(string word) => Entries.TryGetValue(word, out var value) ? value : null;
}

/// <summary>The identifiers of the built-in lookup tables, as stored when users extend them.</summary>
public static class LookupTableIds
{
    /// <summary>Month name or abbreviation to month number (1-12).</summary>
    public const string Months = "months";

    /// <summary>Words and symbols that can come before an amount ("£ 7.865", "lire 50.000") to ISO currency code.</summary>
    public const string CurrenciesBeforeAmount = "currenciesBeforeAmount";

    /// <summary>Words and symbols that can come after an amount ("100 euro") to ISO currency code.</summary>
    public const string CurrenciesAfterAmount = "currenciesAfterAmount";

    /// <summary>Label written before a measurement ("h", "diam") to the measure kind it names.</summary>
    public const string MeasureLabels = "measureLabels";

    /// <summary>Further spellings of the inch, accepted when a unit with the abbreviation "in" exists (value: "in").</summary>
    public const string InchAliases = "inchAliases";
}

/// <summary>
/// The lookup tables parsing reads, by identifier, with typed access. Pure and immutable: a registry with user
/// entries is a new one made by <see cref="With"/>. The currency regexes are built from the currency tables once per
/// registry, so a user's currency word is recognised like a built-in one.
/// </summary>
public sealed class LookupTables
{
    private readonly Dictionary<string, LookupTable> _byId;
    private readonly Lazy<Regex> _moneyBefore;
    private readonly Lazy<Regex> _moneyAfter;

    public LookupTables(IEnumerable<LookupTable> tables)
    {
        Tables = tables.ToList();
        _byId = Tables.ToDictionary(t => t.Id, StringComparer.Ordinal);
        _moneyBefore = new Lazy<Regex>(() => BuildMoneyBefore(Get(LookupTableIds.CurrenciesBeforeAmount)));
        _moneyAfter = new Lazy<Regex>(() => BuildMoneyAfter(Get(LookupTableIds.CurrenciesAfterAmount)));
    }

    public IReadOnlyList<LookupTable> Tables { get; }

    /// <summary>The tables that ship with the application.</summary>
    public static LookupTables BuiltIn { get; } = new(
    [
        new LookupTable(LookupTableIds.Months, "Month names",
            ("gennaio", "1"), ("febbraio", "2"), ("marzo", "3"), ("aprile", "4"), ("maggio", "5"), ("giugno", "6"),
            ("luglio", "7"), ("agosto", "8"), ("settembre", "9"), ("ottobre", "10"), ("novembre", "11"), ("dicembre", "12"),
            ("january", "1"), ("february", "2"), ("march", "3"), ("april", "4"), ("may", "5"), ("june", "6"),
            ("july", "7"), ("august", "8"), ("september", "9"), ("october", "10"), ("november", "11"), ("december", "12"),
            ("gen", "1"), ("feb", "2"), ("mar", "3"), ("apr", "4"), ("mag", "5"), ("giu", "6"), ("lug", "7"), ("ago", "8"),
            ("set", "9"), ("ott", "10"), ("nov", "11"), ("dic", "12"), ("jan", "1"), ("jun", "6"), ("jul", "7"), ("aug", "8"),
            ("sep", "9"), ("sept", "9"), ("oct", "10"), ("dec", "12")),
        new LookupTable(LookupTableIds.CurrenciesBeforeAmount, "Currency words before an amount",
            ("£", "GBP"), ("€", "EUR"), ("$", "USD"), ("us$", "USD"),
            ("lire", "ITL"), ("lira", "ITL"), ("lit", "ITL"), ("l", "ITL"), ("itl", "ITL"),
            ("fr", "FRF"), ("frs", "FRF"), ("frf", "FRF"), ("chf", "CHF"), ("dm", "DEM"),
            ("eur", "EUR"), ("euro", "EUR"), ("gbp", "GBP"), ("usd", "USD")),
        new LookupTable(LookupTableIds.CurrenciesAfterAmount, "Currency words after an amount",
            ("€", "EUR"), ("£", "GBP"), ("$", "USD"), ("euro", "EUR"), ("eur", "EUR"), ("lire", "ITL"), ("gbp", "GBP"), ("usd", "USD"), ("chf", "CHF")),
        new LookupTable(LookupTableIds.MeasureLabels, "Measurement labels",
            ("h", "height"), ("alt", "height"), ("altezza", "height"), ("height", "height"),
            ("l", "length"), ("lung", "length"), ("lungh", "length"), ("lunghezza", "length"), ("length", "length"),
            ("w", "width"), ("larg", "width"), ("largh", "width"), ("larghezza", "width"), ("width", "width"),
            ("diam", "diameter"), ("diametro", "diameter"), ("diameter", "diameter"), ("ø", "diameter")),
        new LookupTable(LookupTableIds.InchAliases, "Spellings of the inch",
            ("\"", "in"), ("''", "in"), ("inch", "in"), ("inches", "in"), ("pollice", "in"), ("pollici", "in")),
    ]);

    /// <summary>The table with this identifier; a table the registry does not have is empty.</summary>
    public LookupTable Get(string id) => _byId.GetValueOrDefault(id) ?? new LookupTable(id, id, new Dictionary<string, string>());

    /// <summary>
    /// These tables with the entries of <paramref name="user"/> laid over them: per table, user entries are added and
    /// win over a built-in entry with the same word; tables only one side has are kept.
    /// </summary>
    public LookupTables With(IEnumerable<LookupTable> user)
    {
        var merged = Tables.ToDictionary(t => t.Id, t => new Dictionary<string, string>(t.Entries, StringComparer.OrdinalIgnoreCase), StringComparer.Ordinal);
        var labels = Tables.ToDictionary(t => t.Id, t => t.Label, StringComparer.Ordinal);
        foreach (var table in user)
        {
            if (!merged.TryGetValue(table.Id, out var entries)) merged[table.Id] = entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            labels.TryAdd(table.Id, table.Label);
            foreach (var (word, value) in table.Entries) entries[word] = value;
        }

        return new LookupTables(merged.Select(t => new LookupTable(t.Key, labels[t.Key], t.Value)));
    }

    public const int MaxWordLength = 40;

    /// <summary>Why a word cannot be an entry of any table, or null.</summary>
    public static string? WordProblem(string word) =>
        word.Length == 0 ? "Enter a word."
        : word.Length > MaxWordLength ? $"At most {MaxWordLength} characters."
        : word.Any(char.IsControl) ? "A word cannot contain line breaks or control characters."
        : null;

    /// <summary>Why this value does not fit the table (a month is 1-12, a currency a 3-letter code, a label a measure kind), or null.</summary>
    public static string? Problem(string tableId, string word, string value) => tableId switch
    {
        LookupTableIds.Months => int.TryParse(value, out var month) && month is >= 1 and <= 12 ? null : "A month number from 1 to 12.",
        LookupTableIds.CurrenciesBeforeAmount or LookupTableIds.CurrenciesAfterAmount =>
            LookupTableRules.CurrencyCode().IsMatch(value) ? null : "A currency code of three capital letters, such as EUR.",
        LookupTableIds.MeasureLabels => Enum.GetNames<MeasureKind>().Contains(value, StringComparer.OrdinalIgnoreCase) ? null : "One of width, height, length or diameter.",
        LookupTableIds.InchAliases => value == "in" ? null : "The value is \"in\".",
        _ => "Unknown lookup table.",
    };

    /// <summary>The month number (1-12) of a month name or abbreviation.</summary>
    public int? Month(string word) =>
        Get(LookupTableIds.Months).Find(word) is { } value && int.TryParse(value, out var month) && month is >= 1 and <= 12 ? month : null;

    /// <summary>The ISO currency code of a currency word or symbol.</summary>
    public string? Currency(string word) =>
        Get(LookupTableIds.CurrenciesBeforeAmount).Find(word) ?? Get(LookupTableIds.CurrenciesAfterAmount).Find(word);

    /// <summary>The measure a label names ("h" is the height).</summary>
    public MeasureKind? MeasureLabel(string word) =>
        Get(LookupTableIds.MeasureLabels).Find(word) is { } value && Enum.TryParse<MeasureKind>(value, true, out var kind) ? kind : null;

    /// <summary>Spellings of the inch, for a unit with the abbreviation "in".</summary>
    public IEnumerable<string> InchAliases => Get(LookupTableIds.InchAliases).Entries.Keys;

    /// <summary>An amount with its currency before it: "£ 7.865", "lire 50.000", "L. 1.000".</summary>
    public Regex MoneyBefore => _moneyBefore.Value;

    /// <summary>An amount with its currency after it: "100 euro", "7.865 £".</summary>
    public Regex MoneyAfter => _moneyAfter.Value;

    private const string Amount = @"\d{1,3}(?:[.,' ]\d{3})+(?:[.,]\d{1,2})?|\d+(?:[.,]\d{1,2})?";

    private static Regex BuildMoneyBefore(LookupTable currencies) =>
        new($@"(?<cur>{Currency(currencies, trailingDot: true)})\s*(?<amt>{Amount})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static Regex BuildMoneyAfter(LookupTable currencies) =>
        new($@"(?<![\d.,])(?<amt>{Amount})\s*(?<cur>{Currency(currencies, trailingDot: false)})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// The alternatives for the currency words: symbols and other non-letter spellings as written, whole words between
    /// word boundaries (with an optional full stop where <paramref name="trailingDot"/>), and a single letter ("L.")
    /// only with its full stop. Never matches when the table is empty.
    /// </summary>
    private static string Currency(LookupTable currencies, bool trailingDot)
    {
        var keys = currencies.Entries.Keys.Where(k => k.Length > 0).OrderByDescending(k => k.Length).ThenBy(k => k, StringComparer.Ordinal).ToList();
        var alternatives = keys.Where(k => !k.All(char.IsLetter)).Select(Regex.Escape).ToList();
        var words = keys.Where(k => k.Length > 1 && k.All(char.IsLetter)).Select(Regex.Escape).ToList();
        if (words.Count > 0) alternatives.Add($@"\b(?:{string.Join('|', words)})\b{(trailingDot ? @"\.?" : "")}");
        foreach (var letter in keys.Where(k => k.Length == 1 && char.IsLetter(k[0]))) alternatives.Add($@"\b{Regex.Escape(letter)}\.");
        return alternatives.Count == 0 ? "(?!)" : string.Join('|', alternatives);
    }
}
