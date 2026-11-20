using System.Text.RegularExpressions;

namespace Artefacts.Application.Imports;

/// <summary>The values an inventory number pattern can use for one row; <see cref="Sheet"/> is the sheet's three-letter prefix.</summary>
public sealed record InventoryNumberValues(string? Group, string? Item, int Row, Func<ImportField, string?> Field, string? Sheet = null);

/// <summary>Builds inventory numbers from a pattern such as <c>CER-{group:2}-{item:2}</c>.</summary>
public static partial class InventoryNumbers
{
    /// <summary>Problems with the pattern itself (unknown tokens, unknown fields), empty when it is usable.</summary>
    public static IReadOnlyList<string> Validate(InventoryNumberPattern pattern)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(pattern.Pattern)) errors.Add("The inventory number pattern is empty.");
        foreach (Match m in BraceRegex().Matches(pattern.Pattern ?? ""))
        {
            var token = TokenRegex().Match(m.Value);
            if (!token.Success)
                errors.Add($"\"{m.Value}\" is not a pattern token. Use {{group}}, {{item}}, {{row}}, {{sheet}} or {{field:name}}, optionally with padding such as {{group:2}}.");
            else if (token.Groups["name"].Value == "field" && !TryField(token.Groups["field"].Value, out _))
                errors.Add($"\"{token.Groups["field"].Value}\" in \"{m.Value}\" is not a field.");
        }

        if (OpenBraceRegex().IsMatch(BraceRegex().Replace(pattern.Pattern ?? "", "")))
            errors.Add("The inventory number pattern has an unclosed brace.");
        return errors;
    }

    /// <summary>The row's inventory number, or null when the pattern needs a value the row does not have.</summary>
    public static string? Build(InventoryNumberPattern pattern, InventoryNumberValues values)
    {
        if (pattern.Prefer is { } prefer && Clean(values.Field(prefer)) is { Length: > 0 } preferred) return preferred;

        var missing = false;
        var result = BraceRegex().Replace(pattern.Pattern, m =>
        {
            var token = TokenRegex().Match(m.Value);
            if (!token.Success)
            {
                missing = true;
                return "";
            }

            var value = token.Groups["name"].Value switch
            {
                "group" => values.Group,
                "item" => values.Item,
                "row" => values.Row.ToString(),
                "sheet" => values.Sheet,
                "field" => TryField(token.Groups["field"].Value, out var field) ? values.Field(field) : null,
                _ => null,
            };
            value = Clean(value);
            if (string.IsNullOrEmpty(value))
            {
                missing = true;
                return "";
            }

            return token.Groups["pad"].Success ? Pad(value, int.Parse(token.Groups["pad"].Value)) : value;
        });

        return missing || string.IsNullOrWhiteSpace(result) ? null : result.Trim();
    }

    /// <summary>Zero-pads the leading number of a value: "2" → "02", "2a" → "02a"; other values are unchanged.</summary>
    public static string Pad(string value, int width)
    {
        var m = LeadingNumberRegex().Match(value);
        return m.Success ? m.Groups[1].Value.PadLeft(width, '0') + value[m.Groups[1].Length..] : value;
    }

    private static string? Clean(string? value) => value is null ? null : Regex.Replace(value.Trim().TrimEnd('.'), @"\s+", " ");

    private static bool TryField(string name, out ImportField field) =>
        Enum.TryParse(name, ignoreCase: true, out field) && Enum.IsDefined(field);

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex BraceRegex();

    [GeneratedRegex(@"[{}]")]
    private static partial Regex OpenBraceRegex();

    [GeneratedRegex(@"^\{(?<name>group|item|row|sheet)(?::(?<pad>\d{1,2}))?\}$|^\{(?<name>field):(?<field>[A-Za-z]+)(?::(?<pad>\d{1,2}))?\}$")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"^(\d+)")]
    private static partial Regex LeadingNumberRegex();
}
