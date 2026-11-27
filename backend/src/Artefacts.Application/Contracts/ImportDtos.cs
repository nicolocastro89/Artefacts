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

public sealed record ImportColumnsDto(IReadOnlyList<ColumnSummary> Columns);

/// <summary>
/// A detector a line rule can use: built-in ones (<see cref="IsSystem"/>, written in code and read-only; some come with a definition that can be copied) and
/// the user's stored ones, with the definition they were saved with. <see cref="Id"/> is what a line rule stores.
/// </summary>
public sealed record ImportDetectorDto(string Id, string Label, string? Example, bool IsSystem = true, DetectorDefinition? Definition = null, DateTimeOffset? UpdatedAt = null);

/// <summary>A detector to save: its name and what it recognises.</summary>
public sealed record SaveImportDetectorRequest(string? Name, DetectorDefinition? Definition);

public sealed record DuplicateImportDetectorRequest(string? Name);

/// <summary>A detector definition (not stored) and sample lines to run it against.</summary>
public sealed record TryImportDetectorRequest(DetectorDefinition? Definition, IReadOnlyList<string>? Lines);

/// <summary>What the detector made of one sample line: <see cref="Matched"/> false when it does not recognise the line.</summary>
public sealed record TryImportDetectorLine(string Line, bool Matched, string? Text, object? Parsed, string? Remainder, string? ItemNumber, bool Guessed);

public sealed record TryImportDetectorResult(IReadOnlyList<TryImportDetectorLine> Lines);

/// <summary>One entry of a lookup table: <see cref="IsUser"/> when a user added or changed it; <see cref="BuiltInValue"/> is what a changed built-in entry says without the change.</summary>
public sealed record ImportLookupEntryDto(string Word, string Value, bool IsUser, string? BuiltInValue);

/// <summary>A lookup table of words the parsers read (month names, currency words, measurement labels, spellings of the inch), built-in entries with the user's.</summary>
public sealed record ImportLookupTableDto(string Id, string Label, IReadOnlyList<ImportLookupEntryDto> Entries);

public sealed record ImportLookupEntryInput(string? Word, string? Value);

/// <summary>The user entries of a table; saving replaces them all, and none puts the table back to the built-in entries.</summary>
public sealed record SaveImportLookupTableRequest(IReadOnlyList<ImportLookupEntryInput>? Entries);

/// <summary>A field a column can be mapped to, from the field registry: <see cref="SingleValued"/> fields take one value per row, <see cref="Curated"/> ones are linked to a managed list.</summary>
public sealed record ImportFieldDto(ImportField Id, string Label, bool SingleValued, bool Curated);

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

/// <summary>An entry (or party) suggested for a list text: its name in the reader's language and the name that matched.</summary>
public sealed record ListSuggestionDto(Guid EntryId, string Name, string MatchedName, double Score, bool Exact, bool Alias);

/// <summary>
/// A distinct text the workbook gives for a list: how many rows use it, the fields it appears in (a party text can be
/// both source and holder), the decision in force (saved, or else an exact match linked by default; none means
/// undecided), and up to three similar entries.
/// </summary>
public sealed record ImportListValueDto(
    string Text,
    int Rows,
    IReadOnlyList<ImportField> Fields,
    ListDecision? Decision,
    bool Decided,
    IReadOnlyList<ListSuggestionDto> Suggestions);

/// <summary>The texts of one list, most used first, with the number still undecided.</summary>
public sealed record ImportListGroupDto(ImportList List, IReadOnlyList<ImportListValueDto> Values, int Undecided);

public sealed record SetListDecisionsRequest(IReadOnlyList<ListDecision>? Lists);

/// <summary>A distinct text of a kind of resolution (dates the rules cannot read, dimensions that were unclear) with the rows using it and the decision in force (none means undecided).</summary>
public sealed record ImportResolutionItemDto(string Text, IReadOnlyList<int> SheetRows, ImportResolution? Resolution);

public sealed record SetImportResolutionsRequest(IReadOnlyList<ImportResolution>? Resolutions);

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
    Guid? ArtefactId,
    IReadOnlyList<string>? Discarded = null);

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
