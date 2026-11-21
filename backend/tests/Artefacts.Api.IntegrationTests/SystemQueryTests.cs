using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Abstractions;
using Artefacts.Domain.Queries;
using Artefacts.Infrastructure.Graph;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SystemQueryTests(ApiFactory factory)
{
    private async Task<IReadOnlyList<StoredQuery>> StoredAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IStoredQueryRepository>().ListAsync(CancellationToken.None);
    }

    private Task StartSyncAsync() =>
        factory.Services.GetServices<IHostedService>().Single(s => s.GetType().Name == "SystemQuerySync").StartAsync(CancellationToken.None);

    [Fact]
    public void The_shipped_system_queries_are_the_list_and_the_detail() =>
        Assert.Equal(["artefact-detail", "artefact-list"], SystemQueries.All.Select(q => q.Id).Order());

    [Fact]
    public async Task Every_system_query_is_valid_against_the_schema()
    {
        var validator = factory.Services.GetRequiredService<StoredQueryValidator>();

        foreach (var query in SystemQueries.All)
            Assert.Empty(await validator.ValidateAsync(query.Document, CancellationToken.None));
    }

    [Fact]
    public async Task Both_are_stored_as_system_queries_after_start()
    {
        var system = (await StoredAsync()).Where(q => q.IsSystem).ToList();

        Assert.Equal(["artefact-detail", "artefact-list"], system.Select(q => q.Id).Order());
        Assert.Equal("Artefact list", system.Single(q => q.Id == "artefact-list").Name);
    }

    [Fact]
    public async Task A_changed_document_replaces_the_stored_one_at_the_next_start()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IStoredQueryRepository>();
            (await repository.GetAsync("artefact-list", CancellationToken.None))!.ReplaceSystem("Old name", "query { language }", DateTimeOffset.UtcNow);
            repository.Add(StoredQuery.CreateSystem("retired-" + Guid.NewGuid().ToString("N")[..6], "Retired", "query { language }", DateTimeOffset.UtcNow));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        await StartSyncAsync();

        var stored = await StoredAsync();
        var list = stored.Single(q => q.Id == "artefact-list");
        Assert.Equal(SystemQueries.All.Single(q => q.Id == "artefact-list").Document, list.Document);
        Assert.Equal("Artefact list", list.Name);
        Assert.DoesNotContain(stored, q => q.IsSystem && q.Id.StartsWith("retired-"));
    }

    [Theory]
    [InlineData("query { artefact(id: \"x\") { nonsense } }", "nonsense")]
    [InlineData("query { language", "Expected")]
    [InlineData("query A { language } query B { language }", "exactly one")]
    [InlineData("mutation { language }", "query")]
    public async Task Documents_that_cannot_be_stored_are_reported_with_the_problem(string document, string expected)
    {
        var problems = await factory.Services.GetRequiredService<StoredQueryValidator>().ValidateAsync(document, CancellationToken.None);

        Assert.Contains(problems, p => p.Message.Contains(expected, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_validation_problem_has_its_line_and_column()
    {
        var problems = await factory.Services.GetRequiredService<StoredQueryValidator>()
            .ValidateAsync("query {\n  language\n  nonsense\n}", CancellationToken.None);

        var problem = Assert.Single(problems);
        Assert.Equal((3, 3), (problem.Line, problem.Column));
    }

    [Fact]
    public async Task The_system_queries_run_within_the_default_limits()
    {
        var client = factory.CreateClient();
        var created = await client.CreateArtefactAsync("Vase");

        var detail = await client.RunStoredAsync("artefact-detail", new { id = created.Id });
        var list = await client.RunStoredAsync("artefact-list", new { pageSize = 100 });

        Assert.Empty(detail.Errors());
        Assert.Empty(list.Errors());
    }
}

[Collection(ApiCollection.Name)]
public class StoredQueryRunTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<StoredQuery> SaveAsync(string document)
    {
        using var scope = factory.Services.CreateScope();
        var query = StoredQuery.CreateUser(HttpExtensions.Unique("Mine"), document, DateTimeOffset.UtcNow);
        scope.ServiceProvider.GetRequiredService<IStoredQueryRepository>().Add(query);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        return query;
    }

    [Fact]
    public async Task A_stored_user_query_runs_by_its_identifier_like_its_document()
    {
        var created = await _client.CreateArtefactAsync("Vase");
        const string document = "query($id: UUID!) { artefact(id: $id) { title } }";
        var stored = await SaveAsync(document);

        var byId = await _client.RunStoredAsync(stored.Id, new { id = created.Id });
        var direct = await _client.QueryAsync(document, new { id = created.Id });

        Assert.Equal(direct.Data().GetRawText(), byId.Data().GetRawText());
    }

    [Fact]
    public async Task A_variable_of_the_wrong_type_is_reported_naming_it()
    {
        var response = await _client.RunStoredAsync("artefact-list", new { page = "two" });

        Assert.Contains(response.Errors(), e => e.Contains("page"));
    }

    [Fact]
    public async Task A_missing_required_variable_is_reported_naming_it()
    {
        var response = await _client.RunStoredAsync("artefact-detail");

        Assert.Contains(response.Errors(), e => e.Contains("id"));
    }

    [Fact]
    public async Task A_deleted_query_reports_that_it_does_not_exist()
    {
        var stored = await SaveAsync("query { language }");
        Assert.Empty((await _client.RunStoredAsync(stored.Id)).Errors());

        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IStoredQueryRepository>();
            repository.Remove((await repository.GetAsync(stored.Id, CancellationToken.None))!);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        var response = await _client.RunStoredAsync(stored.Id);

        Assert.Contains(response.Errors(), e => e.Contains("exist", StringComparison.OrdinalIgnoreCase) || e.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }
}
