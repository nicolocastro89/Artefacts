using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Application.Imports;
using Artefacts.Domain.Imports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Artefacts.Api.IntegrationTests;

/// <summary>
/// An import interrupted by a stopped process continues in the next one from its first unfinished row. Uses its own
/// database so that no other host's worker takes the job.
/// </summary>
public sealed class ImportRestartTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17.12").Build();
    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), "artefacts-tests", Guid.NewGuid().ToString("N"));

    public async ValueTask InitializeAsync() => await _db.StartAsync();

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_mediaRoot, recursive: true); } catch (DirectoryNotFoundException) { }
    }

    private sealed class Host(string connectionString, string mediaRoot, bool worker) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Collection"] = connectionString,
                ["Database:MigrateOnStartup"] = "true",
                ["Storage:Provider"] = "FileSystem",
                ["Storage:FileSystem:Root"] = mediaRoot,
                ["Imgproxy:PublicBaseUrl"] = "http://imgproxy.test",
                ["Imgproxy:Key"] = "736563726574",
                ["Imgproxy:Salt"] = "68656C6C6F",
                ["Imports:PollSeconds"] = "1",
                ["Imports:LeaseSeconds"] = "2",
                ["Imports:Worker:Enabled"] = worker ? "true" : "false",
            }));
        }
    }

    [Fact]
    public async Task Import_continues_after_a_restart_without_duplicates()
    {
        const int rows = 40;
        var builder = ImportTestData.Ceramics(Enumerable.Range(1, rows).Select(i => ($"{i:00}. Coppa {i}", (string?)"Cina\nXV", (string?)null)).ToArray());
        // Pictures make each row take a little while, so the stop lands in the middle of the import.
        for (var i = 0; i < rows; i++) builder.Picture(3 + i, 3, TestData.Jpeg(200_000));
        var workbook = builder.Build();
        Guid jobId;
        int doneAtStop;

        await using (var first = new Host(_db.GetConnectionString(), _mediaRoot, worker: false))
        {
            var client = first.CreateClient();
            var type = (await client.AddObjectTypeAsync("Ceramics")).Id;
            var created = await (await client.PostAsync("/api/imports", ImportTestData.ImportForm(workbook, type))).ReadAsync<ImportJobDto>();
            jobId = created.Id;
            var processor = first.Services.GetRequiredService<ImportProcessor>();
            await processor.ProcessAsync(jobId, CancellationToken.None);
            await client.DecideAllListsAsync(jobId);
            await (await client.PostAsync($"/api/imports/{jobId}/commit", null)).ReadAsync<ImportJobDto>();

            // Run the import and stop it once the first rows are in, as a process shutting down would.
            using var stop = new CancellationTokenSource();
            var running = processor.ProcessAsync(jobId, stop.Token);
            while (await ImportedAsync(first, jobId) < 3) await Task.Delay(20);
            await stop.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
            doneAtStop = await ImportedAsync(first, jobId);
            Assert.Equal(ImportJobState.Importing, (await client.GetImportAsync(jobId)).State);
        }

        Assert.InRange(doneAtStop, 3, rows - 1);

        await using var second = new Host(_db.GetConnectionString(), _mediaRoot, worker: true);
        var resumed = second.CreateClient();
        var finished = await resumed.WaitForImportAsync(jobId, j => j.State is ImportJobState.Finished or ImportJobState.Failed);

        Assert.Equal(ImportJobState.Finished, finished.State);
        Assert.Equal((rows, 0, 0), (finished.ImportedRows, finished.SkippedRows, finished.ProblemRows));
        var list = await resumed.ListArtefactsAsync(search: "CER-01-", pageSize: 100);
        Assert.Equal(rows, list.Total);
        Assert.Equal(rows, list.Items.Select(a => a.InventoryNumber).Distinct().Count());
        Assert.All(list.Items, a => Assert.Equal(1, a.ImageCount));
    }

    private static async Task<int> ImportedAsync(WebApplicationFactory<Program> host, Guid jobId)
    {
        using var scope = host.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IImportRepository>().GetRowsAsync(jobId, CancellationToken.None);
        return rows.Count(r => r.Status == ImportRowStatus.Imported);
    }
}
