using Artefacts.Application.Abstractions;
using Artefacts.Application.Artefacts;
using Artefacts.Application.Contracts;
using Artefacts.Application.Media;
using Artefacts.Application.Regions;
using Artefacts.Domain.Common;
using Artefacts.Domain.Imports;
using Artefacts.Domain.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Artefacts.Application.Imports;

public sealed class ImportOptions
{
    public const string Section = "Imports";

    /// <summary>How often the worker looks for work when nothing wakes it.</summary>
    public int PollSeconds { get; set; } = 5;

    /// <summary>How long a claimed job is held before another worker may take it over (renewed after every row).</summary>
    public int LeaseSeconds { get; set; } = 120;

    /// <summary>Imports never committed are discarded after this many days without changes.</summary>
    public int AbandonedAfterDays { get; set; } = 14;

    /// <summary>How often abandoned imports are swept.</summary>
    public int SweepMinutes { get; set; } = 60;
}

/// <summary>
/// The background phases of an import, run by the worker for a claimed job: reading the workbook into staged rows
/// and pictures, and committing the ready rows one at a time. Each row is saved in its own transaction (artefact,
/// media and row status together), so a failing row never affects the others and a restart resumes after the last
/// finished row.
/// </summary>
public sealed class ImportProcessor(
    IServiceScopeFactory scopes,
    IWorkbookReader reader,
    IMediaStorage storage,
    TimeProvider clock,
    IOptions<ImportOptions> options,
    ILogger<ImportProcessor> logger)
{
    private TimeSpan Lease => TimeSpan.FromSeconds(options.Value.LeaseSeconds);

    /// <summary>Runs whichever phase the job is waiting for.</summary>
    public async Task ProcessAsync(Guid jobId, CancellationToken ct)
    {
        ImportJobState state;
        using (var scope = scopes.CreateScope())
        {
            var job = await scope.ServiceProvider.GetRequiredService<IImportRepository>().GetJobAsync(jobId, ct);
            if (job is null) return;
            state = job.State;
        }

        if (state == ImportJobState.Reading) await ReadAsync(jobId, ct);
        else if (state == ImportJobState.Importing) await ImportAsync(jobId, ct);
    }

    // ---------- read phase ----------

    public async Task ReadAsync(Guid jobId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var services = scope.ServiceProvider;
        var imports = services.GetRequiredService<IImportRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var job = await imports.GetJobAsync(jobId, ct);
        if (job is not { State: ImportJobState.Reading }) return;

        var temp = Path.Combine(Path.GetTempPath(), $"artefacts-import-{job.Id:N}.xlsx");
        try
        {
            await using (var source = await storage.OpenReadAsync(job.SourceKey, ct)
                                      ?? throw new WorkbookReadException("The uploaded workbook is missing from storage."))
            await using (var file = File.Create(temp))
            {
                await source.CopyToAsync(file, ct);
            }

            // A restart during reading starts the phase again from scratch.
            await imports.DeleteRowsAsync(job.Id, ct);

            var staged = new Dictionary<int, StagedPicture>();
            WorkbookSheet sheet;
            await using (var workbook = File.OpenRead(temp))
            {
                sheet = await reader.ReadAsync(workbook, job.SheetName, async (picture, content) =>
                    staged[picture.Index] = await StagePictureAsync(job, picture, content, ct), ct);
            }

            var layout = SheetLayoutReader.Classify(sheet.Rows, sheet.Pictures);
            imports.AddRows(layout.Records.Select(r => ImportRow.Stage(
                job.Id, r.SheetRow, r.GroupNumber, r.GroupLabel,
                ImportJson.Write(r.Cells),
                ImportJson.Write(r.Pictures.Select(i => staged[i]).ToList()),
                ImportJson.Write(r.Warnings))));

            var mapping = job.Mapping ?? ImportJson.Write(MappingSuggestion.Suggest(layout.Headings, layout.Records, sheet.Name));
            var warnings = layout.Warnings.ToList();
            if (layout.Records.Count == 0) warnings.Add("The sheet has no record rows below its headings.");
            job.MarkRead(sheet.Name, ImportJson.Write(sheet.SheetNames), ImportJson.Write(layout.Headings), layout.Records.Count,
                layout.GroupHeadings, ImportJson.Write(warnings), mapping, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(ct);
            logger.LogInformation("Import {JobId}: read {Rows} rows and {Pictures} pictures from sheet {Sheet}",
                job.Id, layout.Records.Count, sheet.Pictures.Count, sheet.Name);
        }
        catch (WorkbookReadException e)
        {
            await FailAsync(jobId, e.Message, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Import {JobId}: reading the workbook failed", jobId);
            await FailAsync(jobId, "The workbook could not be read: " + e.Message, ct);
        }
        finally
        {
            try { File.Delete(temp); } catch (IOException) { }
        }
    }

    private async Task<StagedPicture> StagePictureAsync(ImportJob job, WorkbookPicture picture, Stream content, CancellationToken ct)
    {
        // Picture parts are small enough to hold (tens of MB at most), which gives the length storage needs.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        var format = ImageFormatSniffer.Detect(bytes[..Math.Min(bytes.Length, ImageFormatSniffer.HeaderLength)]);
        var extension = format switch
        {
            ImageFormat.Jpeg => "jpg",
            ImageFormat.Png => "png",
            ImageFormat.Tiff => "tif",
            ImageFormat.WebP => "webp",
            _ => picture.ContentType switch
            {
                "image/vnd.ms-photo" => "wdp",
                "image/x-emf" => "emf",
                "image/x-wmf" => "wmf",
                _ => "bin",
            },
        };
        var key = $"{job.StagingPrefix}pictures/{picture.Index}.{extension}";
        if (format is not null)
        {
            buffer.Position = 0;
            await storage.PutAsync(key, buffer, buffer.Length, ImageHandler.ContentType(format.Value), ct);
        }

        return new StagedPicture(picture.Index, $"{picture.Name}.{extension}", key, picture.ContentType, buffer.Length, format);
    }

    private async Task FailAsync(Guid jobId, string reason, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var imports = scope.ServiceProvider.GetRequiredService<IImportRepository>();
        var job = await imports.GetJobAsync(jobId, ct);
        if (job is null) return;
        job.Fail(reason, clock.GetUtcNow());
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        logger.LogWarning("Import {JobId} failed: {Reason}", jobId, reason);
    }

    // ---------- commit phase ----------

    public async Task ImportAsync(Guid jobId, CancellationToken ct)
    {
        await CreatePlacesAsync(jobId, ct);

        IReadOnlyList<(Guid RowId, EvaluatedRow Evaluated)> work;
        using (var scope = scopes.CreateScope())
        {
            var services = scope.ServiceProvider;
            var imports = services.GetRequiredService<IImportRepository>();
            var job = await imports.GetJobAsync(jobId, ct);
            if (job is not { State: ImportJobState.Importing }) return;
            var rows = await imports.GetRowsAsync(jobId, ct);
            var evaluation = await services.GetRequiredService<ImportHandler>().EvaluateAsync(job, null, ct, validate: false);
            var bySheetRow = evaluation.Rows.ToDictionary(r => r.Row.SheetRow);
            work = rows.Where(r => !r.IsDone).Select(r => (r.Id, bySheetRow[r.SheetRow])).ToList();
        }

        foreach (var (rowId, evaluated) in work)
        {
            ct.ThrowIfCancellationRequested();
            await ImportRowAsync(jobId, rowId, evaluated, ct);
            await RecordProgressAsync(jobId, ct);
        }

        using (var scope = scopes.CreateScope())
        {
            var services = scope.ServiceProvider;
            var job = await services.GetRequiredService<IImportRepository>().GetJobAsync(jobId, ct);
            if (job is not { State: ImportJobState.Importing }) return;
            job.Finish(clock.GetUtcNow());
            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
            logger.LogInformation("Import {JobId} finished: {Imported} imported, {Skipped} skipped, {Problems} with problems",
                jobId, job.ImportedRows, job.SkippedRows, job.ProblemRows);
            await CleanUpAsync(job);
        }
    }

    /// <summary>Creates the regions marked for creation, once, before any row; their ids are recorded on the job.</summary>
    private async Task CreatePlacesAsync(Guid jobId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var services = scope.ServiceProvider;
        var imports = services.GetRequiredService<IImportRepository>();
        var job = await imports.GetJobAsync(jobId, ct);
        if (job is not { State: ImportJobState.Importing }) return;
        var decisions = ImportHandler.Decisions(job);
        if (!decisions.Places.Any(p => p is { Action: PlaceAction.Create, RegionId: null })) return;

        var regionHandler = services.GetRequiredService<RegionHandler>();
        var places = new List<PlaceDecision>();
        foreach (var place in decisions.Places)
        {
            if (place is not { Action: PlaceAction.Create, RegionId: null })
            {
                places.Add(place);
                continue;
            }

            try
            {
                // Adding returns the existing region when one already has the name in the same language.
                var result = await regionHandler.AddAsync(new AddRegionRequest(place.Names, place.ParentId), ct);
                places.Add(place with { RegionId = result.Region.Id });
            }
            catch (DomainException e)
            {
                logger.LogWarning(e, "Import {JobId}: region for \"{Place}\" could not be created; the place stays unlinked", jobId, place.Text);
                places.Add(place with { Action = PlaceAction.Keep });
            }

            // Recorded after each region so a restart does not create it twice.
            job.RecordDecisions(ImportJson.Write(decisions with { Places = [.. places, .. decisions.Places.Skip(places.Count)] }), clock.GetUtcNow());
            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }
    }

    private async Task ImportRowAsync(Guid jobId, Guid rowId, EvaluatedRow evaluated, CancellationToken ct)
    {
        var copied = new List<string>();
        try
        {
            using var scope = scopes.CreateScope();
            var services = scope.ServiceProvider;
            var imports = services.GetRequiredService<IImportRepository>();
            var row = await imports.GetRowAsync(rowId, ct);
            if (row is null || row.IsDone) return;
            var now = clock.GetUtcNow();
            var warnings = ImportJson.Write(evaluated.Warnings);

            if (evaluated.Status == ImportRowStatus.Problem)
            {
                row.MarkProblem(evaluated.InventoryNumber, ImportJson.Write(evaluated.Problems), warnings, now);
                await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
                return;
            }

            // Checked again here: the catalogue may have changed since the review.
            var artefacts = services.GetRequiredService<IArtefactRepository>();
            if (evaluated.Status == ImportRowStatus.Skipped ||
                await artefacts.InventoryNumberExistsAsync(evaluated.InventoryNumber!, null, ct))
            {
                row.MarkSkipped(evaluated.InventoryNumber, now);
                await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
                return;
            }

            var artefact = await services.GetRequiredService<ArtefactCommandHandler>().BuildNewAsync(evaluated.Request!, ct);
            var mediaAssets = services.GetRequiredService<IMediaAssetRepository>();
            var i = 0;
            foreach (var picture in evaluated.Row.Pictures.Where(p => p.Supported))
            {
                var asset = MediaAsset.CreateImage(picture.Name, picture.Format!.Value, picture.Size, now);
                await storage.CopyAsync(picture.StorageKey, asset.StorageKey, ct);
                copied.Add(asset.StorageKey);
                mediaAssets.Add(asset);
                // Distinct timestamps keep the sheet's reading order; the first picture becomes primary.
                artefact.AttachImage(asset.Id, now.AddTicks(i++));
            }

            row.MarkImported(artefact.Id.Value, artefact.InventoryNumber, warnings, now);
            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            foreach (var key in copied)
            {
                try { await storage.DeleteAsync(key, CancellationToken.None); }
                catch (Exception d) { logger.LogWarning(d, "Could not remove orphaned media object {Key}", key); }
            }

            var problems = e is DomainValidationException v
                ? ImportHandler.ToProblems(v.Errors, evaluated).ToList()
                : [new RowProblem("row", null, e is DomainException ? e.Message : "The row could not be saved: " + e.Message)];
            if (e is not DomainException) logger.LogError(e, "Import {JobId}: row {Row} failed", jobId, evaluated.Row.SheetRow);
            await MarkFailedRowAsync(rowId, evaluated, problems, ct);
        }
    }

    private async Task MarkFailedRowAsync(Guid rowId, EvaluatedRow evaluated, List<RowProblem> problems, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<IImportRepository>().GetRowAsync(rowId, ct);
        if (row is null || row.IsDone) return;
        row.MarkProblem(evaluated.InventoryNumber, ImportJson.Write(problems), ImportJson.Write(evaluated.Warnings), clock.GetUtcNow());
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
    }

    private async Task RecordProgressAsync(Guid jobId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var imports = scope.ServiceProvider.GetRequiredService<IImportRepository>();
        var job = await imports.GetJobAsync(jobId, ct);
        if (job is null) return;
        var rows = await imports.GetRowsAsync(jobId, ct);
        job.RecordProgress(rows.Count(r => r.IsDone), rows.Count(r => r.Status == ImportRowStatus.Imported),
            rows.Count(r => r.Status == ImportRowStatus.Skipped), rows.Count(r => r.Status == ImportRowStatus.Problem), clock.GetUtcNow());
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        await imports.RenewLeaseAsync(jobId, clock.GetUtcNow() + Lease, ct);
    }

    // ---------- clean-up ----------

    private async Task CleanUpAsync(ImportJob job)
    {
        try { await storage.DeletePrefixAsync(job.StagingPrefix, CancellationToken.None); }
        catch (Exception e) { logger.LogWarning(e, "Could not remove the staging files of import {JobId}", job.Id); }
    }

    /// <summary>Discards imports never committed and untouched for <see cref="ImportOptions.AbandonedAfterDays"/> days.</summary>
    public async Task<int> SweepAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var services = scope.ServiceProvider;
        var imports = services.GetRequiredService<IImportRepository>();
        var abandoned = await imports.AbandonedAsync(clock.GetUtcNow() - TimeSpan.FromDays(options.Value.AbandonedAfterDays), ct);
        foreach (var job in abandoned)
        {
            job.Discard(clock.GetUtcNow());
            await imports.DeleteRowsAsync(job.Id, ct);
            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
            await CleanUpAsync(job);
            logger.LogInformation("Import {JobId} was discarded after {Days} days without changes", job.Id, options.Value.AbandonedAfterDays);
        }

        return abandoned.Count;
    }
}
