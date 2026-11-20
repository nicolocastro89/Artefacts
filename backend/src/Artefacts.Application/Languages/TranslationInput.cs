using Artefacts.Application.Common;
using Artefacts.Application.Contracts;

namespace Artefacts.Application.Languages;

/// <summary>Checks the language keys of a per-language input map (names, descriptions) against the content languages.</summary>
internal static class TranslationInput
{
    /// <summary>
    /// Returns the map with lower-cased keys. Keys that are not content languages are reported on
    /// <c>{prefix}.{key}</c> and left out.
    /// </summary>
    public static Dictionary<string, string?> Check(
        IReadOnlyDictionary<string, string?>? values, IReadOnlyList<LanguageDto> languages, string prefix, ValidationCollector v)
    {
        var known = languages.Select(l => l.Code).ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in values ?? new Dictionary<string, string?>())
        {
            var code = key.Trim().ToLowerInvariant();
            if (!known.Contains(code))
            {
                v.Add($"{prefix}.{key}", $"\"{key}\" is not a content language.");
                continue;
            }
            result[code] = value;
        }
        return result;
    }
}
