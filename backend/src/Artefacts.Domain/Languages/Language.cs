using System.Text.RegularExpressions;
using Artefacts.Domain.Common;

namespace Artefacts.Domain.Languages;

/// <summary>
/// A language that collection content can be recorded in (e.g. "it", "en"). Languages are data: adding one is
/// adding a row. <see cref="Position"/> orders the fallback used when a value is missing in the reader's language.
/// </summary>
public sealed partial class Language
{
    public const int MaxNameLength = 100;

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public int Position { get; private set; }

    // For EF Core.
    private Language()
    {
    }

    public static Language Create(string? code, string? name, int position)
    {
        var language = new Language { Code = NormalizeCode(code) ?? throw new DomainValidationException("code", "Must be a language code such as \"it\" or \"pt-br\".") };
        language.Rename(name);
        language.MoveTo(position);
        return language;
    }

    public void Rename(string? name) =>
        Name = Guard.Required(name, "name", MaxNameLength);

    public void MoveTo(int position) => Position = position;

    /// <summary>Lower-cases a code and checks its shape (ISO 639-1, optionally with a region): null when invalid.</summary>
    public static string? NormalizeCode(string? code)
    {
        var c = code?.Trim().ToLowerInvariant().Replace('_', '-');
        return c is not null && CodeRegex().IsMatch(c) ? c : null;
    }

    [GeneratedRegex("^[a-z]{2}(-[a-z0-9]{2,4})?$")]
    private static partial Regex CodeRegex();
}
