using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Dating;
using Artefacts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Artefacts.Infrastructure.Read;

/// <summary>
/// The artefact list's steps, composable over any <see cref="ArtefactRow"/> query: free-text search, group-first
/// ordering with a sort, every group's label and count, and paging. The GraphQL list resolver builds on them.
/// </summary>
internal static class ArtefactListSteps
{
    public const int MaxPageSize = 100;

    /// <summary>
    /// Artefacts whose inventory number, title, maker or place contains <paramref name="search"/> (ignoring case), or
    /// whose collection group, dynasty or storage location has a name in any language, or whose holder or source has a
    /// name, that contains it. All artefacts when the search is empty.
    /// </summary>
    public static IQueryable<ArtefactRow> Search(IQueryable<ArtefactRow> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var pattern = $"%{Like.Escape(search.Trim())}%";
        return query.Where(r =>
            EF.Functions.ILike(r.InventoryNumber, pattern, Like.EscapeChar) ||
            EF.Functions.ILike(r.Title, pattern, Like.EscapeChar) ||
            (r.Maker != null && EF.Functions.ILike(r.Maker, pattern, Like.EscapeChar)) ||
            (r.PlaceText != null && EF.Functions.ILike(r.PlaceText, pattern, Like.EscapeChar)) ||
            (r.CollectionGroupId != null && LocalizedSql.ReferenceEntryMatches(r.CollectionGroupId, pattern)) ||
            (r.DynastyId != null && LocalizedSql.ReferenceEntryMatches(r.DynastyId, pattern)) ||
            (r.StorageLocationId != null && LocalizedSql.ReferenceEntryMatches(r.StorageLocationId, pattern)) ||
            (r.HolderId != null && EF.Functions.ILike(LocalizedSql.PartyName(r.HolderId)!, pattern, Like.EscapeChar)) ||
            (r.AcquisitionSourceId != null && EF.Functions.ILike(LocalizedSql.PartyName(r.AcquisitionSourceId)!, pattern, Like.EscapeChar)));
    }

    /// <summary>One page of an ordered query; the inventory number breaks remaining ties so pages never overlap.</summary>
    public static IQueryable<ArtefactRow> Page(IOrderedQueryable<ArtefactRow> ordered, int page, int pageSize) =>
        ordered.ThenBy(r => r.InventoryNumber).Skip((page - 1) * pageSize).Take(pageSize);

    /// <summary>
    /// Orders by group first: groups with a value come first, ordered by their label (names) or key (century),
    /// and artefacts without a value form the last group. Without grouping this is a no-op ordering.
    /// </summary>
    public static IOrderedQueryable<ArtefactRow> OrderByGroup(IQueryable<ArtefactRow> q, ArtefactGrouping group, string[] languages) => group switch
    {
        ArtefactGrouping.ObjectType => q.OrderBy(r => r.ObjectTypeId == null)
            .ThenBy(r => LocalizedSql.ObjectTypeName(r.ObjectTypeId, languages)).ThenBy(r => r.ObjectTypeId),
        ArtefactGrouping.CollectionGroup => q.OrderBy(r => r.CollectionGroupId == null)
            .ThenBy(r => LocalizedSql.ReferenceEntryName(r.CollectionGroupId, languages)).ThenBy(r => r.CollectionGroupId),
        ArtefactGrouping.Origin => q.OrderBy(r => r.PlaceRegionId == null)
            .ThenBy(r => LocalizedSql.RegionName(r.PlaceRegionId, languages)).ThenBy(r => r.PlaceRegionId),
        ArtefactGrouping.Century => q.OrderBy(r => r.Century == null).ThenBy(r => r.Century),
        ArtefactGrouping.Dynasty => q.OrderBy(r => r.DynastyId == null)
            .ThenBy(r => LocalizedSql.ReferenceEntryName(r.DynastyId, languages)).ThenBy(r => r.DynastyId),
        _ => q.OrderBy(r => 0),
    };

