using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Dating;
using Artefacts.Domain.Media;

namespace Artefacts.Application.Contracts;

// ---------- Shared ----------

/// <summary>Signed URLs for the sized renditions of one image.</summary>
public sealed record ImageUrls(string Thumb, string Web, string Print);

public sealed record HistoricalDateDto(string Display, int? EarliestYear, int? LatestYear, HistoricalDatePrecision Precision)
{
    public static HistoricalDateDto? From(HistoricalDate? d) =>
        d is null ? null : new(d.Display, d.EarliestYear, d.LatestYear, d.Precision);
}

/// <summary>Historical date as entered by a user: free text, optionally with an explicit year range.</summary>
public sealed record HistoricalDateInput(string? Text, int? EarliestYear = null, int? LatestYear = null);

public sealed record MoneyDto(decimal Amount, string Currency);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

// ---------- Artefacts ----------

public sealed record AcquisitionDto(AcquisitionMethod Method, DateOnly? Date, string? Source, MoneyDto? Price);

public sealed record ProvenanceEntryDto(Guid RegionId, string RegionName, HistoricalDateDto Period, string? Note);

public sealed record ArtefactImageDto(
    Guid MediaAssetId,
    string OriginalFileName,
    ImageFormat Format,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    string? Caption,
    bool IsPrimary,
    ImageUrls Urls);

public sealed record ArtefactDetail(
    Guid Id,
    string InventoryNumber,
    string Title,
    string? Maker,
    HistoricalDateDto? CreationDate,
    string? Medium,
    string? Dimensions,
    string? Description,
    AcquisitionDto? Acquisition,
    IReadOnlyList<ProvenanceEntryDto> Provenance,
    ArtefactImageDto? PrimaryImage,
    IReadOnlyList<ArtefactImageDto> Images);

public sealed record ArtefactListItem(
    Guid Id,
    string InventoryNumber,
    string Title,
    string? Maker,
    HistoricalDateDto? CreationDate,
    string? Medium,
    ImageUrls? PrimaryImage,
    int ImageCount);

public enum ArtefactSort
{
    InventoryNumber = 0,
    Title = 1,
    CreationDate = 2,
}

public enum SortDirection
{
    Asc = 0,
    Desc = 1,
}

public sealed record ArtefactListRequest(
    string? Search = null,
    ArtefactSort Sort = ArtefactSort.InventoryNumber,
    SortDirection Direction = SortDirection.Asc,
    int Page = 1,
    int PageSize = 25);

public sealed record AcquisitionInput(AcquisitionMethod Method, DateOnly? Date, string? Source, MoneyDto? Price);

public sealed record ProvenanceEntryInput(Guid RegionId, HistoricalDateInput? Period, string? Note);

public sealed record ArtefactWriteRequest(
    string? InventoryNumber,
    string? Title,
    string? Maker = null,
    HistoricalDateInput? CreationDate = null,
    string? Medium = null,
    string? Dimensions = null,
    string? Description = null,
    AcquisitionInput? Acquisition = null,
    IReadOnlyList<ProvenanceEntryInput>? Provenance = null);

public sealed record SetCaptionRequest(string? Caption);

// ---------- Regions ----------

public sealed record RegionDto(Guid Id, string Name, Guid? ParentId, string? ParentName);

public sealed record AddRegionRequest(string? Name, Guid? ParentId = null);

public sealed record RenameRegionRequest(string? Name);

public sealed record SetRegionParentRequest(Guid? ParentId);

/// <summary>Result of an add: the region, and whether it was newly created (false = an existing match was returned).</summary>
public sealed record AddRegionResult(RegionDto Region, bool Created);

// ---------- Field catalog ----------

public enum FieldType
{
    Text,
    Number,
    Date,
    HistoricalDate,
    Money,
    Image,
    Reference,
    Boolean,
}

public sealed record FieldDto(
    string Id,
    string Label,
    FieldType Type,
    bool Filterable,
    bool Sortable,
    string? Sensitivity,
    IReadOnlyList<string> Columns);

public sealed record EntityDto(string Id, string Label, string View, IReadOnlyList<FieldDto> Fields);

public sealed record FieldCatalogDto(IReadOnlyList<EntityDto> Entities);
