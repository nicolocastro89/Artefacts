using Artefacts.Domain.Common;

namespace Artefacts.Domain.Languages;

/// <summary>The name of a reference entry (region, object type) in one language.</summary>
public sealed class LocalizedName
{
    public string Language { get; private set; } = default!;
    public string Name { get; private set; } = default!;

    /// <summary>Lower case, whitespace collapsed: unique per language within one reference list.</summary>
    public string NormalizedName { get; private set; } = default!;

    // For EF Core.
    private LocalizedName()
    {
    }

    /// <summary>Normalises a name for comparison: whitespace collapsed, lower case.</summary>
    public static string Normalize(string? name) => Collapse(name).ToLowerInvariant();

    private static string Collapse(string? name) => string.Join(' ', (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Builds the names of an entry from a language → name map: names are trimmed and collapsed, blank ones dropped,
    /// codes lower-cased. At least one name is required.
    /// </summary>
    /// <exception cref="DomainValidationException">Keyed <c>names</c> (none left) or <c>names.{code}</c> (too long).</exception>
    public static List<LocalizedName> FromMap(IReadOnlyDictionary<string, string?>? names, int maxLength)
    {
        var errors = new Dictionary<string, string[]>();
        var list = new List<LocalizedName>();
        foreach (var (code, raw) in names ?? new Dictionary<string, string?>())
        {
            var language = code.Trim().ToLowerInvariant();
            var name = Collapse(raw);
            if (name.Length == 0) continue;
            if (name.Length > maxLength)
            {
                errors[$"names.{language}"] = [$"Must be at most {maxLength} characters."];
                continue;
            }
            if (list.Any(n => n.Language == language)) continue;
            list.Add(new LocalizedName { Language = language, Name = name, NormalizedName = name.ToLowerInvariant() });
        }

        if (errors.Count > 0) throw new DomainValidationException(errors);
        if (list.Count == 0) throw new DomainValidationException("names", "Give a name in at least one language.");
        return list;
    }
}
