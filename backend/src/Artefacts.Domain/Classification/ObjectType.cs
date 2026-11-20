using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;

namespace Artefacts.Domain.Classification;

/// <summary>
/// A kind of object in the institution's reference list (e.g. "Ceramics", "Carpets"), used to classify artefacts.
/// It has a name in one or more content languages; names are unique per language, like region names.
/// </summary>
public sealed class ObjectType
{
    public const int MaxNameLength = 200;

    private readonly List<LocalizedName> _names = [];

    public ObjectTypeId Id { get; private set; }

    /// <summary>The object type's names, one per language.</summary>
    public IReadOnlyList<LocalizedName> Names => _names.OrderBy(n => n.Language, StringComparer.Ordinal).ToList();

    // For EF Core.
    private ObjectType()
    {
    }

    public static ObjectType Create(IReadOnlyDictionary<string, string?>? names)
    {
        var type = new ObjectType { Id = ObjectTypeId.New() };
        type.SetNames(names);
        return type;
    }

    /// <summary>Replaces all names (blank ones dropped; at least one required).</summary>
    public void SetNames(IReadOnlyDictionary<string, string?>? names)
    {
        var list = LocalizedName.FromMap(names, MaxNameLength);
        _names.Clear();
        _names.AddRange(list);
    }

    /// <summary>The name in the first preferred language that has one.</summary>
    public ResolvedText? NameIn(IReadOnlyList<string> preferences) =>
        Localized.Resolve(_names.Select(n => (n.Language, n.Name)), preferences);
}
