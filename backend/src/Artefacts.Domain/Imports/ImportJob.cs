using Artefacts.Domain.Classification;
using Artefacts.Domain.Common;

namespace Artefacts.Domain.Imports;

public enum ImportJobState
{
    Reading = 0,
    AwaitingMapping = 1,
    ReadyForReview = 2,
    Importing = 3,
    Finished = 4,
    Failed = 5,
    Discarded = 6,
}

public enum ImportRowStatus
{
    Pending = 0,
    Ready = 1,
    Problem = 2,
    Imported = 3,

    /// <summary>Not imported because an artefact with its inventory number is already in the catalogue.</summary>
    Skipped = 4,
}

/// <summary>
/// One import of a workbook: read in the background, mapped and reviewed by the user, then committed row by row by
/// the worker. Mapping, decisions and staged values are JSON documents owned by the application layer.
/// </summary>
public sealed class ImportJob
{
    public const int MaxFileNameLength = 255;

    public Guid Id { get; private set; }
    public string FileName { get; private set; } = default!;
    public long FileSize { get; private set; }
    public ImportJobState State { get; private set; }
    public ObjectTypeId ObjectTypeId { get; private set; }
    public Guid? TemplateId { get; private set; }

    /// <summary>The sheet asked for at upload; afterwards the sheet that was read.</summary>
    public string? SheetName { get; private set; }

    public string SheetNames { get; private set; } = "[]";
    public string Headings { get; private set; } = "{}";
    public string? Mapping { get; private set; }
    public string Decisions { get; private set; } = "{}";
    public string Warnings { get; private set; } = "[]";
    public int GroupHeadings { get; private set; }

