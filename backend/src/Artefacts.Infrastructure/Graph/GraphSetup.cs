using HotChocolate.Execution.Configuration;
using HotChocolate.PersistedOperations;
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
}

/// <summary>
/// The read-only GraphQL server over the read views: a query type only (no mutations), depth and cost limits, and
/// stored queries run by identifier through the persisted operation pipeline, next to ad-hoc documents.
/// </summary>
public static class GraphSetup
{
    public static IRequestExecutorBuilder AddArtefactsGraph(this IServiceCollection services)
    {
        var options = new GraphOptions();
        return services
            .AddGraphQLServer()
            .AddQueryType<GraphQuery>()
            .AddMaxExecutionDepthRule(options.MaxDepth)
            .ModifyCostOptions(o =>
            {
                o.MaxFieldCost = options.MaxFieldCost;
                o.MaxTypeCost = options.MaxTypeCost;
                o.EnforceCostLimits = true;
            })
            .ModifyRequestOptions(o => o.PersistedOperations.OnlyAllowPersistedDocuments = false)
            // Stored queries are looked up in the application's services (a singleton that opens its own scopes).
            .AddApplicationService<IStoredQueryDocuments>()
            .ConfigureSchemaServices(s => s.AddSingleton<IOperationDocumentStorage, StoredQueryDocumentStorage>())
            .UsePersistedOperationPipeline();
    }
}
