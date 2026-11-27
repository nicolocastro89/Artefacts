using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;

namespace Artefacts.Domain.Imports;

/// <summary>
/// A line detector a user defined (aggregate root): a name and a definition (a pattern with named captures, or a list of
/// keywords) kept as JSON text. The built-in detectors are written in code and are not stored; line rules refer to a
/// stored detector by its <see cref="Identifier"/>. Whether the definition is valid is checked by whoever saves it.
/// </summary>
public sealed class ImportDetector
{
    public const int MaxNameLength = 200;
    public const string IdentifierPrefix = "user:";

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;

    /// <summary>Lower case, whitespace collapsed: unique across stored detectors.</summary>
    public string NormalizedName { get; private set; } = default!;

    /// <summary>The detector definition as JSON text.</summary>
    public string Definition { get; private set; } = default!;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // For EF Core.
    private ImportDetector()
    {
    }

    /// <summary>What a line rule stores to refer to this detector: <c>user:</c> and the identifier without hyphens.</summary>
    public string Identifier => IdentifierOf(Id);

    public static string IdentifierOf(Guid id) => IdentifierPrefix + id.ToString("N");

    /// <summary>The stored detector an identifier refers to, or null when it is not the identifier of a stored detector.</summary>
    public static Guid? IdOf(string? identifier) =>
        identifier is not null && identifier.StartsWith(IdentifierPrefix, StringComparison.Ordinal) &&
        Guid.TryParseExact(identifier[IdentifierPrefix.Length..], "N", out var id)
            ? id
            : null;

    public static ImportDetector Create(string? name, string definition, DateTimeOffset now)
    {
        var detector = new ImportDetector { Id = Guid.NewGuid(), CreatedAt = now };
        detector.Update(name, definition, now);
        return detector;
    }

    public void Update(string? name, string definition, DateTimeOffset now)
    {
        Name = Guard.Required(name, "name", MaxNameLength);
        NormalizedName = LocalizedName.Normalize(Name);
        Definition = definition;
        UpdatedAt = now;
    }

    /// <summary>A new detector with this one's definition.</summary>
    public ImportDetector Duplicate(string? name, DateTimeOffset now) => Create(name, Definition, now);
}
