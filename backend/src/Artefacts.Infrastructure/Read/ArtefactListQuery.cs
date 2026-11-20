using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Dating;
using Artefacts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Artefacts.Infrastructure.Read;

internal sealed class ArtefactListQuery(ReadDbContext db, IImageUrlBuilder imageUrls) : IArtefactListQuery
{
    public const int MaxPageSize = 100;

    public async Task<ArtefactListPage> ListAsync(ArtefactListRequest request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var query = db.Artefacts;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{Like.Escape(request.Search.Trim())}%";
            query = query.Where(r =>
                EF.Functions.ILike(r.InventoryNumber, pattern, Like.EscapeChar) ||
                EF.Functions.ILike(r.Title, pattern, Like.EscapeChar) ||
                (r.Maker != null && EF.Functions.ILike(r.Maker, pattern, Like.EscapeChar)) ||
                (r.CollectionGroup != null && EF.Functions.ILike(r.CollectionGroup, pattern, Like.EscapeChar)) ||
                (r.PlaceText != null && EF.Functions.ILike(r.PlaceText, pattern, Like.EscapeChar)) ||
                (r.Dynasty != null && EF.Functions.ILike(r.Dynasty, pattern, Like.EscapeChar)) ||
                (r.Holder != null && EF.Functions.ILike(r.Holder, pattern, Like.EscapeChar)) ||
                (r.StorageLocation != null && EF.Functions.ILike(r.StorageLocation, pattern, Like.EscapeChar)));
        }

        var total = await query.CountAsync(ct);
        var groups = await GroupsAsync(query, request.Group, ct);

        var ordered = ThenSort(OrderByGroup(query, request.Group), request.Sort, request.Direction == SortDirection.Desc);
        var rows = await ordered.ThenBy(r => r.InventoryNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var labels = groups.ToDictionary(g => g.Key ?? "", g => g.Label);
        var items = rows.Select(r =>
        {
            var key = request.Group == ArtefactGrouping.None ? null : GroupKeyOf(r, request.Group);
            return new ArtefactListItem(
                r.Id,
                r.InventoryNumber,
                r.Title,
                r.Maker,
                r.CreationDateDisplay is null
                    ? null
                    : new HistoricalDateDto(r.CreationDateDisplay, r.CreationDateEarliestYear, r.CreationDateLatestYear,
                        Enum.TryParse<HistoricalDatePrecision>(r.CreationDatePrecision, out var p) ? p : HistoricalDatePrecision.Approximate),
                r.Medium,
                r.PrimaryImageKey is null ? null : imageUrls.Build(r.PrimaryImageKey),
                r.ImageCount,
                r.ObjectTypeName,
                r.CollectionGroup,
                r.PlaceText,
                r.Dynasty,
                key,
                request.Group == ArtefactGrouping.None ? null : labels.GetValueOrDefault(key ?? ""));
        }).ToList();

        return new ArtefactListPage(items, total, page, pageSize, groups);
    }

    /// <summary>
    /// Orders by group first: groups with a value come first, ordered by their label (names) or key (century),
    /// and artefacts without a value form the last group. Without grouping this is a no-op ordering.
    /// </summary>
    private static IOrderedQueryable<ArtefactRow> OrderByGroup(IQueryable<ArtefactRow> q, ArtefactGrouping group) => group switch
    {
        ArtefactGrouping.ObjectType => q.OrderBy(r => r.ObjectTypeId == null).ThenBy(r => r.ObjectTypeName).ThenBy(r => r.ObjectTypeId),
        ArtefactGrouping.CollectionGroup => q.OrderBy(r => r.CollectionGroupKey == null).ThenBy(r => r.CollectionGroupKey),
        ArtefactGrouping.Origin => q.OrderBy(r => r.PlaceRegionId == null).ThenBy(r => r.PlaceRegionName).ThenBy(r => r.PlaceRegionId),
        ArtefactGrouping.Century => q.OrderBy(r => r.Century == null).ThenBy(r => r.Century),
        ArtefactGrouping.Dynasty => q.OrderBy(r => r.DynastyKey == null).ThenBy(r => r.DynastyKey),
        _ => q.OrderBy(r => 0),
    };

    private static IOrderedQueryable<ArtefactRow> ThenSort(IOrderedQueryable<ArtefactRow> q, ArtefactSort sort, bool desc) => sort switch
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
    private static async Task<IReadOnlyList<ArtefactGroupDto>> GroupsAsync(IQueryable<ArtefactRow> q, ArtefactGrouping group, CancellationToken ct)
    {
        List<ArtefactGroupDto> groups;
        switch (group)
        {
            case ArtefactGrouping.ObjectType:
                groups = (await q.GroupBy(r => new { r.ObjectTypeId, r.ObjectTypeName })
                        .Select(g => new { g.Key.ObjectTypeId, g.Key.ObjectTypeName, Count = g.Count() }).ToListAsync(ct))
                    .OrderBy(g => g.ObjectTypeId is null).ThenBy(g => g.ObjectTypeName, StringComparer.CurrentCultureIgnoreCase)
                    .Select(g => new ArtefactGroupDto(g.ObjectTypeId?.ToString(), g.ObjectTypeName, g.Count)).ToList();
                break;
            case ArtefactGrouping.CollectionGroup:
                groups = TextGroups(await q.GroupBy(r => new { Key = r.CollectionGroupKey, Text = r.CollectionGroup })
                    .Select(g => new TextVariant(g.Key.Key, g.Key.Text, g.Count())).ToListAsync(ct));
                break;
            case ArtefactGrouping.Origin:
                groups = (await q.GroupBy(r => new { r.PlaceRegionId, r.PlaceRegionName })
                        .Select(g => new { g.Key.PlaceRegionId, g.Key.PlaceRegionName, Count = g.Count() }).ToListAsync(ct))
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
                groups = TextGroups(await q.GroupBy(r => new { Key = r.DynastyKey, Text = r.Dynasty })
                    .Select(g => new TextVariant(g.Key.Key, g.Key.Text, g.Count())).ToListAsync(ct));
                break;
            default:
                return [];
        }

        return groups;
    }

    private sealed record TextVariant(string? Key, string? Text, int Count);

    /// <summary>
    /// Free-text groups ("CINA", "Cina", "cina " share one key): labelled with the most frequent spelling as written
    /// (ties: ordinal order), counted over all spellings.
    /// </summary>
    private static List<ArtefactGroupDto> TextGroups(IEnumerable<TextVariant> variants) =>
        variants.GroupBy(v => v.Key)
            .Select(g => new ArtefactGroupDto(
                g.Key,
                g.Key is null ? null : g.OrderByDescending(v => v.Count).ThenBy(v => v.Text, StringComparer.Ordinal).First().Text?.Trim(),
                g.Sum(v => v.Count)))
            .OrderBy(g => g.Key is null).ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

    private static string? GroupKeyOf(ArtefactRow r, ArtefactGrouping group) => group switch
    {
        ArtefactGrouping.ObjectType => r.ObjectTypeId?.ToString(),
        ArtefactGrouping.CollectionGroup => r.CollectionGroupKey,
        ArtefactGrouping.Origin => r.PlaceRegionId?.ToString(),
        ArtefactGrouping.Century => r.Century?.ToString(),
        ArtefactGrouping.Dynasty => r.DynastyKey,
        _ => null,
    };

    /// <summary>"17th century", "1st century BCE".</summary>
    internal static string CenturyLabel(int century)
    {
        var n = Math.Abs(century);
        var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return century < 0 ? $"{n}{suffix} century BCE" : $"{n}{suffix} century";
    }
}
