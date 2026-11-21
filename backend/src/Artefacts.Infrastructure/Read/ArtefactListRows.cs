using Artefacts.Application.Contracts;

namespace Artefacts.Infrastructure.Read;

/// <summary>One row of <c>v_artefact_provenance</c>: a provenance entry of an artefact.</summary>
[CatalogEntity("ProvenanceEntry", "Provenance entry")]
public sealed class ProvenanceRow
{
    [CatalogField("artefact", "Artefact", FieldType.Reference), Filterable]
    public Guid ArtefactId { get; init; }

    [CatalogField("position", "Position", FieldType.Number), Sortable]
    public int Position { get; init; }

    [CatalogField("region", "Region", FieldType.Reference), Filterable]
    public Guid RegionId { get; init; }

    [CatalogField("period", "Period", FieldType.HistoricalDate), Filterable, Sortable]
    public string PeriodDisplay { get; init; } = default!;

    [PartOf("period")]
    public int? PeriodEarliestYear { get; init; }

    [PartOf("period")]
    public int? PeriodLatestYear { get; init; }

    [PartOf("period")]
    public string? PeriodPrecision { get; init; }

    [CatalogField("note", "Note", FieldType.Text), Filterable]
    public string? Note { get; init; }
}

/// <summary>One row of <c>v_artefact_image</c>: an image attached to an artefact, with its media asset.</summary>
[CatalogEntity("ArtefactImage", "Image")]
public sealed class ImageRow
{
    [CatalogField("artefact", "Artefact", FieldType.Reference), Filterable]
    public Guid ArtefactId { get; init; }

    [CatalogField("mediaAsset", "Media asset", FieldType.Reference), Filterable]
    public Guid MediaAssetId { get; init; }

    [CatalogField("image", "Image", FieldType.Image)]
    public string StorageKey { get; init; } = default!;

    [CatalogField("originalFileName", "File name", FieldType.Text), Filterable, Sortable]
    public string OriginalFileName { get; init; } = default!;

    [CatalogField("format", "Format", FieldType.Text), Filterable]
    public string Format { get; init; } = default!;

    [CatalogField("sizeBytes", "Size", FieldType.Number), Filterable, Sortable]
    public long SizeBytes { get; init; }

    [CatalogField("uploadedAt", "Uploaded", FieldType.Date), Filterable, Sortable]
    public DateTimeOffset UploadedAt { get; init; }

    [CatalogField("caption", "Caption", FieldType.Text), Filterable]
    public string? Caption { get; init; }

    [CatalogField("isPrimary", "Primary image", FieldType.Boolean), Filterable]
    public bool IsPrimary { get; init; }

    [CatalogField("attachedAt", "Attached", FieldType.Date), Sortable]
    public DateTimeOffset AttachedAt { get; init; }
}

/// <summary>One row of <c>v_artefact_exhibition</c>: an exhibition an artefact was shown in.</summary>
[CatalogEntity("Exhibition", "Exhibition")]
public sealed class ExhibitionRow
{
    [CatalogField("artefact", "Artefact", FieldType.Reference), Filterable]
    public Guid ArtefactId { get; init; }

    [CatalogField("position", "Position", FieldType.Number), Sortable]
    public int Position { get; init; }

    [CatalogField("text", "Exhibition", FieldType.Text), Filterable]
    public string Text { get; init; } = default!;
}

/// <summary>One row of <c>v_artefact_description</c>: an artefact's description in one language.</summary>
[CatalogEntity("Description", "Description")]
public sealed class DescriptionRow
{
    [CatalogField("artefact", "Artefact", FieldType.Reference), Filterable]
    public Guid ArtefactId { get; init; }

    [CatalogField("language", "Language", FieldType.Text), Filterable]
    public string Language { get; init; } = default!;

    [CatalogField("description", "Description", FieldType.Text), Filterable]
    public string Description { get; init; } = default!;
}

/// <summary>One row of <c>v_artefact_catalogue_entry</c>: an artefact's catalogue entry in one language.</summary>
[CatalogEntity("CatalogueEntry", "Catalogue entry")]
public sealed class CatalogueEntryRow
{
    [CatalogField("artefact", "Artefact", FieldType.Reference), Filterable]
    public Guid ArtefactId { get; init; }

    [CatalogField("language", "Language", FieldType.Text), Filterable]
    public string Language { get; init; } = default!;

    [CatalogField("catalogueEntry", "Catalogue entry", FieldType.Text), Filterable]
    public string CatalogueEntry { get; init; } = default!;
}
