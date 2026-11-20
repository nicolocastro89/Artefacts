using Artefacts.Application.Imports;
using Artefacts.Domain.Imports;

namespace Artefacts.Application.Contracts;

// ---------- Jobs ----------

public sealed record ImportCountsDto(int Total, int Ready, int Skipped, int Problems, int Imported);

public sealed record ImportJobDto(
    Guid Id,
    string FileName,
    long FileSize,
    ImportJobState State,
    NamedRefDto ObjectType,
    NamedRefDto? Template,
    string? SheetName,
    IReadOnlyList<string> SheetNames,
    int GroupHeadings,
    int TotalRows,
    int ProcessedRows,
    int ImportedRows,
    int SkippedRows,
    int ProblemRows,
    string? Error,
    IReadOnlyList<string> Warnings,
    ImportMapping? Mapping,
    ImportDecisions Decisions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CommittedAt,
    DateTimeOffset? FinishedAt);

// ---------- Mapping ----------

public sealed record ImportColumnsDto(IReadOnlyList<ColumnSummary> Columns, IReadOnlyList<string> Fields, IReadOnlyList<string> Detectors);

/// <summary>A mapping saved on a job, with the warnings it raises (for example a line-rule column without a rest field).</summary>
public sealed record ImportMappingResult(ImportJobDto Job, IReadOnlyList<string> Warnings);

// ---------- Places and dates ----------

public sealed record PlaceSuggestionDto(Guid RegionId, string RegionName, string MatchedName, double Score, bool Exact, bool Alias);

/// <summary>
/// A distinct place text of the workbook: how many rows use it, the decision in force (saved, or else an exact match
/// linked by default), and up to three similar regions.
/// </summary>
public sealed record ImportPlaceDto(
    string Text,
    int Rows,
    PlaceDecision? Decision,
    bool Decided,
    IReadOnlyList<PlaceSuggestionDto> Suggestions);

public sealed record SetPlaceDecisionsRequest(IReadOnlyList<PlaceDecision>? Places);

/// <summary>A distinct date text the historical date rules cannot interpret, with the rows using it.</summary>
public sealed record ImportDateDto(string Text, IReadOnlyList<int> SheetRows, DateResolution? Resolution);

public sealed record SetDateResolutionsRequest(IReadOnlyList<DateResolution>? Dates);

// ---------- Review ----------

public sealed record ReviewFieldDto(ImportField Field, string Value);

public sealed record ReviewPictureDto(int Index, string Name, bool Supported, string? ThumbUrl);

public sealed record ReviewRowDto(
    int SheetRow,
    ImportRowStatus Status,
    string? InventoryNumber,
    string? Title,
    string? GroupLabel,
    IReadOnlyList<ReviewFieldDto> Fields,
    IReadOnlyList<ReviewPictureDto> Pictures,
    IReadOnlyList<RowProblem> Problems,
    IReadOnlyList<string> Warnings,
    Guid? ArtefactId);

public sealed record ImportReviewDto(ImportCountsDto Counts, IReadOnlyList<ReviewRowDto> Rows, int Total, int Page, int PageSize);

/// <summary>An unsaved mapping to preview against the first rows.</summary>
public sealed record PreviewMappingRequest(ImportMapping? Mapping, int? Rows = null);

// ---------- Report ----------

public sealed record ImportReportRowDto(
    int SheetRow,
    ImportRowStatus Status,
    string? InventoryNumber,
    Guid? ArtefactId,
    IReadOnlyList<RowProblem> Problems,
    IReadOnlyList<string> Warnings);

public sealed record ImportReportDto(ImportJobDto Job, IReadOnlyList<ImportReportRowDto> Rows);

// ---------- Templates ----------

public sealed record ImportTemplateDto(Guid Id, string Name, ImportMapping Mapping, ImportDecisions Decisions, DateTimeOffset UpdatedAt);

/// <summary>Saves a template from a job's mapping and decisions, or from the mapping and decisions given.</summary>
public sealed record SaveImportTemplateRequest(string? Name, Guid? FromJobId = null, ImportMapping? Mapping = null, ImportDecisions? Decisions = null);