    public int TotalRows { get; private set; }
    public int ProcessedRows { get; private set; }
    public int ImportedRows { get; private set; }
    public int SkippedRows { get; private set; }
    public int ProblemRows { get; private set; }
    public string? Error { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CommittedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Until when the worker that claimed the job holds it; another worker may take it over afterwards.</summary>
    public DateTimeOffset? LeaseUntil { get; private set; }

    // For EF Core.
    private ImportJob()
    {
    }

    public static ImportJob Create(string? fileName, long fileSize, Guid objectTypeId, Guid? templateId, string? sheet,
        string? mapping, string decisions, DateTimeOffset now)
    {
        var name = FileNames.Leaf(fileName).Trim();
        if (name.Length == 0) throw new DomainValidationException("file", "The file has no name.");
        if (name.Length > MaxFileNameLength) name = name[^MaxFileNameLength..];
        return new ImportJob
        {
            Id = Guid.NewGuid(),
            FileName = name,
            FileSize = fileSize,
            ObjectTypeId = new ObjectTypeId(objectTypeId),
            TemplateId = templateId,
            SheetName = string.IsNullOrWhiteSpace(sheet) ? null : sheet.Trim(),
            Mapping = mapping,
            Decisions = decisions,
            State = ImportJobState.Reading,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>Storage prefix of everything staged for this job (the workbook and its pictures).</summary>
    public string StagingPrefix => $"imports/{Id:N}/";

    public string SourceKey => StagingPrefix + "source.xlsx";

    public bool IsEditable => State is ImportJobState.AwaitingMapping or ImportJobState.ReadyForReview;

    public bool IsTerminal => State is ImportJobState.Finished or ImportJobState.Failed or ImportJobState.Discarded;

    public void MarkRead(string sheetName, string sheetNames, string headings, int totalRows, int groupHeadings,
        string warnings, string mapping, DateTimeOffset now)
    {
        Require(ImportJobState.Reading);
        SheetName = sheetName;
        SheetNames = sheetNames;
        Headings = headings;
        TotalRows = totalRows;
        GroupHeadings = groupHeadings;
        Warnings = warnings;
        Mapping = mapping;
        State = ImportJobState.AwaitingMapping;
        LeaseUntil = null;
        UpdatedAt = now;
    }

    public void Fail(string error, DateTimeOffset now)
    {
        Error = error.Length > 2000 ? error[..2000] : error;
        State = ImportJobState.Failed;
        LeaseUntil = null;
        UpdatedAt = now;
        FinishedAt = now;
    }

    public void SetMapping(string mapping, DateTimeOffset now)
    {
        RequireEditable();
        Mapping = mapping;
        State = ImportJobState.ReadyForReview;
        UpdatedAt = now;
    }

    public void SetDecisions(string decisions, DateTimeOffset now)
    {
        RequireEditable();
        Decisions = decisions;
        UpdatedAt = now;
    }

    /// <summary>The worker records decisions too, e.g. the identifiers of regions it created.</summary>
    public void RecordDecisions(string decisions, DateTimeOffset now)
    {
        Require(ImportJobState.Importing);
        Decisions = decisions;
        UpdatedAt = now;
    }

    public void Commit(DateTimeOffset now)
    {
        RequireEditable();
        State = ImportJobState.Importing;
        CommittedAt = now;
        UpdatedAt = now;
    }

    public void RecordProgress(int processed, int imported, int skipped, int problems, DateTimeOffset now)
    {
        ProcessedRows = processed;
        ImportedRows = imported;
        SkippedRows = skipped;
        ProblemRows = problems;
        UpdatedAt = now;
    }

    public void Finish(DateTimeOffset now)
    {
        Require(ImportJobState.Importing);
        State = ImportJobState.Finished;
        LeaseUntil = null;
        FinishedAt = now;
        UpdatedAt = now;
    }

    public void Discard(DateTimeOffset now)
    {
        if (State is ImportJobState.Importing or ImportJobState.Finished)
            throw new ConflictException("import_committed", "An import that has been committed cannot be discarded.");
        if (State == ImportJobState.Discarded) return;
        State = ImportJobState.Discarded;
        LeaseUntil = null;
        FinishedAt = now;
        UpdatedAt = now;
    }

    private void RequireEditable()
    {
        if (!IsEditable)
            throw new ConflictException("import_state", State switch
            {
                ImportJobState.Reading => "The workbook is still being read.",
                ImportJobState.Importing => "The import is running and can no longer be changed.",
                _ => $"The import is {State.ToString().ToLowerInvariant()} and can no longer be changed.",
            });
    }

    private void Require(ImportJobState state)
    {
        if (State != state)
            throw new ConflictException("import_state", $"The import is {State} rather than {state}.");
    }
}

/// <summary>A record row of an import job, staged when the workbook was read.</summary>
public sealed class ImportRow
{
    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public int SheetRow { get; private set; }
    public string? GroupNumber { get; private set; }
    public string? GroupLabel { get; private set; }
    public string Cells { get; private set; } = "{}";
    public string Pictures { get; private set; } = "[]";
    public string Warnings { get; private set; } = "[]";
    public ImportRowStatus Status { get; private set; }
    public string Problems { get; private set; } = "[]";
    public string? InventoryNumber { get; private set; }
    public Guid? ArtefactId { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    // For EF Core.
    private ImportRow()
    {
    }

    public static ImportRow Stage(Guid jobId, int sheetRow, string? groupNumber, string? groupLabel, string cells, string pictures, string warnings) =>
        new()
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            SheetRow = sheetRow,
            GroupNumber = groupNumber,
            GroupLabel = groupLabel,
            Cells = cells,
            Pictures = pictures,
            Warnings = warnings,
            Status = ImportRowStatus.Pending,
        };

    public bool IsDone => Status is ImportRowStatus.Imported or ImportRowStatus.Skipped or ImportRowStatus.Problem;

    public void MarkImported(Guid artefactId, string inventoryNumber, string warnings, DateTimeOffset now)
    {
        if (Status == ImportRowStatus.Imported)
            throw new ConflictException("row_imported", $"Row {SheetRow} has already been imported.");
        Status = ImportRowStatus.Imported;
        ArtefactId = artefactId;
        InventoryNumber = inventoryNumber;
        Warnings = warnings;
        Problems = "[]";
        ProcessedAt = now;
    }

    public void MarkSkipped(string? inventoryNumber, DateTimeOffset now)
    {
        Status = ImportRowStatus.Skipped;
        InventoryNumber = inventoryNumber;
        ProcessedAt = now;
    }

    public void MarkProblem(string? inventoryNumber, string problems, string warnings, DateTimeOffset now)
    {
        Status = ImportRowStatus.Problem;
        InventoryNumber = inventoryNumber;
        Problems = problems;
        Warnings = warnings;
        ProcessedAt = now;
    }
}

/// <summary>A named, reusable mapping with its place decisions and date resolutions.</summary>
public sealed class ImportTemplate
{
    public const int MaxNameLength = 200;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string NormalizedName { get; private set; } = default!;
    public string Mapping { get; private set; } = default!;
    public string Decisions { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // For EF Core.
    private ImportTemplate()
    {
    }

    public static ImportTemplate Create(string? name, string mapping, string decisions, DateTimeOffset now)
    {
        var template = new ImportTemplate { Id = Guid.NewGuid(), CreatedAt = now };
        template.Update(name, mapping, decisions, now);
        return template;
    }

    public void Update(string? name, string mapping, string decisions, DateTimeOffset now)
    {
        Name = Guard.Required(name, "name", MaxNameLength);
        NormalizedName = Languages.LocalizedName.Normalize(Name);
        Mapping = mapping;
        Decisions = decisions;
        UpdatedAt = now;
    }
}
