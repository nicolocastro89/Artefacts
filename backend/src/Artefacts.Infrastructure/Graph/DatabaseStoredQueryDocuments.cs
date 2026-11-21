using Artefacts.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Artefacts.Infrastructure.Graph;

/// <summary>Reads the documents of stored queries from the database, in a scope of its own (it is a singleton).</summary>
internal sealed class DatabaseStoredQueryDocuments(IServiceScopeFactory scopes) : IStoredQueryDocuments
{
    public async ValueTask<string?> FindAsync(string id, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IStoredQueryRepository>().GetAsync(id, ct))?.Document;
    }
}
