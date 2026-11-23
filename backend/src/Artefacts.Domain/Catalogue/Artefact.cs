using Artefacts.Domain.Common;
using Artefacts.Domain.Dating;
using Artefacts.Domain.Finance;
using Artefacts.Domain.Languages;
using Artefacts.Domain.Measures;

namespace Artefacts.Domain.Catalogue;

/// <summary>
/// The central catalogue record for one object in the collection (aggregate root).
/// Owns its provenance entries and its image attachments, and enforces the primary-image rules.
/// Other aggregates (regions, media assets, list entries, parties) are referenced by id only.
/// </summary>
public sealed class Artefact
{
    public const int MaxInventoryNumberLength = 64;
    public const int MaxTitleLength = 500;
    public const int MaxShortTextLength = 500;
    public const int MaxDescriptionLength = 10_000;
    public const int MaxCatalogueEntryLength = 20_000;

    private readonly List<ProvenanceEntry> _provenance = [];
    private readonly List<Exhibition> _exhibitions = [];
    private readonly List<ArtefactTranslation> _translations = [];
    private readonly List<ImageAttachment> _images = [];

    public ArtefactId Id { get; private set; }
    public string InventoryNumber { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string? Maker { get; private set; }
    public HistoricalDate? CreationDate { get; private set; }
    public string? Medium { get; private set; }
    public string? Dimensions { get; private set; }

    /// <summary>Structured measurements, each in its own unit; independent of the <see cref="Dimensions"/> text.</summary>
    public Measurement? Width { get; private set; }
    public Measurement? Height { get; private set; }
    public Measurement? Length { get; private set; }
    public Measurement? Diameter { get; private set; }

    public ArtefactMeasurements Measurements => new(Width, Height, Length, Diameter);
    public Acquisition? Acquisition { get; private set; }

    /// <summary>Kind of object, from the object type reference list.</summary>
    public ObjectTypeId? ObjectTypeId { get; private set; }

    /// <summary>The group the collection files the artefact under (e.g. "CINA", "LAMPASSI"), from the collection group list.</summary>
    public ReferenceEntryId? CollectionGroupId { get; private set; }

    public PlaceOfProduction? PlaceOfProduction { get; private set; }

    /// <summary>Dynasty or period (e.g. "Ottomana"), from the dynasty list.</summary>
    public ReferenceEntryId? DynastyId { get; private set; }

    /// <summary>The English catalogue entry (long text, line breaks preserved).</summary>

    /// <summary>The party holding the artefact (e.g. a company); none means the collection itself holds it.</summary>
    public PartyId? HolderId { get; private set; }

    /// <summary>Where the artefact is stored (e.g. "MI F3", "cella 16"), from the storage location list.</summary>
    public ReferenceEntryId? StorageLocationId { get; private set; }

    /// <summary>Current valuation. Financially sensitive.</summary>
    public Money? Valuation { get; private set; }

    /// <summary>Texts per language (the description), one row per language that has any.</summary>
    public IReadOnlyList<ArtefactTranslation> Translations => _translations.OrderBy(t => t.Language, StringComparer.Ordinal).ToList();

    /// <summary>Exhibitions in the order entered.</summary>
    public IReadOnlyList<Exhibition> Exhibitions => _exhibitions.OrderBy(e => e.Position).ToList();

    /// <summary>Provenance in chronological/curatorial order as entered.</summary>
    public IReadOnlyList<ProvenanceEntry> Provenance => _provenance.OrderBy(p => p.Position).ToList();

    /// <summary>Images in the order they were attached.</summary>
    public IReadOnlyList<ImageAttachment> Images => _images.OrderBy(i => i.AttachedAt).ToList();

    public ImageAttachment? PrimaryImage => _images.SingleOrDefault(i => i.IsPrimary);

    // For EF Core.
    private Artefact()
    {
    }

    public static Artefact Create(string? inventoryNumber, string? title)
    {
        var artefact = new Artefact { Id = ArtefactId.New() };
        artefact.SetIdentity(inventoryNumber, title);
        return artefact;
    }

    public void SetIdentity(string? inventoryNumber, string? title)
    {
        var errors = new Dictionary<string, string[]>();
        string? inv = null, t = null;
        try { inv = Guard.Required(inventoryNumber, "inventoryNumber", MaxInventoryNumberLength); }
        catch (DomainValidationException e) { Merge(errors, e); }
        try { t = Guard.Required(title, "title", MaxTitleLength); }
        catch (DomainValidationException e) { Merge(errors, e); }
        if (errors.Count > 0) throw new DomainValidationException(errors);

        InventoryNumber = inv!;
        Title = t!;
    }

    public void Describe(string? maker, HistoricalDate? creationDate, string? medium, string? dimensions)
    {
        Maker = Guard.Optional(maker, "maker", MaxShortTextLength);
        Medium = Guard.Optional(medium, "medium", MaxShortTextLength);
        Dimensions = Guard.Optional(dimensions, "dimensions", MaxShortTextLength);
        CreationDate = creationDate;
    }

    /// <summary>Replaces the four measurements (null clears one). Units are checked by the caller.</summary>
    public void Measure(ArtefactMeasurements measurements)
    {
        Width = measurements.Width;
        Height = measurements.Height;
        Length = measurements.Length;
        Diameter = measurements.Diameter;
    }

    /// <summary>
    /// Replaces the description in every language from a language → text map. Text is kept as entered apart from
    /// trimming; blank entries mean no description in that language. Catalogue entries are left as they are.
    /// </summary>
    public void ReplaceDescriptions(IReadOnlyDictionary<string, string?>? descriptions) =>
        ReplaceTexts(descriptions, "descriptions", MaxDescriptionLength, replaceDescriptions: true);

    /// <summary>The description in the first preferred language that has one.</summary>
    public ResolvedText? DescriptionIn(IReadOnlyList<string> preferences) =>
        Localized.Resolve(_translations.Where(t => t.Description is not null).Select(t => (t.Language, t.Description!)), preferences);

    /// <summary>
    /// Replaces the catalogue entry in every language from a language → text map, as <see cref="ReplaceDescriptions"/>
    /// does for descriptions. Descriptions are left as they are.
    /// </summary>
    public void ReplaceCatalogueEntries(IReadOnlyDictionary<string, string?>? entries) =>
        ReplaceTexts(entries, "catalogueEntries", MaxCatalogueEntryLength, replaceDescriptions: false);

    /// <summary>The catalogue entry in the first preferred language that has one.</summary>
    public ResolvedText? CatalogueEntryIn(IReadOnlyList<string> preferences) =>
        Localized.Resolve(_translations.Where(t => t.CatalogueEntry is not null).Select(t => (t.Language, t.CatalogueEntry!)), preferences);

    /// <summary>Replaces one kind of translated text, keeping the other kind of every language.</summary>
    private void ReplaceTexts(IReadOnlyDictionary<string, string?>? texts, string field, int maxLength, bool replaceDescriptions)
    {
        var clean = new Dictionary<string, string>();
        foreach (var (code, text) in texts ?? new Dictionary<string, string?>())
        {
            var language = code.Trim().ToLowerInvariant();
            var value = Guard.Optional(text, $"{field}.{language}", maxLength);
            if (value is not null) clean.TryAdd(language, value);
        }

        var rows = clean.Keys.Union(_translations.Select(t => t.Language))
            .Select(language =>
            {
                var current = _translations.SingleOrDefault(t => t.Language == language);
                return replaceDescriptions
                    ? ArtefactTranslation.Create(language, clean.GetValueOrDefault(language), current?.CatalogueEntry)
                    : ArtefactTranslation.Create(language, current?.Description, clean.GetValueOrDefault(language));
            })
            .Where(t => t.Description is not null || t.CatalogueEntry is not null)
            .ToList();
        _translations.Clear();
        _translations.AddRange(rows);
    }

    public void SetAcquisition(Acquisition? acquisition) => Acquisition = acquisition;

    /// <summary>Sets what kind of object this is and the group the collection files it under.</summary>
    public void Classify(ObjectTypeId? objectTypeId, ReferenceEntryId? collectionGroupId)
    {
        ObjectTypeId = objectTypeId;
        CollectionGroupId = collectionGroupId;
    }

    /// <summary>Sets where and under which dynasty or period the artefact was made.</summary>
    public void SetOrigin(PlaceOfProduction? placeOfProduction, ReferenceEntryId? dynastyId)
    {
        PlaceOfProduction = placeOfProduction;
        DynastyId = dynastyId;
    }

    /// <summary>Sets who holds the artefact, where it is kept, and its current valuation.</summary>
    public void SetHolding(PartyId? holderId, ReferenceEntryId? storageLocationId, Money? valuation)
    {
        HolderId = holderId;
        StorageLocationId = storageLocationId;
        Valuation = valuation;
    }

    /// <summary>Replaces the exhibitions with <paramref name="entries"/> in order; blank entries are dropped.</summary>
    public void ReplaceExhibitions(IEnumerable<string?> entries)
    {
        var list = entries.ToList();
        var kept = new List<Exhibition>();
        for (var i = 0; i < list.Count; i++)
        {
            var exhibition = Exhibition.Create(list[i], kept.Count, i);
            if (exhibition is not null) kept.Add(exhibition);
        }
        _exhibitions.Clear();
        _exhibitions.AddRange(kept);
    }

    /// <summary>Replaces the provenance with <paramref name="entries"/>, preserving their order.</summary>
    public void ReplaceProvenance(IEnumerable<ProvenanceEntry> entries)
    {
        _provenance.Clear();
        _provenance.AddRange(entries.Select((e, i) => e.AtPosition(i)));
    }

    /// <summary>Attaches an image. The first image of an artefact becomes its primary image.</summary>
    public ImageAttachment AttachImage(MediaAssetId mediaAssetId, DateTimeOffset attachedAt, string? caption = null)
    {
        if (_images.Any(i => i.MediaAssetId == mediaAssetId))
            throw new ConflictException("image_already_attached", "This image is already attached to the artefact.");

        var attachment = new ImageAttachment(mediaAssetId, attachedAt) { IsPrimary = _images.Count == 0 };
        attachment.SetCaption(caption);
        _images.Add(attachment);
        return attachment;
    }

    public void MakePrimary(MediaAssetId mediaAssetId)
    {
        var target = FindImage(mediaAssetId);
        foreach (var image in _images) image.IsPrimary = false;
        target.IsPrimary = true;
    }

    public void SetImageCaption(MediaAssetId mediaAssetId, string? caption) => FindImage(mediaAssetId).SetCaption(caption);

    /// <summary>Removes an image. If it was primary, the earliest remaining attached image becomes primary.</summary>
    public void RemoveImage(MediaAssetId mediaAssetId)
    {
        var target = FindImage(mediaAssetId);
        _images.Remove(target);
        if (target.IsPrimary && _images.Count > 0)
            _images.OrderBy(i => i.AttachedAt).First().IsPrimary = true;
    }

    private ImageAttachment FindImage(MediaAssetId mediaAssetId) =>
        _images.SingleOrDefault(i => i.MediaAssetId == mediaAssetId)
        ?? throw new NotFoundException("Image", mediaAssetId);

    private static void Merge(Dictionary<string, string[]> target, DomainValidationException e)
    {
        foreach (var (k, v) in e.Errors) target[k] = v;
    }
}
