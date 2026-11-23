using HotChocolate.Execution.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Artefacts.Infrastructure.Graph;

/// <summary>Limits applied to every GraphQL operation (configuration section <c>GraphQL</c>).</summary>
public sealed class GraphOptions
{
    public const string Section = "GraphQL";

    /// <summary>Deepest allowed selection nesting.</summary>
    public int MaxDepth { get; set; } = 8;

    /// <summary>Highest allowed estimated field cost (resolver work) of one operation.</summary>
    public double MaxFieldCost { get; set; } = 5_000;

    /// <summary>Highest allowed estimated type cost (objects returned) of one operation.</summary>
    public double MaxTypeCost { get; set; } = 5_000;

    /// <summary>Whether the start stores the shipped system queries; off where there is no database (build-time generation).</summary>
    public bool SyncSystemQueries { get; set; } = true;
}

/// <summary>
/// The read-only GraphQL server over the read views: a query type only (no mutations), depth and cost limits, and
/// stored queries run by identifier (see <see cref="StoredQueryMiddleware"/>), next to ad-hoc documents.
/// </summary>
public static class GraphSetup
{
    public static IRequestExecutorBuilder AddArtefactsGraph(this IServiceCollection services)
    {
        // The limits themselves are applied per request from IOptions<GraphOptions> (GraphLimitsInterceptor); the defaults
        // here only register the rules the per-request limits drive.
        var defaults = new GraphOptions();
        return services
            .AddGraphQLServer()
            .AddQueryType<GraphQuery>()
            .AddDirectiveType<SensitiveDirectiveType>()
            .AddFiltering()
            .AddSorting()
            .AddDataLoader<RegionNamesDataLoader>()
            .AddDataLoader<ObjectTypeNamesDataLoader>()
            .AddDataLoader<EntryNamesDataLoader>()
            .AddDataLoader<PartiesDataLoader>()
            .AddDataLoader<UnitsDataLoader>()
            .AddDataLoader<ProvenanceDataLoader>()
            .AddDataLoader<ImagesDataLoader>()
            .AddDataLoader<ExhibitionsDataLoader>()
            .AddDataLoader<DescriptionsDataLoader>()
            .AddDataLoader<CatalogueEntriesDataLoader>()
            .AddMaxExecutionDepthRule(defaults.MaxDepth)
            .ModifyCostOptions(o =>
            {
                o.MaxFieldCost = defaults.MaxFieldCost;
                o.MaxTypeCost = defaults.MaxTypeCost;
                o.EnforceCostLimits = true;
            })
            .AddHttpRequestInterceptor<GraphLimitsInterceptor>()
            .AddErrorFilter<VariableErrorFilter>();
    }
}
