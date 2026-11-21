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
using Artefacts.Domain.Lists;
using Artefacts.Domain.Parties;
using Artefacts.Domain.Regions;
using Microsoft.Extensions.Logging;

namespace Artefacts.Application.Imports;

/// <summary>The steps of an import a user drives: upload, mapping, places, lists, dates, review, commit and discard.</summary>
public sealed class ImportHandler(
    IImportRepository imports,
    IImportTemplateRepository templates,
    IObjectTypeRepository objectTypes,
    IRegionRepository regions,
    IReferenceEntryRepository listEntries,
    IPartyRepository parties,
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
        return Review(evaluation, await NamesAsync(ct), null, 1, count);
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
        var names = await NamesAsync(ct);
        return evaluation.Rows
            .Where(r => r.PlaceText is not null)
            .GroupBy(r => NameMatcher.Normalize(r.PlaceText!))
            .Where(g => g.Key.Length > 0)
            .Select(g =>
            {
                var text = g.First().PlaceText!;
                var saved = evaluation.Places.Saved.GetValueOrDefault(g.Key);
                var decision = saved ?? evaluation.Places.Default(text);
                var suggestions = NameMatcher.Suggest(text, evaluation.Places.Candidates)
                    .Select(s => new PlaceSuggestionDto(s.Id, names.GetValueOrDefault(s.Id) ?? s.MatchedName, s.MatchedName, s.Score, s.Exact, s.Alias))
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
        var byKey = decisions.Places.ToDictionary(d => NameMatcher.Normalize(d.Text));
        foreach (var d in cleaned) byKey[NameMatcher.Normalize(d.Text)] = d;
        job.SetDecisions(ImportJson.Write(decisions with { Places = byKey.Values.ToList() }), clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        return await PlacesAsync(id, ct);
    }

    // ---------- list values and parties ----------

    private static readonly ImportList[] AllLists = [ImportList.CollectionGroup, ImportList.Dynasty, ImportList.StorageLocation, ImportList.Party];

    /// <summary>
    /// For each list, the distinct texts of the workbook with their row counts, the decision in force (saved, or else
    /// an exact match linked by default) and up to three similar entries, most used first.
    /// </summary>
    public async Task<IReadOnlyList<ImportListGroupDto>> ListsAsync(Guid id, CancellationToken ct)
    {
        var job = await LoadAsync(id, ct);
        var evaluation = await EvaluateAsync(job, null, ct);
        var names = await NamesAsync(ct);
        return AllLists.Select(list =>
        {
            var values = evaluation.Rows
                .SelectMany(r => r.ListTexts.Where(t => t.List == list).Select(t => (Row: r.Row.SheetRow, t.Field, t.Text)))
                .GroupBy(t => NameMatcher.Normalize(t.Text))
                .Where(g => g.Key.Length > 0)
                .Select(g =>
                {
                    var text = g.First().Text;
                    var saved = evaluation.Lists.Saved.GetValueOrDefault((list, g.Key));
                    var decision = saved ?? evaluation.Lists.Default(list, text);
                    var suggestions = NameMatcher.Suggest(text, evaluation.Lists.Candidates(list))
                        .Select(s => new ListSuggestionDto(s.Id, names.GetValueOrDefault(s.Id) ?? s.MatchedName, s.MatchedName, s.Score, s.Exact, s.Alias))
                        .ToList();
                    return new ImportListValueDto(text, g.Select(t => t.Row).Distinct().Count(),
                        g.Select(t => t.Field).Distinct().Order().ToList(), decision, saved is not null, suggestions);
                })
                .OrderByDescending(v => v.Rows)
                .ThenBy(v => v.Text, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            return new ImportListGroupDto(list, values, values.Count(v => v.Decision is null));
        }).ToList();
    }

    /// <summary>Merges list and party decisions into the job's; decisions for other texts are kept.</summary>
    public async Task<IReadOnlyList<ImportListGroupDto>> SetListsAsync(Guid id, SetListDecisionsRequest request, CancellationToken ct)
    {
        var job = await LoadAsync(id, ct);
        var v = new ValidationCollector();
        var input = request.Lists ?? [];
        var languages = await contentLanguages.ListAsync(ct);
        var cleaned = new List<ListDecision>();
        for (var i = 0; i < input.Count; i++)
        {
            var d = input[i];
            var key = $"lists[{i}]";
            if (string.IsNullOrWhiteSpace(d.Text)) { v.Add($"{key}.text", "The text is required."); continue; }
            if (!Enum.IsDefined(d.List)) { v.Add($"{key}.list", "Is not a list."); continue; }
            var text = d.Text.Trim();
            if (d.Action == ListAction.Link)
            {
                if (d.EntryId is not { } entryId) v.Add($"{key}.entryId", "Choose the entry to link.");
                else if (!await ExistsAsync(d.List, entryId, ct)) v.Add($"{key}.entryId", "The entry does not exist.");
                cleaned.Add(d with { Text = text, Names = null, Name = null, PartyKind = null });
            }
            else if (d.Action != ListAction.Create)
            {
                v.Add($"{key}.action", "Choose link or create.");
            }
            else if (d.List == ImportList.Party)
            {
                if (d.PartyKind is not { } kind || !Enum.IsDefined(kind)) v.Add($"{key}.partyKind", "Choose person or organisation.");
                else v.Try(key, () => Party.Create(d.Name, kind));
                cleaned.Add(d with { Text = text, EntryId = null, Names = null });
            }
            else if (!ListKindOf(d.List).IsTranslated())
            {
                v.Try(key, () => ReferenceEntry.Create(ListKindOf(d.List), d.Name));
                cleaned.Add(d with { Text = text, EntryId = null, Names = null, PartyKind = null });
            }
            else
            {
                var names = TranslationInput.Check(d.Names, languages, $"{key}.names", v);
                v.Try(key, () => LocalizedName.FromMap(names, ReferenceEntry.MaxNameLength));
                cleaned.Add(d with { Text = text, EntryId = null, Names = names, Name = null, PartyKind = null });
            }
        }

        v.ThrowIfAny();
        var decisions = Decisions(job);
        job.SetDecisions(ImportJson.Write(decisions.MergedWith(ImportDecisions.Empty with { Lists = cleaned })), clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        return await ListsAsync(id, ct);
    }

    public static ListKind ListKindOf(ImportList list) => list switch
    {
        ImportList.CollectionGroup => ListKind.CollectionGroup,
        ImportList.Dynasty => ListKind.Dynasty,
        ImportList.StorageLocation => ListKind.StorageLocation,
        _ => throw new ArgumentOutOfRangeException(nameof(list), list, "Parties are not a reference list."),
    };

    private async Task<bool> ExistsAsync(ImportList list, Guid id, CancellationToken ct) =>
        list == ImportList.Party
            ? await parties.GetAsync(new PartyId(id), ct) is not null
            : await listEntries.GetAsync(ListKindOf(list), new ReferenceEntryId(id), ct) is not null;

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
        return Review(await EvaluateAsync(job, null, ct), await NamesAsync(ct), status, page, pageSize);
    }

    public async Task<ImportJobDto> CommitAsync(Guid id, CancellationToken ct)
    {
        var job = await LoadAsync(id, ct);
        if (job.Mapping is null) throw new ConflictException("import_state", "Map the columns before importing.");
        await ValidateMappingAsync(MappingOf(job), Headings(job), ct);
        job.Commit(clock.GetUtcNow());
        await LearnTemplateAsync(job, ct);
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

    /// <summary>
    /// Lays the job's place, date and list decisions over those of the template it started from (the job's decision
    /// wins per text; the template's others are kept). The mapping is changed only when the user saves the template.
    /// Not saved here: the caller saves it with its own changes.
    /// </summary>
    public async Task LearnTemplateAsync(ImportJob job, CancellationToken ct)
    {
        if (job.TemplateId is not { } templateId || await templates.GetAsync(templateId, ct) is not { } template) return;
        var merged = ReadDecisions(template.Decisions).MergedWith(Decisions(job));
        template.Update(template.Name, template.Mapping, ImportJson.Write(merged), clock.GetUtcNow());
    }

    private async Task TryCleanUpAsync(ImportJob job)
    {
        try { await storage.DeletePrefixAsync(job.StagingPrefix, CancellationToken.None); }
        catch (Exception e) { logger.LogWarning(e, "Could not remove the staging files of import {JobId}", job.Id); }
    }

    // ---------- evaluation ----------

    /// <summary>The evaluated rows of a job with its current (or the given) mapping, decisions and catalogue.</summary>
    public sealed record Evaluation(ImportJob Job, IReadOnlyList<EvaluatedRow> Rows, PlaceResolver Places, ListResolver Lists);

    public async Task<Evaluation> EvaluateAsync(ImportJob job, ImportMapping? mapping, CancellationToken ct, bool validate = true)
    {
        mapping ??= MappingOf(job) ?? throw new ConflictException("import_state", "The workbook is still being read.");
        var rows = await StagedRowsAsync(job, ct);
        var decisions = Decisions(job);
        var places = await PlaceResolverAsync(decisions, ct);
        var lists = await ListResolverAsync(decisions, ct);
        var dates = decisions.Dates.GroupBy(d => RowEvaluator.DateKey(d.Text)).ToDictionary(g => g.Key, g => g.Last());
        var context = new EvaluationContext(mapping, Headings(job), job.ObjectTypeId.Value, new HashSet<string>(), places.Resolve, dates,
            job.SheetName, lists.Resolve);

        var evaluated = RowEvaluator.Evaluate(rows, context);
        var numbers = evaluated.Where(r => r.InventoryNumber is not null).Select(r => r.InventoryNumber!).ToList();
        var existing = await artefacts.ExistingInventoryNumbersAsync(numbers, ct);
        if (existing.Count > 0) evaluated = RowEvaluator.Evaluate(rows, context with { ExistingInventoryNumbers = existing });

        if (validate) evaluated = await ValidateRowsAsync(evaluated, ct);
        return new Evaluation(job, evaluated, places, lists);
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
            "collectionGroupId" => ListTextOf(row, ImportField.CollectionGroup),
            "dynastyId" => ListTextOf(row, ImportField.Dynasty),
            "holderId" => ListTextOf(row, ImportField.Holder),
            "storageLocationId" => ListTextOf(row, ImportField.StorageLocation),
            "acquisition.sourceId" => ListTextOf(row, ImportField.AcquisitionSource),
            "creationDate" => r?.CreationDate?.Text,
            "catalogueEntryEn" => r?.CatalogueEntryEn,
            _ when key.StartsWith("placeOfProduction") => r?.PlaceOfProduction?.Text,
            _ when key.StartsWith("exhibitions") => string.Join(" · ", r?.Exhibitions ?? []),
            _ when key.StartsWith("descriptions") => r?.Descriptions?.Values.FirstOrDefault(),
            _ => null,
        };
    }

    private static string? ListTextOf(EvaluatedRow row, ImportField field) => row.ListTexts.FirstOrDefault(t => t.Field == field)?.Text;

    /// <summary>
    /// Resolves list texts with the saved decisions, else an exact match on an entry's names (any language) or on a
    /// text linked to it in a template. A "create" decision counts as decided; its entry exists once committed.
    /// </summary>
    public sealed class ListResolver(
        IReadOnlyDictionary<(ImportList, string), ListDecision> saved,
        IReadOnlyDictionary<ImportList, List<NameCandidate>> candidates)
    {
        public IReadOnlyDictionary<(ImportList, string), ListDecision> Saved { get; } = saved;

        public IReadOnlyList<NameCandidate> Candidates(ImportList list) => candidates.TryGetValue(list, out var c) ? c : [];

        public ListDecision? Default(ImportList list, string text)
        {
            var exact = NameMatcher.Suggest(text, Candidates(list)).FirstOrDefault(s => s.Exact);
            return exact is null ? null : new ListDecision(list, text, ListAction.Link, exact.Id);
        }

        public ListDecision? InForce(ImportList list, string text) =>
            Saved.GetValueOrDefault((list, NameMatcher.Normalize(text))) ?? Default(list, text);

        public ResolvedEntry Resolve(ImportList list, string text) => InForce(list, text) switch
        {
            { EntryId: { } id } => new ResolvedEntry(id, false),
            { Action: ListAction.Create } => new ResolvedEntry(null, true),
            _ => ResolvedEntry.Undecided,
        };
    }

    public async Task<ListResolver> ListResolverAsync(ImportDecisions decisions, CancellationToken ct)
    {
        var candidates = new Dictionary<ImportList, List<NameCandidate>>();
        foreach (var list in AllLists.Where(l => l != ImportList.Party))
        {
            var entries = await listEntries.SearchAsync(ListKindOf(list), null, ct);
            candidates[list] = entries.SelectMany(e => e.Kind.IsTranslated()
                ? e.Names.Select(n => new NameCandidate(e.Id.Value, n.Name))
                : [new NameCandidate(e.Id.Value, e.Name!)]).ToList();
        }
        candidates[ImportList.Party] = (await parties.SearchAsync(null, null, ct)).Select(p => new NameCandidate(p.Id.Value, p.Name)).ToList();

        // Texts linked to an entry in any template count as names of that entry (while the entry exists).
        var known = candidates.ToDictionary(c => c.Key, c => c.Value.Select(x => x.Id).ToHashSet());
        foreach (var template in await templates.ListAsync(ct))
        {
            foreach (var d in ReadDecisions(template.Decisions).Lists)
                if (d.EntryId is { } entryId && known.TryGetValue(d.List, out var ids) && ids.Contains(entryId))
                    candidates[d.List].Add(new NameCandidate(entryId, d.Text, Alias: true));
        }

        var saved = new Dictionary<(ImportList, string), ListDecision>();
        foreach (var d in decisions.Lists) saved[(d.List, NameMatcher.Normalize(d.Text))] = d;
        return new ListResolver(saved, candidates);
    }

    /// <summary>Resolves place texts with the saved decisions, else an exact match on a region's names or aliases.</summary>
    public sealed class PlaceResolver(IReadOnlyDictionary<string, PlaceDecision> saved, IReadOnlyList<NameCandidate> candidates)
    {
        public IReadOnlyDictionary<string, PlaceDecision> Saved { get; } = saved;
        public IReadOnlyList<NameCandidate> Candidates { get; } = candidates;

        public PlaceDecision? Default(string text)
        {
            var exact = NameMatcher.Suggest(text, Candidates).FirstOrDefault(s => s.Exact);
            return exact is null ? null : new PlaceDecision(text, PlaceAction.Link, exact.Id);
        }

        public ResolvedPlace Resolve(string text)
        {
            var decision = Saved.GetValueOrDefault(NameMatcher.Normalize(text)) ?? Default(text);
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
        var candidates = new List<NameCandidate>();
        foreach (var region in await regions.SearchAsync(null, ct))
            candidates.AddRange(region.Names.Select(n => new NameCandidate(region.Id.Value, n.Name)));

        // Texts linked to a region in any template count as names of that region (while the region exists).
        var known = candidates.Select(c => c.Id).ToHashSet();
        foreach (var template in await templates.ListAsync(ct))
        {
            foreach (var d in ReadDecisions(template.Decisions).Places)
                if (d.RegionId is { } regionId && d.Action is PlaceAction.Link or PlaceAction.Create && known.Contains(regionId))
                    candidates.Add(new NameCandidate(regionId, d.Text, Alias: true));
        }

        var saved = decisions.Places.GroupBy(d => NameMatcher.Normalize(d.Text)).ToDictionary(g => g.Key, g => g.Last());
        return new PlaceResolver(saved, candidates);
    }

    /// <summary>The names of every region, list entry and party, in the reader's language (identifiers are unique across them).</summary>
    private async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(CancellationToken ct)
    {
        var languages = preferences.Current;
        var names = (await regions.SearchAsync(null, ct)).ToDictionary(r => r.Id.Value, r => r.NameIn(languages)?.Text ?? "");
        foreach (var list in AllLists.Where(l => l != ImportList.Party))
            foreach (var e in await listEntries.SearchAsync(ListKindOf(list), null, ct))
                names[e.Id.Value] = e.NameIn(languages)?.Text ?? "";
        foreach (var p in await parties.SearchAsync(null, null, ct)) names[p.Id.Value] = p.Name;
        return names;
    }

    // ---------- review rows ----------

    private ImportReviewDto Review(Evaluation evaluation, IReadOnlyDictionary<Guid, string> names, ImportRowStatus? status, int page, int pageSize)
    {
        var rows = evaluation.Rows;
        var counts = new ImportCountsDto(rows.Count, rows.Count(r => r.Status == ImportRowStatus.Ready),
            rows.Count(r => r.Status == ImportRowStatus.Skipped), rows.Count(r => r.Status == ImportRowStatus.Problem), 0);
        var filtered = status is { } s ? rows.Where(r => r.Status == s).ToList() : rows.ToList();
        (page, pageSize) = (Math.Max(1, page), Math.Clamp(pageSize, 1, 200));
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new ReviewRowDto(r.Row.SheetRow, r.Status, r.InventoryNumber, r.Request?.Title, r.Row.GroupLabel,
                Fields(r, evaluation, names), Pictures(r.Row.Pictures), r.Problems, r.Warnings, null))
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

    private static IReadOnlyList<ReviewFieldDto> Fields(EvaluatedRow row, Evaluation evaluation, IReadOnlyDictionary<Guid, string> names)
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
            var resolved = evaluation.Places.Resolve(place.Text!);
            Add(ImportField.PlaceOfProduction, place.Text + resolved switch
            {
                { PendingCreate: true } => " → new region",
                { RegionId: { } id } => names.TryGetValue(id, out var name) && name.Length > 0 ? $" → {name}" : " → linked region",
                _ => "",
            });
        }

        // List values and parties: the text as written and the entry it links to.
        void AddListValue(ImportField field)
        {
            if (row.ListTexts.FirstOrDefault(t => t.Field == field) is not { } listText) return;
            var decision = evaluation.Lists.InForce(listText.List, listText.Text);
            Add(field, listText.Text + decision switch
            {
                { EntryId: { } id } => names.TryGetValue(id, out var name) && name.Length > 0 ? $" → {name}" : " → linked entry",
                { Action: ListAction.Create } =>
                    $" → {decision.Name ?? decision.Names?.Values.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? listText.Text} (new)",
                _ => " (not decided)",
            });
        }

        AddListValue(ImportField.CollectionGroup);

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

        AddListValue(ImportField.Dynasty);
        Add(ImportField.Description, r.Descriptions?.Values.FirstOrDefault());
        if (r.Acquisition is { } a)
        {
            AddListValue(ImportField.AcquisitionSource);
            Add(ImportField.AcquisitionDate, a.Date?.ToString("d MMMM yyyy", CultureInfo.InvariantCulture));
            Add(ImportField.AcquisitionLot, a.Lot);
            Add(ImportField.AcquisitionPrice, a.Price is { } price ? Amount(price) : null);
            Add(ImportField.AcquisitionNote, a.Note);
        }

        Add(ImportField.Exhibitions, string.Join("\n", r.Exhibitions ?? []));
        Add(ImportField.CatalogueEntryEn, r.CatalogueEntryEn);
        AddListValue(ImportField.Holder);
        AddListValue(ImportField.StorageLocation);
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
        return new ImportDecisions(d.Places ?? [], d.Dates ?? [], d.Lists ?? []);
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
