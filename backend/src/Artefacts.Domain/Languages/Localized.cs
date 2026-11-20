namespace Artefacts.Domain.Languages;

/// <summary>A text resolved from its per-language values, together with the language it is in.</summary>
public readonly record struct ResolvedText(string Text, string Language);

public static class Localized
{
    /// <summary>
    /// The value in the first language of <paramref name="preferences"/> that has one; when none of them has a value,
    /// the value in the alphabetically first remaining language, so a value is never hidden. Null when there is none.
    /// </summary>
    public static ResolvedText? Resolve(IEnumerable<(string Language, string Text)> values, IReadOnlyList<string> preferences)
    {
        var list = values.Where(v => !string.IsNullOrWhiteSpace(v.Text)).ToList();
        if (list.Count == 0) return null;
        foreach (var language in preferences)
        {
            var hit = list.FirstOrDefault(v => v.Language == language);
            if (hit.Text is not null) return new ResolvedText(hit.Text, hit.Language);
        }
        var first = list.OrderBy(v => v.Language, StringComparer.Ordinal).First();
        return new ResolvedText(first.Text, first.Language);
    }
}
