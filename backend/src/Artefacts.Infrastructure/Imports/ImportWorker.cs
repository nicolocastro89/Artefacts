using Artefacts.Application.Abstractions;
using Artefacts.Application.Imports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Artefacts.Infrastructure.Imports;

public sealed class ImportWorkerOptions
{
    /// <summary>Whether this process runs the import worker (on by default).</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Runs import jobs in the background, one at a time: claims the oldest job waiting to be read or imported, runs its
/// phase, and sweeps abandoned imports. A job left in progress by a stopped process is claimed again once its lease
/// has expired and resumes from its first unfinished row.
/// </summary>
public sealed class ImportWorker(
    ImportProcessor processor,
    IServiceScopeFactory scopes,
    IImportSignal signal,
    TimeProvider clock,
    IOptions<ImportOptions> options,
    IOptions<ImportWorkerOptions> worker,
    ILogger<ImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!worker.Value.Enabled) return;
        var poll = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollSeconds));
        var sweepEvery = TimeSpan.FromMinutes(Math.Max(1, options.Value.SweepMinutes));
        var lastSweep = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (clock.GetUtcNow() - lastSweep > sweepEvery)
                {
                    await processor.SweepAsync(stoppingToken);
                    lastSweep = clock.GetUtcNow();
                }

                var jobId = await ClaimAsync(stoppingToken);
                if (jobId is null)
                {
                    await signal.WaitAsync(poll, stoppingToken);
                    continue;
                }

                await processor.ProcessAsync(jobId.Value, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "The import worker failed; retrying in {Seconds} s", poll.TotalSeconds);
                try { await Task.Delay(poll, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task<Guid?> ClaimAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var now = clock.GetUtcNow();
        return await scope.ServiceProvider.GetRequiredService<IImportRepository>()
            .ClaimAsync(now, now + TimeSpan.FromSeconds(options.Value.LeaseSeconds), ct);
    }
}
