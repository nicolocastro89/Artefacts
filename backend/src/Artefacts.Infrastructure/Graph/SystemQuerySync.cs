using Artefacts.Application.Abstractions;
using Artefacts.Domain.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Artefacts.Infrastructure.Graph;

/// <summary>
/// At start, brings the stored system queries to the documents shipped with this release: missing ones are added,
/// changed ones replaced, and ones the release no longer ships removed. A shipped document that is not valid against
/// the schema stops the start. Runs after the migrations (which <c>Program</c> applies before the host starts).
/// </summary>
internal sealed class SystemQuerySync(
    IServiceScopeFactory scopes, StoredQueryValidator validator, TimeProvider clock, IOptions<GraphOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.SyncSystemQueries) return;

        foreach (var query in SystemQueries.All)
        {
            var problems = await validator.ValidateAsync(query.Document, cancellationToken);
            if (problems.Count > 0)
                throw new InvalidOperationException(
                    $"System query '{query.Id}' is not valid: " + string.Join("; ", problems.Select(p => p.Line is null ? p.Message : $"{p.Message} (line {p.Line}, column {p.Column})")));
        }

        using var scope = scopes.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IStoredQueryRepository>();
        var stored = (await repository.ListAsync(cancellationToken)).Where(q => q.IsSystem).ToDictionary(q => q.Id);
        var now = clock.GetUtcNow();

        foreach (var query in SystemQueries.All)
        {
            if (stored.Remove(query.Id, out var existing)) existing.ReplaceSystem(query.Name, query.Document, now);
            else repository.Add(StoredQuery.CreateSystem(query.Id, query.Name, query.Document, now));
        }

        foreach (var gone in stored.Values) repository.Remove(gone);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
