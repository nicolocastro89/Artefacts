using Artefacts.Domain.Common;

namespace Artefacts.Domain.Regions;

/// <summary>
/// A geographic region in the institution's reference list (e.g. "Flanders"), used by provenance.
/// Names are unique ignoring case and surrounding/repeated whitespace.
/// </summary>
public sealed class Region
{
    public const int MaxNameLength = 200;

    public RegionId Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string NormalizedName { get; private set; } = default!;
    public RegionId? ParentId { get; private set; }

    // For EF Core.
    private Region()
    {
    }

    public static Region Create(string? name)
    {
        var region = new Region { Id = RegionId.New() };
        region.Rename(name);
        return region;
    }

    public void Rename(string? name)
    {
        var clean = CleanName(name);
        Name = clean;
        NormalizedName = Normalize(clean);
    }

    /// <summary>Sets or clears the parent. <paramref name="parentOf"/> resolves the current parent of any region.</summary>
    public void SetParent(RegionId? parentId, Func<RegionId, RegionId?> parentOf)
    {
        if (parentId is { } p)
            RegionHierarchy.EnsureNoCycle(Id, p, parentOf);
        ParentId = parentId;
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
