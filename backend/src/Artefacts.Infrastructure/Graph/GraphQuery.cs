using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Infrastructure.Read;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Sorting;
using Microsoft.EntityFrameworkCore;

namespace Artefacts.Infrastructure.Graph;

/// <summary>
/// The root query type: read-only access to the collection through the read views. Resolvers run in parallel, so each
/// takes a read context of its own from the factory.
/// </summary>
public sealed class GraphQuery
{
    /// <summary>The reader's first preferred language: the language names and descriptions are asked for in.</summary>
    public string Language([Service] ILanguagePreferences preferences) => preferences.Current.FirstOrDefault() ?? "";

    /// <summary>One artefact, or null when there is none with this identifier.</summary>
    public async Task<Artefact?> GetArtefact(Guid id, [Service] IDbContextFactory<ReadDbContext> dbFactory, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Artefacts.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        return row is null ? null : new Artefact(row);
    }

    /// <summary>
    /// A page of artefacts. Search, group-first ordering, grouping and paging are those of the artefact list; <c>where</c>
    /// narrows the search before grouping, so totals and groups reflect it, and <c>order</c>, when given, replaces
    /// <c>sort</c> and <c>direction</c> after the group ordering.
    /// </summary>
    [UseFiltering(typeof(ArtefactFilterInputType))]
    [UseSorting(typeof(ArtefactSortInputType))]
    public async Task<ArtefactPage> GetArtefacts(
        [Service] IDbContextFactory<ReadDbContext> dbFactory,
        [Service] ILanguagePreferences preferences,
        IFilterContext filter,
        ISortingContext sorting,
        CancellationToken cancellationToken,
        string? search = null,
        ArtefactSort sort = ArtefactSort.InventoryNumber,
        SortDirection direction = SortDirection.Asc,
        ArtefactGrouping group = ArtefactGrouping.None,
        int page = 1,
        int pageSize = 25)
    {
        if (pageSize is < 1 or > ArtefactListSteps.MaxPageSize)
            throw new GraphQLException($"pageSize must be between 1 and {ArtefactListSteps.MaxPageSize}.");
        page = Math.Max(1, page);

        // The filter and the sorting are applied here, in the list's order of steps, not to the resolver's result.
        filter.Handled(true);
        sorting.Handled(true);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var languages = preferences.Current.ToArray();

        var query = ArtefactListSteps.Search(db.Artefacts, search);
        if (filter.IsDefined && filter.AsPredicate<ArtefactRow>() is { } predicate) query = query.Where(predicate);

        var total = await query.CountAsync(cancellationToken);
        var groups = await ArtefactListSteps.GroupsAsync(query, group, languages, cancellationToken);

        var ordered = ArtefactListSteps.OrderByGroup(query, group, languages);
        if (sorting.IsDefined && sorting.AsSortDefinition<ArtefactRow>() is { } definition)
            ordered = definition.Operations.Aggregate(ordered, (current, operation) => operation.ApplyThenBy(current));
        else
            ordered = ArtefactListSteps.ThenSort(ordered, sort, direction == SortDirection.Desc);
        var rows = await ArtefactListSteps.Page(ordered, page, pageSize).ToListAsync(cancellationToken);

        var labels = groups.ToDictionary(g => g.Key ?? "", g => g.Label);
        var items = rows.Select(r =>
        {
            var key = group == ArtefactGrouping.None ? null : ArtefactListSteps.GroupKeyOf(r, group);
            return new Artefact(r, key, group == ArtefactGrouping.None ? null : labels.GetValueOrDefault(key ?? ""));
        }).ToList();

        return new ArtefactPage(items, total, page, pageSize, groups.Select(g => new ArtefactGroup(g.Key, g.Label, g.Count)).ToList());
    }
}
