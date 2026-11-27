using System.Globalization;
using System.Text.RegularExpressions;
using Artefacts.Application.Contracts;

namespace Artefacts.Application.Imports;

/// <summary>A unit as the parser knows it: its identifier, abbreviation and full names in every language.</summary>
public sealed record ParserUnit(Guid Id, string Abbreviation, IReadOnlyList<string> Names);

/// <summary>Measurements read from a dimensions text, each with its unit.</summary>
public sealed record ParsedDimensions(IReadOnlyDictionary<MeasureKind, MeasurementInput> Measurements)
{
    public MeasurementInput? Of(MeasureKind kind) => Measurements.GetValueOrDefault(kind);
}

/// <summary>
/// Reads width, height, length and diameter from dimensions text as curators write it ("diam 17,6 x h 7 cm",
/// "h 30 x l 45", "82 x 64 cm"). Deliberately strict: a text is read only when every number can be assigned to a
/// different measurement and every measurement has a unit; anything else is unclear and left to the user.
/// </summary>
public static partial class DimensionsParser
{
    /// <summary>Words that join values without meaning anything ("x", "per").</summary>
    private static readonly HashSet<string> Joiners = new(StringComparer.OrdinalIgnoreCase) { "x", "per", "by" };

    /// <summary>The measurements of <paramref name="text"/>, or null when the text is unclear.</summary>
    public static ParsedDimensions? Parse(string? text, IReadOnlyList<ParserUnit> units, DimensionsOptions? options, LookupTables? tables = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        tables ??= LookupTables.BuiltIn;
        var unitWords = UnitWords(units, tables);

        var values = new List<(decimal Value, MeasureKind? Kind, Guid? Unit)>();
        MeasureKind? pendingLabel = null;
        Guid? leadingUnit = null;
        foreach (Match token in TokenRegex().Matches(text.Replace('×', 'x').Replace('*', 'x')))
        {
            var t = token.Value;
            if (char.IsDigit(t[0]))
            {
                var value = decimal.Parse(t.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
                values.Add((value, pendingLabel, leadingUnit));
                pendingLabel = null;
                continue;
            }

            if (t is ";" or ",") continue;
            var word = t.TrimEnd('.').ToLowerInvariant();
            if (Joiners.Contains(word)) continue;

            if (tables.MeasureLabel(word) is { } kind)
            {
                // Two labels in a row ("h l 7") cannot be told apart.
                if (pendingLabel is not null) return null;
                pendingLabel = kind;
                continue;
            }

            if (unitWords.TryGetValue(word, out var unit))
            {
                if (pendingLabel is not null) return null;
                var open = values.Select((v, i) => (v, i)).Where(x => x.v.Unit is null).ToList();
                // A unit before any number ("cm 30 x 40") applies to the numbers that follow it.
                if (open.Count == 0) leadingUnit = unit;
                foreach (var (v, i) in open) values[i] = v with { Unit = unit };
                continue;
            }

            // Any other word ("chiuso", "aperto", "circa") means the text says more than measurements.
            return null;
        }

        if (pendingLabel is not null || values.Count == 0) return null;

        var labelled = values.Count(v => v.Kind is not null);
        if (labelled > 0 && labelled < values.Count) return null;
        if (labelled == 0)
        {
            var order = options?.UnlabelledOrder ?? [];
            if (order.Count < values.Count) return null;
            values = values.Select((v, i) => v with { Kind = order[i] }).ToList();
        }

        var result = new Dictionary<MeasureKind, MeasurementInput>();
        foreach (var (value, kind, unit) in values)
        {
            var unitId = unit ?? options?.DefaultUnitId;
            if (unitId is null || value <= 0 || decimal.Round(value, 4) != value) return null;
            if (!result.TryAdd(kind!.Value, new MeasurementInput(value, unitId))) return null;
        }

        return new ParsedDimensions(result);
    }

    private static Dictionary<string, Guid> UnitWords(IReadOnlyList<ParserUnit> units, LookupTables tables)
    {
        var words = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in units)
        {
            words.TryAdd(unit.Abbreviation.Trim().TrimEnd('.').ToLowerInvariant(), unit.Id);
            foreach (var name in unit.Names) words.TryAdd(name.Trim().ToLowerInvariant(), unit.Id);
        }

        if (units.FirstOrDefault(u => string.Equals(u.Abbreviation, "in", StringComparison.OrdinalIgnoreCase)) is { } inch)
            foreach (var alias in tables.InchAliases) words.TryAdd(alias, inch.Id);
        return words;
    }

    // Numbers (decimal comma or point), words with an optional full stop, inch marks, Ø, and separators.
    [GeneratedRegex(@"\d+(?:[.,]\d+)?|[\p{L}]+\.?|''|""|[;,]", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
