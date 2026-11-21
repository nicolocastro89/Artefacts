using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;

namespace Artefacts.Domain.Parties;

public enum PartyKind
{
    Person = 0,
    Organisation = 1,
}

/// <summary>
/// A person or organisation the collection deals with (aggregate root): auction houses, dealers, private sellers,
/// institutions. Used as an artefact's acquisition source and holder, and later as a loan's borrower. Not translated.
/// </summary>
public sealed class Party
{
    public const int MaxNameLength = 300;

    public PartyId Id { get; private set; }
    public string Name { get; private set; } = default!;

    /// <summary>Lower case, whitespace collapsed: unique across parties.</summary>
    public string NormalizedName { get; private set; } = default!;

    public PartyKind Kind { get; private set; }

    // For EF Core.
    private Party()
    {
    }

    public static Party Create(string? name, PartyKind kind)
    {
        var party = new Party { Id = PartyId.New() };
        party.Rename(name);
        party.ChangeKind(kind);
        return party;
    }

    public void Rename(string? name)
    {
        var clean = string.Join(' ', (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (clean.Length == 0) throw new DomainValidationException("name", "Is required.");
        if (clean.Length > MaxNameLength) throw new DomainValidationException("name", $"Must be at most {MaxNameLength} characters.");
        Name = clean;
        NormalizedName = LocalizedName.Normalize(clean);
    }

    public void ChangeKind(PartyKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new DomainValidationException("kind", "Must be person or organisation.");
        Kind = kind;
    }
}
