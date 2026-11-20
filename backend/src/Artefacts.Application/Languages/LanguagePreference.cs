namespace Artefacts.Application.Languages;

/// <summary>Works out the preferred content languages of a request.</summary>
public static class LanguagePreference
{
    /// <summary>
    /// The request's languages, from <paramref name="lang"/> (one code or a comma list, in order) when given, otherwise
    /// from <paramref name="acceptLanguage"/> in quality order. Each is matched to a content language exactly, then by
    /// its primary subtag ("en-GB" → "en"); unknown codes and repeats are dropped. The remaining content languages
    /// follow in fallback order, so every content language appears exactly once.
    /// </summary>
    public static IReadOnlyList<string> Resolve(string? lang, string? acceptLanguage, IReadOnlyList<string> contentLanguages)
    {
        var requested = !string.IsNullOrWhiteSpace(lang)
            ? lang.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : ParseAcceptLanguage(acceptLanguage);

        var result = new List<string>();
        foreach (var tag in requested)
        {
            var match = Match(tag, contentLanguages);
            if (match is not null && !result.Contains(match)) result.Add(match);
        }
        foreach (var code in contentLanguages)
            if (!result.Contains(code)) result.Add(code);
        return result;
    }

    private static string? Match(string tag, IReadOnlyList<string> contentLanguages)
    {
        var code = tag.Trim().ToLowerInvariant().Replace('_', '-');
        if (contentLanguages.Contains(code)) return code;
        var primary = code.Split('-')[0];
        return contentLanguages.Contains(primary) ? primary : null;
    }

    /// <summary>Language tags of an Accept-Language header, highest quality first (ties keep header order).</summary>
    private static IEnumerable<string> ParseAcceptLanguage(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return [];
        return header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((part, index) =>
            {
                var pieces = part.Split(';', StringSplitOptions.TrimEntries);
                var q = 1.0;
                foreach (var p in pieces.Skip(1))
                    if (p.StartsWith("q=", StringComparison.OrdinalIgnoreCase) &&
                        double.TryParse(p[2..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                        q = parsed;
                return (Tag: pieces[0], Q: q, Index: index);
            })
            .Where(x => x.Q > 0 && x.Tag != "*")
            .OrderByDescending(x => x.Q).ThenBy(x => x.Index)
            .Select(x => x.Tag);
    }
}
