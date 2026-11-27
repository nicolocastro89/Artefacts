using System.Globalization;
using System.Text.RegularExpressions;
using Artefacts.Domain.Imports;

namespace Artefacts.Application.Imports;

public enum DetectorKind
{
    /// <summary>A regular expression; its named groups say what is recognised (see <see cref="UserDetector"/>).</summary>
    Regex = 0,

    /// <summary>A list of words or phrases; a line is recognised when it holds (or starts with, or is) one of them.</summary>
    Keywords = 1,
}

/// <summary>What a recognised text is read as, beyond text.</summary>
public enum DetectorParse
{
    None = 0,
    Number = 1,
    CalendarDate = 2,
    HistoricalDate = 3,
    Money = 4,
}

public enum KeywordMatch
{
    Contains = 0,
    StartsWith = 1,

    /// <summary>The whole line, ignoring punctuation left at its ends.</summary>
    Equals = 2,
}

/// <summary>
/// What a user detector recognises. For <see cref="DetectorKind.Regex"/>, <see cref="Pattern"/> is matched against a
/// line; the group <c>value</c> (else the whole match) is the recognised text, <c>remainder</c> (else the rest of the
/// line outside the match) is what is left for other rules, and <c>item</c> gives an item number. <see cref="Parse"/>
/// reads the recognised text as a number, a date or an amount; text that cannot be read is not recognised. For
/// <see cref="DetectorKind.Keywords"/>, the whole line is recognised when <see cref="Keywords"/> match it
/// (ignoring case) the way <see cref="Match"/> says. <see cref="Example"/> is a line it recognises, shown in the picker.
/// </summary>
public sealed record DetectorDefinition(
    DetectorKind Kind,
    string? Pattern = null,
    bool IgnoreCase = true,
    DetectorParse Parse = DetectorParse.None,
    IReadOnlyList<string>? Keywords = null,
    KeywordMatch Match = KeywordMatch.Contains,
    string? Example = null)
{
    public const int MaxPatternLength = 500;
    public const int MaxKeywords = 200;
    public const int MaxKeywordLength = 100;
    public const int MaxExampleLength = 200;

    /// <summary>Longest a pattern may take on one line before the line counts as not recognised.</summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>Every problem that stops the definition being stored, by field (empty when it is fine).</summary>
    public IReadOnlyDictionary<string, string> Problems()
    {
        var problems = new Dictionary<string, string>();
        if (Example is { Length: > MaxExampleLength }) problems["definition.example"] = $"Must be at most {MaxExampleLength} characters.";
        switch (Kind)
        {
            case DetectorKind.Regex:
                if (string.IsNullOrWhiteSpace(Pattern)) problems["definition.pattern"] = "Is required.";
                else if (Pattern.Length > MaxPatternLength) problems["definition.pattern"] = $"Must be at most {MaxPatternLength} characters.";
                else
                {
                    try
                    {
                        var regex = Compile();
                        if (regex.IsMatch("")) problems["definition.pattern"] = "Matches empty text, so it would match every line. Require some text.";
                    }
                    catch (ArgumentException e)
                    {
                        problems["definition.pattern"] = $"Not a valid pattern: {e.Message}";
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        problems["definition.pattern"] = "Takes too long to match.";
                    }
                }

                break;
            case DetectorKind.Keywords:
                var keywords = Keywords ?? [];
                if (keywords.Count == 0 || keywords.All(string.IsNullOrWhiteSpace)) problems["definition.keywords"] = "Give at least one keyword.";
                else if (keywords.Count > MaxKeywords) problems["definition.keywords"] = $"At most {MaxKeywords} keywords.";
                else if (keywords.Any(k => k is { Length: > MaxKeywordLength })) problems["definition.keywords"] = $"Each keyword must be at most {MaxKeywordLength} characters.";
                break;
            default:
                problems["definition.kind"] = "Is not a known kind of detector.";
                break;
        }

        return problems;
    }

    /// <summary>The pattern as a regex that gives up after <see cref="MatchTimeout"/>.</summary>
    internal Regex Compile() =>
        new(Pattern!, RegexOptions.CultureInvariant | (IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None), MatchTimeout);
}

/// <summary>A detector made from a stored definition. Pure and safe to share: patterns time out and keywords are plain text.</summary>
public sealed class UserDetector : ILineDetector
{
    private readonly DetectorDefinition _definition;
    private readonly Regex? _regex;
    private readonly string[] _keywords;

    public UserDetector(string id, string label, DetectorDefinition definition)
    {
        Id = id;
        Label = label;
        _definition = definition;
        _keywords = [.. (definition.Keywords ?? []).Select(k => k.Trim()).Where(k => k.Length > 0)];
        if (definition.Kind == DetectorKind.Regex) _regex = definition.Compile();
    }

    /// <summary>The detector of a stored definition, or null when the definition no longer holds (it cannot be used).</summary>
    public static UserDetector? From(ImportDetector stored)
    {
        try
        {
            var definition = ImportJson.Read<DetectorDefinition>(stored.Definition);
            return definition.Problems().Count > 0 ? null : new UserDetector(stored.Identifier, stored.Name, definition);
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    public string Id { get; }

    public string Label { get; }

    public string? Example => _definition.Example;

    public Detection? Detect(string line, DetectionContext context) =>
        _definition.Kind == DetectorKind.Regex ? DetectPattern(line, context) : DetectKeyword(line);

    private Detection? DetectPattern(string line, DetectionContext context)
    {
        Match m;
        try
        {
            m = _regex!.Match(line);
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        if (!m.Success) return null;
        var value = (m.Groups["value"].Success ? m.Groups["value"].Value : m.Value).Trim();
        if (value.Length == 0) return null;
        var remainder = m.Groups["remainder"].Success ? Detectors.Tidy(m.Groups["remainder"].Value) : Detectors.RemainderOf(line, m);
        var item = m.Groups["item"].Success && m.Groups["item"].Value.Trim() is { Length: > 0 } n ? n : null;

        object? parsed = null;
        var guessed = false;
        if (_definition.Parse != DetectorParse.None)
        {
            var read = Read(value, context);
            if (read is null) return null;
            (parsed, guessed) = (read.Parsed, read.Guessed);
        }

        return new Detection(value, parsed, remainder.Length == 0 ? null : remainder, item, guessed);
    }

    private Detection? Read(string value, DetectionContext context) => _definition.Parse switch
    {
        DetectorParse.Number => decimal.TryParse(value.Replace(',', '.').Replace(" ", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? new Detection(value, number)
            : null,
        DetectorParse.CalendarDate => Detectors.CalendarDate(value, context.Lookup),
        DetectorParse.HistoricalDate => Detectors.HistoricalDate(value),
        DetectorParse.Money => Detectors.Money(value, context.Lookup),
        _ => new Detection(value),
    };

    /// <summary>A text without punctuation at its ends, for comparing whole lines.</summary>
    private static string Whole(string text) => Detectors.Tidy(text).TrimEnd('.', ' ');

    private Detection? DetectKeyword(string line)
    {
        var text = line.Trim();
        if (text.Length == 0) return null;
        var whole = Whole(text);
        var matched = _definition.Match switch
        {
            KeywordMatch.StartsWith => _keywords.Any(k => text.StartsWith(k, StringComparison.OrdinalIgnoreCase)),
            KeywordMatch.Equals => _keywords.Any(k => string.Equals(whole, Whole(k), StringComparison.OrdinalIgnoreCase)),
            _ => _keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)),
        };
        return matched ? new Detection(text) : null;
    }
}
