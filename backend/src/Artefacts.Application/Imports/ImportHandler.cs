using System.Globalization;
using Artefacts.Application.Abstractions;
using Artefacts.Application.Artefacts;
using Artefacts.Application.Common;
using Artefacts.Application.Contracts;
using Artefacts.Application.Languages;
using Artefacts.Application.Media;
using Artefacts.Domain.Classification;
using Artefacts.Domain.Common;
using Artefacts.Domain.Dating;
using Artefacts.Domain.Imports;
using Artefacts.Domain.Languages;
using Artefacts.Domain.Regions;
using Microsoft.Extensions.Logging;

namespace Artefacts.Application.Imports;

/// <summary>The steps of an import a user drives: upload, mapping, places, dates, review, commit and discard.</summary>
public sealed class ImportHandler(
    IImportRepository imports,
    IImportTemplateRepository templates,
    IObjectTypeRepository objectTypes,
    IRegionRepository regions,
    IArtefactRepository artefacts,
    IMediaStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IImportSignal signal,
    ILanguagePreferences preferences,
    IContentLanguages contentLanguages,
    IImageUrlBuilder imageUrls,
    ArtefactCommandHandler commands,
    ILogger<ImportHandler> logger)
{
    public const int DefaultPreviewRows = 5;

    // ---------- upload ----------

    public async Task<ImportJobDto> CreateAsync(UploadFile? file, Guid? objectTypeId, Guid? templateId, string? sheet, CancellationToken ct)
    {
        var v = new ValidationCollector();
        if (file is null || file.Length <= 0)
            v.Add("file", "Choose an .xlsx workbook to import.");
        else if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase) || !await IsZipAsync(file, ct))
            v.Add("file", $"\"{file.FileName}\" is not an .xlsx workbook. Save it from Excel as an Excel workbook (.xlsx); .xls and .csv files cannot be imported.");

        if (objectTypeId is null) v.Add("objectTypeId", "Choose the object type of the rows in this workbook.");
        else if (await objectTypes.GetAsync(new ObjectTypeId(objectTypeId.Value), ct) is null) v.Add("objectTypeId", "The object type does not exist.");

        Domain.Imports.ImportTemplate? template = null;
        if (templateId is { } t && (template = await templates.GetAsync(t, ct)) is null) v.Add("templateId", "The template does not exist.");
        v.ThrowIfAny();

        var job = ImportJob.Create(file!.FileName, file.Length, objectTypeId!.Value, template?.Id, sheet,
            template?.Mapping, template?.Decisions ?? ImportJson.Write(ImportDecisions.Empty), clock.GetUtcNow());
        await using (var content = file.OpenRead())
        {
            await storage.PutAsync(job.SourceKey, content, file.Length,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ct);
        }

        try
        {
            imports.AddJob(job);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await TryCleanUpAsync(job);
            throw;
        }

        signal.Notify();
        return await ToDtoAsync(job, ct);
    }

    private static async Task<bool> IsZipAsync(UploadFile file, CancellationToken ct)
    {
        await using var stream = file.OpenRead();
        var header = new byte[4];
        var read = 0;
        while (read < header.Length)
        {
            var n = await stream.ReadAsync(header.AsMemory(read), ct);
            if (n == 0) break;
            read += n;
        }

        return read == 4 && header[0] == 'P' && header[1] == 'K' && header[2] == 3 && header[3] == 4;
    }

    // ---------- jobs ----------

    public async Task<IReadOnlyList<ImportJobDto>> ListAsync(CancellationToken ct)
    {
        var jobs = await imports.ListJobsAsync(ct);
        var result = new List<ImportJobDto>(jobs.Count);
        foreach (var job in jobs) result.Add(await ToDtoAsync(job, ct));
        return result;
    }

    public async Task<ImportJobDto> GetAsync(Guid id, CancellationToken ct) => await ToDtoAsync(await LoadAsync(id, ct), ct);

    // ---------- mapping ----------

    public async Task<ImportColumnsDto> ColumnsAsync(Guid id, CancellationToken ct)
    {
        var job = await LoadAsync(id, ct);
        var rows = await StagedRowsAsync(job, ct);
        var records = rows.Select(r => new SheetRecord(r.SheetRow, r.GroupNumber, r.GroupLabel, r.Cells, [], [])).ToList();
        return new ImportColumnsDto(
            MappingSuggestion.Summarise(Headings(job), records),
            Enum.GetNames<ImportField>().Select(JsonName).ToList(),
            Enum.GetNames<LineDetector>().Select(JsonName).ToList());
    }

    public async Task<ImportMappingResult> SetMappingAsync(Guid id, ImportMapping? mapping, CancellationToken ct)
    {
        var job = await LoadAsync(id, ct);
        var warnings = await ValidateMappingAsync(mapping, Headings(job), ct);
        job.SetMapping(ImportJson.Write(mapping!), clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        return new ImportMappingResult(await ToDtoAsync(job, ct), warnings);
    }

    /// <summary>Evaluates the first rows with an unsaved mapping, for the live preview of the mapping step.</summary>
    public async Task<ImportReviewDto> PreviewAsync(Guid id, PreviewMappingRequest request, CancellationToken ct)
    {
        var job = await LoadAsync(id, ct);
        await ValidateMappingAsync(request.Mapping, Headings(job), ct);
        var evaluation = await EvaluateAsync(job, request.Mapping, ct);
        var count = Math.Clamp(request.Rows ?? DefaultPreviewRows, 1, 50);
        return Review(evaluation, await RegionNamesAsync(ct), null, 1, count);
    }

    public async Task<IReadOnlyList<string>> ValidateMappingAsync(ImportMapping? mapping, IReadOnlyDictionary<int, string> headings, CancellationToken ct)
    {
        var v = new ValidationCollector();
        if (mapping is null) throw new DomainValidationException("mapping", "A mapping is required.");
        var warnings = new List<string>();

        var columns = mapping.Columns ?? [];
        var resolved = MappingEngine.Resolve(mapping with { Columns = columns }, headings);
        for (var i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            var key = $"mapping.columns[{i}]";
            if (c.Column < 1) v.Add($"{key}.column", "Columns are numbered from 1.");
            for (var j = 0; j < (c.Lines?.Count ?? 0); j++)
            {
                var line = c.Lines![j];
                if ((line.Detector is null) == (line.Position is null))
                    v.Add($"{key}.lines[{j}]", "A line rule recognises lines either by content or by position.");
                if (line.Position is < 0) v.Add($"{key}.lines[{j}].position", "Line positions start at 0.");
            }

            var label = c.Heading is { Length: > 0 } h ? $"\"{SheetLayoutReader.Clean(h)}\"" : $"column {c.Column}";
            if (c.UsesLines && c.Rest is null)
                warnings.Add($"The {label} column has no field for remaining lines: lines that match no rule will not be imported.");
            if (!c.Ignored && c.Heading is { Length: > 0 } && headings.Count > 0 && !headings.Values.Any(x => SameHeading(x, c.Heading)))
                warnings.Add(resolved.Any(r => r.Rule == c)
                    ? $"This workbook has no {label} column; the rule applies to column {c.Column}."
                    : $"This workbook has no {label} column, and column {c.Column} belongs to another rule; the rule is not used.");
        }

        if (mapping.InventoryNumber is null) v.Add("mapping.inventoryNumber", "An inventory number pattern is required.");
        else
            foreach (var error in InventoryNumbers.Validate(mapping.InventoryNumber))
                v.Add("mapping.inventoryNumber.pattern", error);

        var languages = await contentLanguages.ListAsync(ct);
        if (!languages.Any(l => l.Code == mapping.Language))
            v.Add("mapping.language", $"\"{mapping.Language}\" is not a content language.");

        var patterns = mapping.LocationPatterns ?? [];
        for (var i = 0; i < patterns.Count; i++)
            if (!Detectors.IsValidPattern(patterns[i]))
                v.Add($"mapping.locationPatterns[{i}]", $"\"{patterns[i]}\" is not a valid pattern.");

        v.ThrowIfAny();
        return warnings;
    }

    // ---------- places ----------

    public async Task<IReadOnlyList<ImportPlaceDto>> PlacesAsync(Guid id, CancellationToken ct)
    {
        var job = await LoadAsync(id, ct);
        var evaluation = await EvaluateAsync(job, null, ct);
        var names = await RegionNamesAsync(ct);
        return evaluation.Rows
            .Where(r => r.PlaceText is not null)
            .GroupBy(r => PlaceMatcher.Normalize(r.PlaceText!))
            .Where(g => g.Key.Length > 0)
            .Select(g =>
            {
                var text = g.First().PlaceText!;
                var saved = evaluation.Places.Saved.GetValueOrDefault(g.Key);
                var decision = saved ?? evaluation.Places.Default(text);
                var suggestions = PlaceMatcher.Suggest(text, evaluation.Places.Candidates)
                    .Select(s => new PlaceSuggestionDto(s.RegionId, names.GetValueOrDefault(s.RegionId) ?? s.MatchedName, s.MatchedName, s.Score, s.Exact, s.Alias))
                    .ToList();
                return new ImportPlaceDto(text, g.Count(), decision, saved is not null, suggestions);
            })
            .OrderByDescending(p => p.Rows)
            .ThenBy(p => p.Text, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<ImportPlaceDto>> SetPlacesAsync(Guid id, SetPlaceDecisionsRequest request, CancellationToken ct)
    {
        var job = await LoadAsync(id, ct);
        var decisions = Decisions(job);
        var v = new ValidationCollector();
        var places = request.Places ?? [];
        var languages = await contentLanguages.ListAsync(ct);
        var cleaned = new List<PlaceDecision>();
        for (var i = 0; i < places.Count; i++)
        {
            var p = places[i];
            var key = $"places[{i}]";
            if (string.IsNullOrWhiteSpace(p.Text)) { v.Add($"{key}.text", "The place text is required."); continue; }
            switch (p.Action)
            {
                case PlaceAction.Link:
                    if (p.RegionId is not { } regionId) v.Add($"{key}.regionId", "Choose the region to link.");
                    else if (await regions.GetAsync(new RegionId(regionId), ct) is null) v.Add($"{key}.regionId", "The region does not exist.");
                    cleaned.Add(p with { Names = null, ParentId = null });
                    break;
                case PlaceAction.Create:
                    var names = TranslationInput.Check(p.Names, languages, $"{key}.names", v);
                    v.Try($"{key}", () => LocalizedName.FromMap(names, Region.MaxNameLength));
                    if (p.ParentId is { } parent && await regions.GetAsync(new RegionId(parent), ct) is null)
                        v.Add($"{key}.parentId", "The parent region does not exist.");
                    cleaned.Add(p with { RegionId = null });
                    break;
                case PlaceAction.Keep:
                    cleaned.Add(p with { RegionId = null, Names = null, ParentId = null });
                    break;
                default:
                    v.Add($"{key}.action", "Choose link, create or keep.");
                    break;
            }
        }

        v.ThrowIfAny();
        // Decisions for texts not in this request are kept (they may come from a template).
        var byKey = decisions.Places.ToDictionary(d => PlaceMatcher.Normalize(d.Text));
        foreach (var d in cleaned) byKey[PlaceMatcher.Normalize(d.Text)] = d;
        job.SetDecisions(ImportJson.Write(decisions with { Places = byKey.Values.ToList() }), clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        return await PlacesAsync(id, ct);
    }

    // ---------- dates ----------

    public async Task<IReadOnlyList<ImportDateDto>> DatesAsync(Guid id, CancellationToken ct)
    {
        var job = await LoadAsync(id, ct);
        var evaluation = await EvaluateAsync(job, null, ct);
        var resolutions = Decisions(job).Dates.ToDictionary(d => RowEvaluator.DateKey(d.Text));
        return evaluation.Rows
            .Where(r => r.DateText is not null)
            .Where(r => r.DateUnresolved || (r.Mapped.First(ImportField.CreationDate)?.Parsed is ParsedDateText { Interpretation: null }))
            .GroupBy(r => RowEvaluator.DateKey(r.DateText!))
            .Select(g => new ImportDateDto(g.First().DateText!, g.Select(r => r.Row.SheetRow).ToList(), resolutions.GetValueOrDefault(g.Key)))
            .OrderByDescending(d => d.SheetRows.Count)
            .ThenBy(d => d.Text, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<ImportDateDto>> SetDatesAsync(Guid id, SetDateResolutionsRequest request, CancellationToken ct)
    {
        var job = await LoadAsync(id, ct);
        var v = new ValidationCollector();
        var dates = request.Dates ?? [];
        for (var i = 0; i < dates.Count; i++)
        {
            var d = dates[i];
            if (string.IsNullOrWhiteSpace(d.Text)) v.Add($"dates[{i}].text", "The date text is required.");
            if (d.EarliestYear is < HistoricalDateParser.MinYear or > HistoricalDateParser.MaxYear)
                v.Add($"dates[{i}].earliestYear", $"Years must be between {HistoricalDateParser.MinYear} and {HistoricalDateParser.MaxYear}.");
            if (d.LatestYear is < HistoricalDateParser.MinYear or > HistoricalDateParser.MaxYear)
                v.Add($"dates[{i}].latestYear", $"Years must be between {HistoricalDateParser.MinYear} and {HistoricalDateParser.MaxYear}.");
            if (d.EarliestYear > d.LatestYear) v.Add($"dates[{i}].latestYear", "The latest year must not be before the earliest year.");
        }

        v.ThrowIfAny();
        var decisions = Decisions(job);
        var byKey = decisions.Dates.ToDictionary(d => RowEvaluator.DateKey(d.Text));
        foreach (var d in dates) byKey[RowEvaluator.DateKey(d.Text)] = d with { Text = d.Text.Trim() };
        job.SetDecisions(ImportJson.Write(decisions with { Dates = byKey.Values.ToList() }), clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        return await DatesAsync(id, ct);
    }

    // ---------- review, commit, discard ----------

    public async Task<ImportReviewDto> ReviewAsync(Guid id, ImportRowStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var job = await LoadAsync(id, ct);
        if (job.IsTerminal || job.State == ImportJobState.Importing)
            return ProcessedReview(job, await imports.GetRowsAsync(id, ct), status, page, pageSize);
        return Review(await EvaluateAsync(job, null, ct), await RegionNamesAsync(ct), status, page, pageSize);
    }

    public async Task<ImportJobDto> CommitAsync(Guid id, CancellationToken ct)
    {
        var job = await LoadAsync(id, ct);
        if (job.Mapping is null) throw new ConflictException("import_state", "Map the columns before importing.");
        await ValidateMappingAsync(MappingOf(job), Headings(job), ct);
        job.Commit(clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        signal.Notify();
        return await ToDtoAsync(job, ct);
    }

    public async Task<ImportJobDto> DiscardAsync(Guid id, CancellationToken ct)
    {
        var job = await LoadAsync(id, ct);
        job.Discard(clock.GetUtcNow());
        await imports.DeleteRowsAsync(id, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await TryCleanUpAsync(job);
        return await ToDtoAsync(job, ct);
    }

    public async Task<ImportReportDto> ReportAsync(Guid id, CancellationToken ct)
    {
        var job = await LoadAsync(id, ct);
        var rows = await imports.GetRowsAsync(id, ct);
        return new ImportReportDto(await ToDtoAsync(job, ct), rows
            .Select(r => new ImportReportRowDto(r.SheetRow, r.Status, r.InventoryNumber, r.ArtefactId,
                ImportJson.Read<List<RowProblem>>(r.Problems), ImportJson.Read<List<string>>(r.Warnings)))
            .ToList());
    }

    private async Task TryCleanUpAsync(ImportJob job)
    {
        try { await storage.DeletePrefixAsync(job.StagingPrefix, CancellationToken.None); }
        catch (Exception e) { logger.LogWarning(e, "Could not remove the staging files of import {JobId}", job.Id); }
    }

    // ---------- evaluation ----------

    /// <summary>The evaluated rows of a job with its current (or the given) mapping, decisions and catalogue.</summary>
    public sealed record Evaluation(ImportJob Job, IReadOnlyList<EvaluatedRow> Rows, PlaceResolver Places);

    public async Task<Evaluation> EvaluateAsync(ImportJob job, ImportMapping? mapping, CancellationToken ct, bool validate = true)
    {
        mapping ??= MappingOf(job) ?? throw new ConflictException("import_state", "The workbook is still being read.");
        var rows = await StagedRowsAsync(job, ct);
        var decisions = Decisions(job);
        var places = await PlaceResolverAsync(decisions, ct);
        var dates = decisions.Dates.GroupBy(d => RowEvaluator.DateKey(d.Text)).ToDictionary(g => g.Key, g => g.Last());
        var context = new EvaluationContext(mapping, Headings(job), job.ObjectTypeId.Value, new HashSet<string>(), places.Resolve, dates, job.SheetName);

        var evaluated = RowEvaluator.Evaluate(rows, context);
        var numbers = evaluated.Where(r => r.InventoryNumber is not null).Select(r => r.InventoryNumber!).ToList();
        var existing = await artefacts.ExistingInventoryNumbersAsync(numbers, ct);
        if (existing.Count > 0) evaluated = RowEvaluator.Evaluate(rows, context with { ExistingInventoryNumbers = existing });

        if (validate) evaluated = await ValidateRowsAsync(evaluated, ct);
        return new Evaluation(job, evaluated, places);
    }

    /// <summary>Adds the catalogue's own validation (lengths, currencies, references) to rows that are otherwise ready.</summary>
    private async Task<IReadOnlyList<EvaluatedRow>> ValidateRowsAsync(IReadOnlyList<EvaluatedRow> rows, CancellationToken ct)
    {
        var result = new List<EvaluatedRow>(rows.Count);
        foreach (var row in rows)
        {
            if (row.Status != ImportRowStatus.Ready || row.Request is null)
            {
                result.Add(row);
                continue;
            }

            var errors = await commands.ValidateNewAsync(row.Request, ct);
            result.Add(errors.Count == 0
                ? row
                : row with
                {
                    Status = ImportRowStatus.Problem,
                    Problems = [.. row.Problems, .. ToProblems(errors, row)],
                });
        }

        return result;
    }

    public static IEnumerable<RowProblem> ToProblems(IReadOnlyDictionary<string, string[]> errors, EvaluatedRow row) =>
        errors.SelectMany(e => e.Value.Select(message => new RowProblem(e.Key.Length == 0 ? "row" : e.Key, TextFor(e.Key, row), message)));

    private static string? TextFor(string key, EvaluatedRow row)
    {
        var r = row.Request;
        return key switch
        {
            "title" => r?.Title,
            "inventoryNumber" => r?.InventoryNumber,
            "dimensions" => r?.Dimensions,
            "dynasty" => r?.Dynasty,
            "holder" => r?.Holder,
            "storageLocation" => r?.StorageLocation,
            "creationDate" => r?.CreationDate?.Text,
            "catalogueEntryEn" => r?.CatalogueEntryEn,
            _ when key.StartsWith("placeOfProduction") => r?.PlaceOfProduction?.Text,
            _ when key.StartsWith("acquisition") => r?.Acquisition?.Source,
            _ when key.StartsWith("exhibitions") => string.Join(" · ", r?.Exhibitions ?? []),
            _ when key.StartsWith("descriptions") => r?.Descriptions?.Values.FirstOrDefault(),
            _ => null,
        };
    }

    /// <summary>Resolves place texts with the saved decisions, else an exact match on a region's names or aliases.</summary>
    public sealed class PlaceResolver(IReadOnlyDictionary<string, PlaceDecision> saved, IReadOnlyList<PlaceCandidate> candidates)
    {
        public IReadOnlyDictionary<string, PlaceDecision> Saved { get; } = saved;
        public IReadOnlyList<PlaceCandidate> Candidates { get; } = candidates;

        public PlaceDecision? Default(string text)
        {
            var exact = PlaceMatcher.Suggest(text, Candidates).FirstOrDefault(s => s.Exact);
            return exact is null ? null : new PlaceDecision(text, PlaceAction.Link, exact.RegionId);
        }

        public ResolvedPlace Resolve(string text)
        {
            var decision = Saved.GetValueOrDefault(PlaceMatcher.Normalize(text)) ?? Default(text);
            return decision switch
            {
                { Action: PlaceAction.Link, RegionId: { } id } => new ResolvedPlace(id, false, PlaceAction.Link),
                { Action: PlaceAction.Create, RegionId: { } id } => new ResolvedPlace(id, false, PlaceAction.Create),
                { Action: PlaceAction.Create } => new ResolvedPlace(null, true, PlaceAction.Create),
                { Action: PlaceAction.Keep } => new ResolvedPlace(null, false, PlaceAction.Keep),
                _ => new ResolvedPlace(null, false, null),
            };
        }
    }

    public async Task<PlaceResolver> PlaceResolverAsync(ImportDecisions decisions, CancellationToken ct)
    {
        var candidates = new List<PlaceCandidate>();
        foreach (var region in await regions.SearchAsync(null, ct))
            candidates.AddRange(region.Names.Select(n => new PlaceCandidate(region.Id.Value, n.Name)));

        // Texts linked to a region in any template count as names of that region (while the region exists).
        var known = candidates.Select(c => c.RegionId).ToHashSet();
        foreach (var template in await templates.ListAsync(ct))
        {
            foreach (var d in ReadDecisions(template.Decisions).Places)
                if (d.RegionId is { } regionId && d.Action is PlaceAction.Link or PlaceAction.Create && known.Contains(regionId))
                    candidates.Add(new PlaceCandidate(regionId, d.Text, Alias: true));
        }

        var saved = decisions.Places.GroupBy(d => PlaceMatcher.Normalize(d.Text)).ToDictionary(g => g.Key, g => g.Last());
        return new PlaceResolver(saved, candidates);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> RegionNamesAsync(CancellationToken ct) =>
        (await regions.SearchAsync(null, ct)).ToDictionary(r => r.Id.Value, r => r.NameIn(preferences.Current)?.Text ?? "");

    // ---------- review rows ----------

    private ImportReviewDto Review(Evaluation evaluation, IReadOnlyDictionary<Guid, string> regionNames, ImportRowStatus? status, int page, int pageSize)
    {
        var rows = evaluation.Rows;
        var counts = new ImportCountsDto(rows.Count, rows.Count(r => r.Status == ImportRowStatus.Ready),
            rows.Count(r => r.Status == ImportRowStatus.Skipped), rows.Count(r => r.Status == ImportRowStatus.Problem), 0);
        var filtered = status is { } s ? rows.Where(r => r.Status == s).ToList() : rows.ToList();
        (page, pageSize) = (Math.Max(1, page), Math.Clamp(pageSize, 1, 200));
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new ReviewRowDto(r.Row.SheetRow, r.Status, r.InventoryNumber, r.Request?.Title, r.Row.GroupLabel,
                Fields(r, evaluation.Places, regionNames), Pictures(r.Row.Pictures), r.Problems, r.Warnings, null))
            .ToList();
        return new ImportReviewDto(counts, items, filtered.Count, page, pageSize);
    }

    private ImportReviewDto ProcessedReview(ImportJob job, IReadOnlyList<ImportRow> rows, ImportRowStatus? status, int page, int pageSize)
    {
        var counts = new ImportCountsDto(rows.Count, rows.Count(r => r.Status is ImportRowStatus.Pending or ImportRowStatus.Ready),
            rows.Count(r => r.Status == ImportRowStatus.Skipped), rows.Count(r => r.Status == ImportRowStatus.Problem),
            rows.Count(r => r.Status == ImportRowStatus.Imported));
        var filtered = status is { } s ? rows.Where(r => r.Status == s).ToList() : rows.ToList();
        (page, pageSize) = (Math.Max(1, page), Math.Clamp(pageSize, 1, 200));
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new ReviewRowDto(r.SheetRow, r.Status, r.InventoryNumber, null, r.GroupLabel, [],
                job.IsTerminal ? [] : Pictures(ImportJson.Read<List<StagedPicture>>(r.Pictures)),
                ImportJson.Read<List<RowProblem>>(r.Problems), ImportJson.Read<List<string>>(r.Warnings), r.ArtefactId))
            .ToList();
        return new ImportReviewDto(counts, items, filtered.Count, page, pageSize);
    }

    private IReadOnlyList<ReviewPictureDto> Pictures(IReadOnlyList<StagedPicture> pictures) =>
        pictures.Select(p => new ReviewPictureDto(p.Index, p.Name, p.Supported, p.Supported ? imageUrls.Build(p.StorageKey).Thumb : null)).ToList();

    private static IReadOnlyList<ReviewFieldDto> Fields(EvaluatedRow row, PlaceResolver places, IReadOnlyDictionary<Guid, string> regionNames)
    {
        var r = row.Request;
        var fields = new List<ReviewFieldDto>();
        void Add(ImportField field, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) fields.Add(new ReviewFieldDto(field, value));
        }

        if (r is null) return fields;
        Add(ImportField.Title, r.Title);
        Add(ImportField.ItemNumber, row.Mapped.First(ImportField.ItemNumber)?.Text);
        Add(ImportField.CatalogueNumber, row.Mapped.First(ImportField.CatalogueNumber)?.Text);
        Add(ImportField.Dimensions, r.Dimensions);
        Add(ImportField.Maker, r.Maker);
        Add(ImportField.Medium, r.Medium);
        if (r.PlaceOfProduction is { } place)
        {
            var resolved = places.Resolve(place.Text!);
            Add(ImportField.PlaceOfProduction, place.Text + resolved switch
            {
                { PendingCreate: true } => " → new region",
                { RegionId: { } id } => regionNames.TryGetValue(id, out var name) && name.Length > 0 ? $" → {name}" : " → linked region",
                _ => "",
            });
        }

        if (r.CreationDate is { } date)
        {
            var parsed = date.EarliestYear is not null
                ? (date.EarliestYear, date.LatestYear)
                : HistoricalDateParser.TryParse(date.Text!) is { } p ? (p.Earliest, p.Latest) : (null, null);
            Add(ImportField.CreationDate, parsed.Item1 is null && parsed.Item2 is null ? date.Text : $"{date.Text} ({parsed.Item1}–{parsed.Item2})");
        }
        else if (row.DateText is not null)
        {
            Add(ImportField.CreationDate, row.DateText);
        }

        Add(ImportField.Dynasty, r.Dynasty);
        Add(ImportField.Description, r.Descriptions?.Values.FirstOrDefault());
        if (r.Acquisition is { } a)
        {
            Add(ImportField.AcquisitionSource, a.Source);
            Add(ImportField.AcquisitionDate, a.Date?.ToString("d MMMM yyyy", CultureInfo.InvariantCulture));
            Add(ImportField.AcquisitionLot, a.Lot);
            Add(ImportField.AcquisitionPrice, a.Price is { } price ? Amount(price) : null);
            Add(ImportField.AcquisitionNote, a.Note);
        }

        Add(ImportField.Exhibitions, string.Join("\n", r.Exhibitions ?? []));
        Add(ImportField.CatalogueEntryEn, r.CatalogueEntryEn);
        Add(ImportField.Holder, r.Holder);
        Add(ImportField.StorageLocation, r.StorageLocation);
        Add(ImportField.Valuation, r.Valuation is { } v ? Amount(v) : null);
        return fields;
    }

    private static string Amount(MoneyDto money) => $"{money.Amount.ToString("#,0.##", CultureInfo.InvariantCulture)} {money.Currency}";

    // ---------- loading and mapping to DTOs ----------

    private async Task<ImportJob> LoadAsync(Guid id, CancellationToken ct) =>
        await imports.GetJobAsync(id, ct) ?? throw new NotFoundException("Import", id);

    public async Task<IReadOnlyList<StagedRow>> StagedRowsAsync(ImportJob job, CancellationToken ct) =>
        (await imports.GetRowsAsync(job.Id, ct)).Select(ToStaged).ToList();

    public static StagedRow ToStaged(ImportRow r) => new(
        r.SheetRow, r.GroupNumber, r.GroupLabel,
        ImportJson.Read<Dictionary<int, string>>(r.Cells),
        ImportJson.Read<List<StagedPicture>>(r.Pictures),
        ImportJson.Read<List<string>>(r.Warnings));

    public static IReadOnlyDictionary<int, string> Headings(ImportJob job) => ImportJson.Read<Dictionary<int, string>>(job.Headings);

    public static ImportMapping? MappingOf(ImportJob job) => job.Mapping is null ? null : ImportJson.Read<ImportMapping>(job.Mapping);

    public static ImportDecisions Decisions(ImportJob job) => ReadDecisions(job.Decisions);

    public static ImportDecisions ReadDecisions(string json)
    {
        var d = ImportJson.Read<ImportDecisions>(json);
        return new ImportDecisions(d.Places ?? [], d.Dates ?? []);
    }

    private static bool SameHeading(string a, string b) =>
        string.Equals(SheetLayoutReader.Clean(a), SheetLayoutReader.Clean(b), StringComparison.OrdinalIgnoreCase);

    private static string JsonName(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    public async Task<ImportJobDto> ToDtoAsync(ImportJob job, CancellationToken ct)
    {
        var languages = preferences.Current;
        var type = await objectTypes.GetAsync(job.ObjectTypeId, ct);
        var template = job.TemplateId is { } t ? await templates.GetAsync(t, ct) : null;
        return new ImportJobDto(
            job.Id, job.FileName, job.FileSize, job.State,
            new NamedRefDto(job.ObjectTypeId.Value, type?.NameIn(languages)?.Text ?? ""),
            template is null ? null : new NamedRefDto(template.Id, template.Name),
            job.SheetName,
            ImportJson.Read<List<string>>(job.SheetNames),
            job.GroupHeadings, job.TotalRows, job.ProcessedRows, job.ImportedRows, job.SkippedRows, job.ProblemRows,
            job.Error,
            ImportJson.Read<List<string>>(job.Warnings),
            MappingOf(job),
            Decisions(job),
            job.CreatedAt, job.UpdatedAt, job.CommittedAt, job.FinishedAt);
    }
}
