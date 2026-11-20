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

    [CatalogField("objectType", "Object type", FieldType.Reference), Filterable, Sortable]
    public Guid? ObjectTypeId { get; init; }

    [PartOf("objectType")]
    public string? ObjectTypeName { get; init; }

    [CatalogField("collectionGroup", "Collection group", FieldType.Text), Filterable, Sortable]
    public string? CollectionGroup { get; init; }

    /// <summary>Grouping key: lower case, whitespace collapsed.</summary>
    [PartOf("collectionGroup")]
    public string? CollectionGroupKey { get; init; }

    [CatalogField("placeOfProduction", "Place of production", FieldType.Text), Filterable, Sortable]
    public string? PlaceText { get; init; }

    [CatalogField("placeRegion", "Place of production (region)", FieldType.Reference), Filterable, Sortable]
    public Guid? PlaceRegionId { get; init; }

    [PartOf("placeRegion")]
    public string? PlaceRegionName { get; init; }

    [CatalogField("dynasty", "Dynasty or period", FieldType.Text), Filterable, Sortable]
    public string? Dynasty { get; init; }

    /// <summary>Grouping key: lower case, whitespace collapsed.</summary>
    [PartOf("dynasty")]
    public string? DynastyKey { get; init; }

    /// <summary>Century of the creation date (17 = 1601-1700; negative for BCE).</summary>
    [CatalogField("century", "Century", FieldType.Number), Filterable, Sortable]
    public int? Century { get; init; }

    [CatalogField("holder", "Holder", FieldType.Text), Filterable, Sortable]
    public string? Holder { get; init; }

    [CatalogField("storageLocation", "Storage location", FieldType.Text), Filterable, Sortable]
    public string? StorageLocation { get; init; }

    [CatalogField("acquisitionMethod", "Acquisition method", FieldType.Text), Filterable, Sortable]
    public string? AcquisitionMethod { get; init; }

    [CatalogField("acquisitionDate", "Acquisition date", FieldType.Date), Filterable, Sortable]
    public DateOnly? AcquisitionDate { get; init; }

    [CatalogField("acquisitionSource", "Acquisition source", FieldType.Text), Filterable, Sortable]
    public string? AcquisitionSource { get; init; }

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