    public static IOrderedQueryable<ArtefactRow> ThenSort(IOrderedQueryable<ArtefactRow> q, ArtefactSort sort, bool desc) => sort switch
    {
        ArtefactSort.Title => desc ? q.ThenByDescending(r => r.Title) : q.ThenBy(r => r.Title),
        ArtefactSort.CreationDate =>
            // Artefacts without a creation date always appear last within their group, whichever the direction.
            desc
                ? q.ThenBy(r => (r.CreationDateEarliestYear ?? r.CreationDateLatestYear) == null)
                    .ThenByDescending(r => r.CreationDateEarliestYear ?? r.CreationDateLatestYear)
                : q.ThenBy(r => (r.CreationDateEarliestYear ?? r.CreationDateLatestYear) == null)
                    .ThenBy(r => r.CreationDateEarliestYear ?? r.CreationDateLatestYear),
        _ => desc ? q.ThenByDescending(r => r.InventoryNumber) : q.ThenBy(r => r.InventoryNumber),
    };

    /// <summary>Every group of the current search with its label and count, in display order (no-value group last).</summary>
    public static async Task<IReadOnlyList<ArtefactGroupDto>> GroupsAsync(
        IQueryable<ArtefactRow> q, ArtefactGrouping group, string[] languages, CancellationToken ct)
    {
        List<ArtefactGroupDto> groups;
        switch (group)
        {
            case ArtefactGrouping.ObjectType:
                groups = (await q.GroupBy(r => r.ObjectTypeId)
                        .Select(g => new { ObjectTypeId = g.Key, ObjectTypeName = LocalizedSql.ObjectTypeName(g.Key, languages), Count = g.Count() })
                        .ToListAsync(ct))
                    .OrderBy(g => g.ObjectTypeId is null).ThenBy(g => g.ObjectTypeName, StringComparer.CurrentCultureIgnoreCase)
                    .Select(g => new ArtefactGroupDto(g.ObjectTypeId?.ToString(), g.ObjectTypeName, g.Count)).ToList();
                break;
            case ArtefactGrouping.CollectionGroup:
                groups = EntryGroups(await q.GroupBy(r => r.CollectionGroupId)
                    .Select(g => new EntryGroup(g.Key, LocalizedSql.ReferenceEntryName(g.Key, languages), g.Count())).ToListAsync(ct));
                break;
            case ArtefactGrouping.Origin:
                groups = (await q.GroupBy(r => r.PlaceRegionId)
                        .Select(g => new { PlaceRegionId = g.Key, PlaceRegionName = LocalizedSql.RegionName(g.Key, languages), Count = g.Count() })
                        .ToListAsync(ct))
                    .OrderBy(g => g.PlaceRegionId is null).ThenBy(g => g.PlaceRegionName, StringComparer.CurrentCultureIgnoreCase)
                    .Select(g => new ArtefactGroupDto(g.PlaceRegionId?.ToString(), g.PlaceRegionName, g.Count)).ToList();
                break;
            case ArtefactGrouping.Century:
                groups = (await q.GroupBy(r => r.Century)
                        .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
                    .OrderBy(g => g.Key is null).ThenBy(g => g.Key)
                    .Select(g => new ArtefactGroupDto(g.Key?.ToString(), g.Key is { } c ? CenturyLabel(c) : null, g.Count)).ToList();
                break;
            case ArtefactGrouping.Dynasty:
                groups = EntryGroups(await q.GroupBy(r => r.DynastyId)
                    .Select(g => new EntryGroup(g.Key, LocalizedSql.ReferenceEntryName(g.Key, languages), g.Count())).ToListAsync(ct));
                break;
            default:
                return [];
        }

        return groups;
    }

    private sealed record EntryGroup(Guid? Id, string? Name, int Count);

    /// <summary>Groups by list entry, labelled and ordered by the entry's name in the reader's language (no-value group last).</summary>
    private static List<ArtefactGroupDto> EntryGroups(IEnumerable<EntryGroup> groups) =>
        groups.OrderBy(g => g.Id is null).ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new ArtefactGroupDto(g.Id?.ToString(), g.Name, g.Count))
            .ToList();

    public static string? GroupKeyOf(ArtefactRow r, ArtefactGrouping group) => group switch
    {
        ArtefactGrouping.ObjectType => r.ObjectTypeId?.ToString(),
        ArtefactGrouping.CollectionGroup => r.CollectionGroupId?.ToString(),
        ArtefactGrouping.Origin => r.PlaceRegionId?.ToString(),
        ArtefactGrouping.Century => r.Century?.ToString(),
        ArtefactGrouping.Dynasty => r.DynastyId?.ToString(),
        _ => null,
    };

    /// <summary>"17th century", "1st century BCE".</summary>
    public static string CenturyLabel(int century)
    {
        var n = Math.Abs(century);
        var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return century < 0 ? $"{n}{suffix} century BCE" : $"{n}{suffix} century";
    }
}
