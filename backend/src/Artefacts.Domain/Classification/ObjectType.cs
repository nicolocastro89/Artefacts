using Artefacts.Domain.Common;

namespace Artefacts.Domain.Classification;

/// <summary>
/// A kind of object in the institution's reference list (e.g. "Ceramics", "Carpets"), used to classify artefacts.
/// Names are unique ignoring case and surrounding/repeated whitespace, like region names.
/// </summary>
public sealed class ObjectType
{
    public const int MaxNameLength = 200;

    public ObjectTypeId Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string NormalizedName { get; private set; } = default!;

    // For EF Core.
    private ObjectType()
    {
    }

    public static ObjectType Create(string? name)
    {
        var type = new ObjectType { Id = ObjectTypeId.New() };
        type.Rename(name);
        return type;
    }

    public void Rename(string? name)
    {
        var clean = CleanName(name);
        Name = clean;
        NormalizedName = Normalize(clean);
    }

    public static string Normalize(string? name) => CleanName(name).ToLowerInvariant();

    private static string CleanName(string? name)
    {
        var collapsed = string.Join(' ', (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (collapsed.Length == 0) throw new DomainValidationException("name", "Is required.");
        if (collapsed.Length > MaxNameLength)
            throw new DomainValidationException("name", $"Must be at most {MaxNameLength} characters.");
        return collapsed;
    }
}
