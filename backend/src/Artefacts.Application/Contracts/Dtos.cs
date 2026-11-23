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

/// <summary>A unit of measure as referenced by a measurement: its resolved name, abbreviation and metres per unit.</summary>
public sealed record UnitRefDto(Guid Id, string Name, string Abbreviation, decimal Factor);

/// <summary>A measurement in the unit it was entered in, with its value converted to metres.</summary>
public sealed record MeasurementDto(decimal Value, decimal InMetres, UnitRefDto Unit);

/// <summary>A measurement as entered: a positive value and the unit's identifier.</summary>
public sealed record MeasurementInput(decimal? Value, Guid? UnitId);

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
    string? CatalogueEntry,
    string? CatalogueEntryLanguage,
    IReadOnlyDictionary<string, string> CatalogueEntries,
    PartyRefDto? Holder,
    NamedRefDto? StorageLocation,
    MoneyDto? Valuation,
    MeasurementDto? Width = null,
    MeasurementDto? Height = null,
    MeasurementDto? Length = null,
    MeasurementDto? Diameter = null);

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
    IReadOnlyDictionary<string, string?>? CatalogueEntries = null,
    Guid? HolderId = null,
    Guid? StorageLocationId = null,
    MoneyDto? Valuation = null,
    MeasurementInput? Width = null,
    MeasurementInput? Height = null,
    MeasurementInput? Length = null,
    MeasurementInput? Diameter = null);

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

// ---------- Units of measure ----------

/// <summary>A unit with its resolved and per-language names, abbreviation, metres per unit and usage.</summary>
public sealed record UnitOfMeasureDto(
    Guid Id, string Name, string NameLanguage, IReadOnlyDictionary<string, string> Names, string Abbreviation, decimal Factor, int ArtefactCount);

/// <summary>A unit's names keyed by content language, its abbreviation, and its factor (metres per one of the unit).</summary>
public sealed record UnitOfMeasureRequest(IReadOnlyDictionary<string, string?>? Names, string? Abbreviation, decimal? Factor);

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

    /// <summary>A positive value with a unit of measure (and its value in metres).</summary>
    Measurement,
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
