using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;

namespace Artefacts.Domain.Regions;

/// <summary>
/// A geographic region in the institution's reference list (e.g. "Flanders"), used by provenance and places of
/// production. A region has a name in one or more content languages ("Turchia"/"Turkey"); names are unique per
/// language, ignoring case and surrounding/repeated whitespace.
/// </summary>
public sealed class Region
{
    public const int MaxNameLength = 200;

    private readonly List<LocalizedName> _names = [];

    public RegionId Id { get; private set; }
    public RegionId? ParentId { get; private set; }

    /// <summary>The region's names, one per language.</summary>
    public IReadOnlyList<LocalizedName> Names => _names.OrderBy(n => n.Language, StringComparer.Ordinal).ToList();

    // For EF Core.
    private Region()
    {
    }

    public static Region Create(IReadOnlyDictionary<string, string?>? names)
    {
        var region = new Region { Id = RegionId.New() };
        region.SetNames(names);
        return region;
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

    /// <summary>Sets or clears the parent. <paramref name="parentOf"/> resolves the current parent of any region.</summary>
    public void SetParent(RegionId? parentId, Func<RegionId, RegionId?> parentOf)
    {
        if (parentId is { } p)
            RegionHierarchy.EnsureNoCycle(Id, p, parentOf);
        ParentId = parentId;
    }
}

/// <summary>Domain service enforcing that the region hierarchy stays acyclic.</summary>
public static class RegionHierarchy
{
    public static void EnsureNoCycle(RegionId regionId, RegionId newParentId, Func<RegionId, RegionId?> parentOf)
    {
        var visited = new HashSet<RegionId>();
        RegionId? current = newParentId;
        while (current is { } c)
        {
            if (c == regionId)
                throw new DomainValidationException("parentId", "A region cannot be its own ancestor.");
            if (!visited.Add(c)) break; // pre-existing cycle elsewhere; do not loop forever
            current = parentOf(c);
        }
    }
}
