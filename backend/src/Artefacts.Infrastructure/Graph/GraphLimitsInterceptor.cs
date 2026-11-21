using HotChocolate;
using HotChocolate.AspNetCore;
using HotChocolate.CostAnalysis;
using HotChocolate.Execution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Artefacts.Infrastructure.Graph;

/// <summary>
/// Applies the configured depth and cost limits to every GraphQL request. They are read per request, from
/// <see cref="GraphOptions"/>, rather than fixed when the schema is built, so configuration changes and test overrides
/// take effect. The validation rules registered with the schema only run these limits.
/// </summary>
internal sealed class GraphLimitsInterceptor : DefaultHttpRequestInterceptor
{
    public override ValueTask OnCreateAsync(
        HttpContext context, IRequestExecutor requestExecutor, OperationRequestBuilder requestBuilder, CancellationToken cancellationToken)
    {
        var limits = context.RequestServices.GetRequiredService<IOptions<GraphOptions>>().Value;
        requestBuilder.SetMaximumAllowedExecutionDepth(limits.MaxDepth);
        requestBuilder.SetCostOptions(new RequestCostOptions(limits.MaxFieldCost, limits.MaxTypeCost, enforceCostLimits: true, skipAnalyzer: false, maxResponseSize: (double?)null));
        return base.OnCreateAsync(context, requestExecutor, requestBuilder, cancellationToken);
    }
}

/// <summary>
/// Names the variable in the message of an error about its value: the server reports it only in the error's
/// <c>inputPath</c> extension ("Int cannot coerce the given value…"), which a client showing the message would miss.
/// </summary>
internal sealed class VariableErrorFilter : IErrorFilter
{
    public IError OnError(IError error)
    {
        if (error.Extensions is null || !error.Extensions.TryGetValue("inputPath", out var path)
            || path is not HotChocolate.Path inputPath || inputPath.ToList().FirstOrDefault()?.ToString() is not { Length: > 0 } variable
            || error.Message.Contains(variable, StringComparison.Ordinal))
            return error;

        return error.WithMessage($"Variable '{variable}': {error.Message}");
    }
}
