using System.Text;
using System.Text.RegularExpressions;

namespace Artefacts.Application.Imports;

/// <summary>
/// A name a record (region, list entry, party) can be recognised by: one of its names in any language, or an alias
/// from a template (a text linked to it before).
/// </summary>
public sealed record NameCandidate(Guid Id, string Name, bool Alias = false);

/// <summary>A record suggested for a text, with its similarity score (1 for an exact match).</summary>
public sealed record NameSuggestion(Guid Id, string MatchedName, double Score, bool Exact, bool Alias);

/// <summary>
/// Matches workbook texts (places, list values, parties) to records. Texts are normalised (accents, case, punctuation and "?" removed). An exact match
/// on any name or alias ranks first; other records are scored with the higher of trigram Jaccard and Jaro-Winkler
/// similarity, also against each part of a text such as "Cairo, Egitto", and the best three at
/// <see cref="Threshold"/> or above are suggested.
/// </summary>
public static partial class NameMatcher
{
    public const double Threshold = 0.6;
    public const int MaxSuggestions = 3;

    /// <summary>Parts score slightly below a match on the whole text.</summary>
    private const double PartWeight = 0.95;

    public static string Normalize(string text) =>
        WhitespaceRegex().Replace(PunctuationRegex().Replace(MappingSuggestion.Fold(text), " "), " ").Trim();

    public static IReadOnlyList<NameSuggestion> Suggest(string text, IReadOnlyList<NameCandidate> candidates)
    {
        var key = Normalize(text);
        if (key.Length == 0) return [];
        var parts = PartSeparatorRegex().Split(MappingSuggestion.Fold(text))
            .Select(Normalize).Where(p => p.Length > 0 && p != key).Distinct().ToList();

        var best = new Dictionary<Guid, NameSuggestion>();
        foreach (var candidate in candidates)
        {
            var name = Normalize(candidate.Name);
            if (name.Length == 0) continue;
            NameSuggestion suggestion;
            if (name == key)
            {
                suggestion = new NameSuggestion(candidate.Id, candidate.Name, 1, true, candidate.Alias);
            }
            else
            {
                var score = Similarity(key, name);
                foreach (var part in parts) score = Math.Max(score, PartWeight * Similarity(part, name));
                suggestion = new NameSuggestion(candidate.Id, candidate.Name, Math.Round(score, 3), false, candidate.Alias);
            }

            if (!best.TryGetValue(candidate.Id, out var current) || Rank(suggestion) > Rank(current))
                best[candidate.Id] = suggestion;
        }

        return best.Values
            .Where(s => s.Exact || s.Score >= Threshold)
            .OrderByDescending(Rank)
            .ThenBy(s => s.MatchedName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxSuggestions)
            .ToList();
    }

    /// <summary>Exact alias hits first, then exact names, then by score.</summary>
    private static double Rank(NameSuggestion s) => (s.Exact ? 2 : 0) + (s.Exact && s.Alias ? 1 : 0) + s.Score;

    /// <summary>
    /// Jaro-Winkler rates short unrelated names around 0.7 ("Iran" and "France"), so it only counts when it is
    /// strong, as for a spelling variant ("Cina" and "China"); trigram overlap covers the rest.
    /// </summary>
    public const double StrongJaroWinkler = 0.8;

    public static double Similarity(string a, string b)
    {
        var jw = JaroWinkler(a, b);
        return Math.Max(TrigramJaccard(a, b), jw >= StrongJaroWinkler ? jw : 0);
    }

    public static double TrigramJaccard(string a, string b)
    {
        var x = Trigrams(a);
        var y = Trigrams(b);
        if (x.Count == 0 || y.Count == 0) return 0;
        var shared = x.Count(y.Contains);
        return shared / (double)(x.Count + y.Count - shared);
    }

    private static HashSet<string> Trigrams(string text)
    {
        var padded = "  " + text + " ";
        var set = new HashSet<string>();
        for (var i = 0; i + 3 <= padded.Length; i++) set.Add(padded.Substring(i, 3));
        return set;
    }

    public static double JaroWinkler(string a, string b)
    {
        if (a == b) return 1;
        if (a.Length == 0 || b.Length == 0) return 0;
        var window = Math.Max(0, Math.Max(a.Length, b.Length) / 2 - 1);
        var aMatched = new bool[a.Length];
        var bMatched = new bool[b.Length];
        var matches = 0;
        for (var i = 0; i < a.Length; i++)
        {
            for (var j = Math.Max(0, i - window); j < Math.Min(b.Length, i + window + 1); j++)
            {
                if (bMatched[j] || a[i] != b[j]) continue;
                aMatched[i] = bMatched[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0) return 0;
        var transpositions = 0;
        for (int i = 0, k = 0; i < a.Length; i++)
        {
            if (!aMatched[i]) continue;
            while (!bMatched[k]) k++;
            if (a[i] != b[k]) transpositions++;
            k++;
        }

        var m = (double)matches;
        var jaro = (m / a.Length + m / b.Length + (m - transpositions / 2.0) / m) / 3;
        var prefix = 0;
        while (prefix < Math.Min(4, Math.Min(a.Length, b.Length)) && a[prefix] == b[prefix]) prefix++;
        return jaro + prefix * 0.1 * (1 - jaro);
    }

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[,/()]|\s-\s|\s+(?:o|or|e|and)\s+")]
    private static partial Regex PartSeparatorRegex();
}
