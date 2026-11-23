using Artefacts.Application.Contracts;

namespace Artefacts.Infrastructure.Read;

/// <summary>One row of <c>v_artefact</c>: a flattened, read-only view of an artefact for listing and querying.</summary>
[CatalogEntity("Artefact", "Artefact")]
public sealed class ArtefactRow
{
    [CatalogField("id", "Identifier", FieldType.Reference), Filterable]
    public Guid Id { get; init; }

    [CatalogField("inventoryNumber", "Inventory number", FieldType.Text), Filterable, Sortable]
    public string InventoryNumber { get; init; } = default!;

    [CatalogField("title", "Title", FieldType.Text), Filterable, Sortable]
    public string Title { get; init; } = default!;

    [CatalogField("maker", "Maker", FieldType.Text), Filterable, Sortable]
    public string? Maker { get; init; }

    [CatalogField("creationDate", "Creation date", FieldType.HistoricalDate), Filterable, Sortable]
    public string? CreationDateDisplay { get; init; }

    [PartOf("creationDate")]
    public int? CreationDateEarliestYear { get; init; }

    [PartOf("creationDate")]
    public int? CreationDateLatestYear { get; init; }

    [PartOf("creationDate")]
    public string? CreationDatePrecision { get; init; }

    [CatalogField("medium", "Medium", FieldType.Text), Filterable, Sortable]
    public string? Medium { get; init; }

    [CatalogField("dimensions", "Dimensions", FieldType.Text), Filterable]
    public string? Dimensions { get; init; }

    [CatalogField("width", "Width", FieldType.Measurement)]
    public decimal? WidthValue { get; init; }

    [PartOf("width")]
    public Guid? WidthUnitId { get; init; }

    /// <summary>The width in metres, from the unit's current factor.</summary>
    [PartOf("width")]
    public decimal? WidthMetres { get; init; }

    [CatalogField("height", "Height", FieldType.Measurement)]
    public decimal? HeightValue { get; init; }

    [PartOf("height")]
    public Guid? HeightUnitId { get; init; }

    /// <summary>The height in metres, from the unit's current factor.</summary>
    [PartOf("height")]
    public decimal? HeightMetres { get; init; }

    [CatalogField("length", "Length", FieldType.Measurement)]
    public decimal? LengthValue { get; init; }

    [PartOf("length")]
    public Guid? LengthUnitId { get; init; }

    /// <summary>The length in metres, from the unit's current factor.</summary>
    [PartOf("length")]
    public decimal? LengthMetres { get; init; }

    [CatalogField("diameter", "Diameter", FieldType.Measurement)]
    public decimal? DiameterValue { get; init; }

    [PartOf("diameter")]
    public Guid? DiameterUnitId { get; init; }

    /// <summary>The diameter in metres, from the unit's current factor.</summary>
    [PartOf("diameter")]
    public decimal? DiameterMetres { get; init; }

    [CatalogField("objectType", "Object type", FieldType.Reference), Filterable, Sortable]
    public Guid? ObjectTypeId { get; init; }

    [CatalogField("collectionGroup", "Collection group", FieldType.Reference), Filterable, Sortable]
    public Guid? CollectionGroupId { get; init; }

    [CatalogField("placeOfProduction", "Place of production", FieldType.Text), Filterable, Sortable]
    public string? PlaceText { get; init; }

    [CatalogField("placeRegion", "Place of production (region)", FieldType.Reference), Filterable, Sortable]
    public Guid? PlaceRegionId { get; init; }

    [CatalogField("dynasty", "Dynasty or period", FieldType.Reference), Filterable, Sortable]
    public Guid? DynastyId { get; init; }

    /// <summary>Century of the creation date (17 = 1601-1700; negative for BCE).</summary>
    [CatalogField("century", "Century", FieldType.Number), Filterable, Sortable]
    public int? Century { get; init; }

    /// <summary>The holding party; null when the collection itself holds the artefact.</summary>
    [CatalogField("holder", "Holder", FieldType.Reference), Filterable, Sortable]
    public Guid? HolderId { get; init; }

    [CatalogField("storageLocation", "Storage location", FieldType.Reference), Filterable, Sortable]
    public Guid? StorageLocationId { get; init; }

    [CatalogField("acquisitionMethod", "Acquisition method", FieldType.Text), Filterable, Sortable]
    public string? AcquisitionMethod { get; init; }

    [CatalogField("acquisitionDate", "Acquisition date", FieldType.Date), Filterable, Sortable]
    public DateOnly? AcquisitionDate { get; init; }

    [CatalogField("acquisitionSource", "Acquisition source", FieldType.Reference), Filterable, Sortable]
    public Guid? AcquisitionSourceId { get; init; }

    [CatalogField("acquisitionLot", "Acquisition lot", FieldType.Text), Filterable]
    public string? AcquisitionLot { get; init; }

    [CatalogField("acquisitionNote", "Acquisition note", FieldType.Text), Filterable]
    public string? AcquisitionNote { get; init; }

    [CatalogField("acquisitionPrice", "Acquisition price", FieldType.Money), Filterable, Sortable, Sensitive("financial")]
    public decimal? AcquisitionPriceAmount { get; init; }

    [PartOf("acquisitionPrice"), Sensitive("financial")]
    public string? AcquisitionPriceCurrency { get; init; }

    [CatalogField("valuation", "Valuation", FieldType.Money), Filterable, Sortable, Sensitive("financial")]
    public decimal? ValuationAmount { get; init; }

    [PartOf("valuation"), Sensitive("financial")]
    public string? ValuationCurrency { get; init; }

    [CatalogField("exhibitionCount", "Number of exhibitions", FieldType.Number), Filterable, Sortable]
    public int ExhibitionCount { get; init; }

    [CatalogField("primaryImage", "Primary image", FieldType.Image)]
    public string? PrimaryImageKey { get; init; }

    [CatalogField("imageCount", "Number of images", FieldType.Number), Filterable, Sortable]
    public int ImageCount { get; init; }
}
