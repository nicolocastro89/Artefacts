using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;

namespace Artefacts.Domain.Lists;

/// <summary>The managed lists an artefact picks values from.</summary>
public enum ListKind
{
    Dynasty = 0,
    CollectionGroup = 1,
    StorageLocation = 2,
}

public static class ListKinds
{
    /// <summary>Whether entries of the list have a name per content language (otherwise a single name, such as a location code).</summary>
    public static bool IsTranslated(this ListKind kind) => kind is ListKind.Dynasty or ListKind.CollectionGroup;
}

/// <summary>One name of a translated reference entry. The list kind is repeated so names are unique per list and language.</summary>
public sealed class ReferenceEntryName
{
    public ListKind Kind { get; private set; }
    public string Language { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string NormalizedName { get; private set; } = default!;

    // For EF Core.
    private ReferenceEntryName()
    {
    }

    internal static ReferenceEntryName From(ListKind kind, LocalizedName name) =>
        new() { Kind = kind, Language = name.Language, Name = name.Name, NormalizedName = name.NormalizedName };
}

/// <summary>
/// An entry of a managed list (aggregate root): a dynasty or collection group with a name per content language, or a
/// storage location with a single name. The shape is enforced by the list kind.
/// </summary>
public sealed class ReferenceEntry
{
    public const int MaxNameLength = 200;

    private readonly List<ReferenceEntryName> _names = [];

    public ReferenceEntryId Id { get; private set; }
    public ListKind Kind { get; private set; }

    /// <summary>The single name of an untranslated entry; null for translated kinds.</summary>
    public string? Name { get; private set; }

    /// <summary>Lower case, whitespace collapsed: unique within the list. Null for translated kinds.</summary>
    public string? NormalizedName { get; private set; }

    /// <summary>The names per language of a translated entry; empty for untranslated kinds.</summary>
    public IReadOnlyList<ReferenceEntryName> Names => _names.OrderBy(n => n.Language, StringComparer.Ordinal).ToList();

    // For EF Core.
    private ReferenceEntry()
    {
    }

    /// <summary>Creates a translated entry from a language → name map.</summary>
    public static ReferenceEntry Create(ListKind kind, IReadOnlyDictionary<string, string?>? names)
    {
        var entry = new ReferenceEntry { Id = ReferenceEntryId.New(), Kind = kind };
        entry.SetNames(names);
        return entry;
    }

    /// <summary>Creates an untranslated entry (a storage location) with its single name.</summary>
    public static ReferenceEntry Create(ListKind kind, string? name)
    {
        var entry = new ReferenceEntry { Id = ReferenceEntryId.New(), Kind = kind };
        entry.Rename(name);
        return entry;
    }

    /// <summary>Replaces the names of a translated entry (blank ones dropped; at least one required).</summary>
    /// <exception cref="DomainValidationException">The list has a single name, or the names are invalid.</exception>
    public void SetNames(IReadOnlyDictionary<string, string?>? names)
    {
        if (!Kind.IsTranslated())
            throw new DomainValidationException("names", "This list has a single name per entry, not names per language.");
        var list = LocalizedName.FromMap(names, MaxNameLength);
        _names.Clear();
        _names.AddRange(list.Select(n => ReferenceEntryName.From(Kind, n)));
    }

    /// <summary>Sets the single name of an untranslated entry (trimmed, whitespace collapsed).</summary>
    /// <exception cref="DomainValidationException">The list is translated, or the name is blank or too long.</exception>
    public void Rename(string? name)
    {
        if (Kind.IsTranslated())
            throw new DomainValidationException("name", "This list has names per language.");
        var clean = string.Join(' ', (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (clean.Length == 0) throw new DomainValidationException("name", "Is required.");
        if (clean.Length > MaxNameLength) throw new DomainValidationException("name", $"Must be at most {MaxNameLength} characters.");
        Name = clean;
        NormalizedName = clean.ToLowerInvariant();
    }

    /// <summary>The name in the first preferred language that has one; the single name for untranslated entries.</summary>
    public ResolvedText? NameIn(IReadOnlyList<string> preferences) =>
        Kind.IsTranslated()
            ? Localized.Resolve(_names.Select(n => (n.Language, n.Name)), preferences)
            : new ResolvedText(Name!, preferences.Count > 0 ? preferences[0] : "");

    /// <summary>Whether <paramref name="normalized"/> is one of the entry's names (normalised as by <see cref="LocalizedName.Normalize"/>).</summary>
    public bool HasName(string normalized) =>
        Kind.IsTranslated() ? _names.Any(n => n.NormalizedName == normalized) : NormalizedName == normalized;
}
