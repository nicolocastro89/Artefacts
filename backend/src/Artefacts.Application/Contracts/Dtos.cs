using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Dating;
using Artefacts.Domain.Lists;
using Artefacts.Domain.Media;
using Artefacts.Domain.Parties;

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

public sealed record AcquisitionDto(
    AcquisitionMethod Method, DateOnly? Date, PartyRefDto? Source, MoneyDto? Price, string? Lot = null, string? Note = null);

/// <summary>A reference to another aggregate together with its current display name.</summary>
public sealed record NamedRefDto(Guid Id, string Name);

/// <summary>A reference to a party with its name and kind.</summary>
public sealed record PartyRefDto(Guid Id, string Name, PartyKind Kind);

/// <summary>Where an artefact was made: the text as written and the linked region, if any.</summary>
public sealed record PlaceOfProductionDto(string? Text, NamedRefDto? Region);

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
    string? DescriptionLanguage,
    IReadOnlyDictionary<string, string> Descriptions,
    string Language,
    AcquisitionDto? Acquisition,
    IReadOnlyList<ProvenanceEntryDto> Provenance,
    ArtefactImageDto? PrimaryImage,
    IReadOnlyList<ArtefactImageDto> Images,
    NamedRefDto? ObjectType,
    NamedRefDto? CollectionGroup,
    PlaceOfProductionDto? PlaceOfProduction,
    NamedRefDto? Dynasty,
    IReadOnlyList<string> Exhibitions,
    string? CatalogueEntryEn,
    PartyRefDto? Holder,
    NamedRefDto? StorageLocation,
    MoneyDto? Valuation);

public sealed record ArtefactListItem(
    Guid Id,
    string InventoryNumber,
    string Title,
    string? Maker,
    HistoricalDateDto? CreationDate,
    string? Medium,
    ImageUrls? PrimaryImage,
    int ImageCount,
    string? ObjectType,
    string? CollectionGroup,
    string? PlaceOfProduction,
    string? Dynasty,
    string? GroupKey,
    string? GroupLabel);

/// <summary>Grouping of the artefact list. <see cref="None"/> keeps the plain sorted list.</summary>
public enum ArtefactGrouping
{
    None = 0,
    ObjectType = 1,
    CollectionGroup = 2,
    Origin = 3,
    Century = 4,
    Dynasty = 5,
}

/// <summary>
/// One group of the current search: <see cref="Key"/> and <see cref="Label"/> are null for the final group of
/// artefacts without a value for the grouping field.
/// </summary>
public sealed record ArtefactGroupDto(string? Key, string? Label, int Count);

/// <summary>A page of the artefact list, with every group of the whole search when the list is grouped.</summary>
public sealed record ArtefactListPage(
    IReadOnlyList<ArtefactListItem> Items, int Total, int Page, int PageSize, IReadOnlyList<ArtefactGroupDto> Groups);

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
    int PageSize = 25,
    ArtefactGrouping Group = ArtefactGrouping.None);

public sealed record AcquisitionInput(
    AcquisitionMethod Method, DateOnly? Date, Guid? SourceId, MoneyDto? Price, string? Lot = null, string? Note = null);

public sealed record PlaceOfProductionInput(string? Text, Guid? RegionId = null);

public sealed record ProvenanceEntryInput(Guid RegionId, HistoricalDateInput? Period, string? Note);

public sealed record ArtefactWriteRequest(
    string? InventoryNumber,
    string? Title,
    string? Maker = null,
    HistoricalDateInput? CreationDate = null,
    string? Medium = null,
    string? Dimensions = null,
    IReadOnlyDictionary<string, string?>? Descriptions = null,
    AcquisitionInput? Acquisition = null,
    IReadOnlyList<ProvenanceEntryInput>? Provenance = null,
    Guid? ObjectTypeId = null,
    Guid? CollectionGroupId = null,
    PlaceOfProductionInput? PlaceOfProduction = null,
    Guid? DynastyId = null,
    IReadOnlyList<string>? Exhibitions = null,
    string? CatalogueEntryEn = null,
    Guid? HolderId = null,
    Guid? StorageLocationId = null,
    MoneyDto? Valuation = null);

public sealed record SetCaptionRequest(string? Caption);

// ---------- Lists and parties ----------

/// <summary>
/// An entry of a managed list. <see cref="Name"/> is resolved in the reader's language; translated lists (dynasties,
/// collection groups) also give <see cref="NameLanguage"/> and every <see cref="Names"/>, storage locations neither.
/// </summary>
public sealed record ReferenceEntryDto(
    Guid Id, ListKind Kind, string Name, string? NameLanguage, IReadOnlyDictionary<string, string>? Names, int ArtefactCount);

/// <summary>Names per language for dynasties and collection groups, or the single name of a storage location.</summary>
public sealed record ReferenceEntryNamesRequest(IReadOnlyDictionary<string, string?>? Names = null, string? Name = null);

public sealed record AddReferenceEntryResult(ReferenceEntryDto Entry, bool Created);

/// <summary>A party with the number of artefacts acquired from it and held by it.</summary>
public sealed record PartyDto(Guid Id, string Name, PartyKind Kind, int SourceCount, int HolderCount);

/// <summary>A party's name and kind (organisation when adding without a kind; kept when updating without one).</summary>
public sealed record PartyRequest(string? Name, PartyKind? Kind = null);

public sealed record AddPartyResult(PartyDto Party, bool Created);

// ---------- Object types ----------

public sealed record ObjectTypeDto(Guid Id, string Name, string NameLanguage, IReadOnlyDictionary<string, string> Names, int ArtefactCount);

/// <summary>Names keyed by content language code, e.g. { "it": "Tappeti", "en": "Carpets" }.</summary>
public sealed record AddObjectTypeRequest(IReadOnlyDictionary<string, string?>? Names);

/// <summary>Outcome of adding an object type: <see cref="Created"/> is false when an existing one was returned.</summary>
public sealed record AddObjectTypeResult(ObjectTypeDto ObjectType, bool Created);

// ---------- Regions ----------

public sealed record RegionDto(
    Guid Id, string Name, string NameLanguage, IReadOnlyDictionary<string, string> Names, Guid? ParentId, string? ParentName);

/// <summary>Names keyed by content language code, e.g. { "it": "Turchia", "en": "Turkey" }.</summary>
public sealed record AddRegionRequest(IReadOnlyDictionary<string, string?>? Names, Guid? ParentId = null);

/// <summary>Replaces all names of a region or object type; a language left out has no name afterwards.</summary>
public sealed record SetNamesRequest(IReadOnlyDictionary<string, string?>? Names);

// ---------- Languages ----------

public sealed record LanguageDto(string Code, string Name, int Position);

public sealed record AddLanguageRequest(string? Code, string? Name);

/// <summary>Every content language code, in the new fallback order.</summary>
public sealed record SetLanguageOrderRequest(IReadOnlyList<string>? Codes);

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
