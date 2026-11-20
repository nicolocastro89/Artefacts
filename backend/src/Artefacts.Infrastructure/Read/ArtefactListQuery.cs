using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Dating;
using Artefacts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Artefacts.Infrastructure.Read;

internal sealed class ArtefactListQuery(ReadDbContext db, IImageUrlBuilder imageUrls) : IArtefactListQuery
{
    public const int MaxPageSize = 100;

    public async Task<PagedResult<ArtefactListItem>> ListAsync(ArtefactListRequest request, CancellationToken ct)
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
                (r.Maker != null && EF.Functions.ILike(r.Maker, pattern, Like.EscapeChar)));
        }

        var total = await query.CountAsync(ct);
        var desc = request.Direction == SortDirection.Desc;

        IOrderedQueryable<ArtefactRow> ordered = request.Sort switch
        {
            ArtefactSort.Title => desc ? query.OrderByDescending(r => r.Title) : query.OrderBy(r => r.Title),
            ArtefactSort.CreationDate =>
                // Artefacts without a creation date always appear last, whichever the direction.
                desc
                    ? query.OrderBy(r => (r.CreationDateEarliestYear ?? r.CreationDateLatestYear) == null)
                        .ThenByDescending(r => r.CreationDateEarliestYear ?? r.CreationDateLatestYear)
                    : query.OrderBy(r => (r.CreationDateEarliestYear ?? r.CreationDateLatestYear) == null)
                        .ThenBy(r => r.CreationDateEarliestYear ?? r.CreationDateLatestYear),
            _ => desc ? query.OrderByDescending(r => r.InventoryNumber) : query.OrderBy(r => r.InventoryNumber),
        };

        var rows = await ordered.ThenBy(r => r.InventoryNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(r => new ArtefactListItem(
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
            r.ImageCount)).ToList();

        return new PagedResult<ArtefactListItem>(items, total, page, pageSize);
    }
}
