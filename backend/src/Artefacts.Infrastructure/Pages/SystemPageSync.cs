using System.Text.Json;
using Artefacts.Application.Abstractions;
using Artefacts.Application.Pages;
using Artefacts.Domain.Pages;
using Artefacts.Infrastructure.Graph;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Artefacts.Infrastructure.Pages;

/// <summary>
/// At start, brings the stored system pages to the definitions shipped with this release: missing ones are added,
/// changed ones replaced, and ones the release no longer ships removed. A shipped definition that is not valid stops
/// the start. Runs after <see cref="SystemQuerySync"/>, since the pages name system queries.
/// </summary>
internal sealed class SystemPageSync(IServiceScopeFactory scopes, TimeProvider clock, IOptions<GraphOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.SyncSystemQueries) return;

        using var scope = scopes.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<StoredPageValidator>();
        foreach (var page in SystemPages.All)
        {
            var problems = await validator.ValidateAsync(JsonDocument.Parse(page.Definition).RootElement, cancellationToken);
            if (problems.Count > 0)
                throw new InvalidOperationException($"System page '{page.Slug}' is not valid: " + string.Join("; ", problems));
        }

        var repository = scope.ServiceProvider.GetRequiredService<IStoredPageRepository>();
        var stored = (await repository.ListAsync(false, cancellationToken)).Where(p => p.IsSystem).ToDictionary(p => p.Id);
        var now = clock.GetUtcNow();

        foreach (var page in SystemPages.All)
        {
            if (!stored.Remove(page.Slug, out var existing)) repository.Add(StoredPage.CreateSystem(page.Slug, page.Name, page.Definition, now));
            // The order of keys is part of a definition (it orders a widget's fields), so definitions are compared as text.
            else if (existing.Name != page.Name || existing.Definition != page.Definition)
                existing.ReplaceSystem(page.Name, page.Definition, now);
        }

        foreach (var gone in stored.Values) repository.Remove(gone);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
