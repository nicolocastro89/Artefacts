using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;

namespace Artefacts.Domain.Measures;

/// <summary>
/// A unit of length used for artefact measurements (e.g. Millimetre, "mm"), with its name per content language, an
/// abbreviation unique among units, and its factor: the number of metres (the SI base unit) in one of the unit.
/// </summary>
public sealed class UnitOfMeasure
{
    public const int MaxNameLength = 100;
    public const int MaxAbbreviationLength = 20;

    private readonly List<LocalizedName> _names = [];

    public UnitOfMeasureId Id { get; private set; }

    /// <summary>The unit's full names, one per language.</summary>
    public IReadOnlyList<LocalizedName> Names => _names.OrderBy(n => n.Language, StringComparer.Ordinal).ToList();

    public string Abbreviation { get; private set; } = default!;

    /// <summary>The abbreviation lower-cased with whitespace collapsed: unique among units.</summary>
    public string NormalizedAbbreviation { get; private set; } = default!;

    /// <summary>Metres per one of this unit (mm = 0.001).</summary>
    public decimal Factor { get; private set; }

    // For EF Core.
    private UnitOfMeasure()
    {
    }

    /// <exception cref="DomainValidationException">Keyed <c>names</c>, <c>abbreviation</c> or <c>factor</c>.</exception>
    public static UnitOfMeasure Create(IReadOnlyDictionary<string, string?>? names, string? abbreviation, decimal factor)
    {
        var unit = new UnitOfMeasure { Id = UnitOfMeasureId.New() };
        unit.Update(names, abbreviation, factor);
        return unit;
    }

    /// <summary>Replaces names, abbreviation and factor, reporting every invalid part at once.</summary>
    public void Update(IReadOnlyDictionary<string, string?>? names, string? abbreviation, decimal factor)
    {
        var errors = new Dictionary<string, string[]>();
        List<LocalizedName>? list = null;
        try
        {
            list = LocalizedName.FromMap(names, MaxNameLength);
        }
        catch (DomainValidationException e)
        {
            foreach (var (key, messages) in e.Errors) errors[key] = messages;
        }

        var abbr = string.Join(' ', (abbreviation ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (abbr.Length == 0)
            errors["abbreviation"] = ["An abbreviation is required."];
        else if (abbr.Length > MaxAbbreviationLength)
            errors["abbreviation"] = [$"Must be at most {MaxAbbreviationLength} characters."];

        if (factor <= 0)
            errors["factor"] = ["Must be greater than zero."];

        if (errors.Count > 0) throw new DomainValidationException(errors);

        _names.Clear();
        _names.AddRange(list!);
        Abbreviation = abbr;
        NormalizedAbbreviation = NormalizeAbbreviation(abbr);
        Factor = factor;
    }

    public static string NormalizeAbbreviation(string? abbreviation) =>
        string.Join(' ', (abbreviation ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    /// <summary>The name in the first preferred language that has one.</summary>
    public ResolvedText? NameIn(IReadOnlyList<string> preferences) =>
        Localized.Resolve(_names.Select(n => (n.Language, n.Name)), preferences);
}
