using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Abstractions;
using Artefacts.Domain.Common;
using Artefacts.Domain.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class StoredQueryPersistenceTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private async Task SaveAsync(params StoredQuery[] queries)
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IStoredQueryRepository>();
        foreach (var query in queries) repository.Add(query);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_stored_query_is_read_back_as_saved()
    {
        var name = HttpExtensions.Unique("Gifts");
        var saved = StoredQuery.CreateUser(name, "query { artefacts { total } }", Now);
        await SaveAsync(saved);

        using var scope = factory.Services.CreateScope();
        var read = await scope.ServiceProvider.GetRequiredService<IStoredQueryRepository>().GetAsync(saved.Id, CancellationToken.None);

        Assert.NotNull(read);
        Assert.StartsWith("q-", read.Id);
        Assert.Equal(name, read.Name);
        Assert.Equal("query { artefacts { total } }", read.Document);
        Assert.False(read.IsSystem);
        Assert.Equal(Now, read.CreatedAt);
    }

    [Fact]
    public async Task Names_are_unique_ignoring_case_and_surrounding_whitespace()
    {
        var name = HttpExtensions.Unique("Gifts");
        await SaveAsync(StoredQuery.CreateUser(name, "query { language }", Now));

        var error = await Assert.ThrowsAsync<DomainValidationException>(() =>
            SaveAsync(StoredQuery.CreateUser($"  {name.ToUpperInvariant()}  ", "query { language }", Now)));

        Assert.Contains("name", error.Errors.Keys);
    }

    [Fact]
    public async Task System_queries_cannot_be_changed_but_can_be_duplicated()
    {
        var system = StoredQuery.CreateSystem(HttpExtensions.Unique("sys"), "System list", "query { language }", Now);

        var conflict = Assert.Throws<ConflictException>(() => system.Change("Other", null, Now));
        var copy = system.Duplicate(HttpExtensions.Unique("Copy"), Now);
        copy.Change(null, "query { artefacts { total } }", Now.AddMinutes(1));

        Assert.Equal("system_query", conflict.Code);
        Assert.False(copy.IsSystem);
        Assert.Equal("query { artefacts { total } }", copy.Document);
        Assert.Equal("query { language }", system.Document);
        await SaveAsync(system, copy);
    }
}
