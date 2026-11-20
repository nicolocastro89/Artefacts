using Artefacts.Domain.Common;
using Artefacts.Domain.Dating;

namespace Artefacts.Domain.Catalogue;

/// <summary>
/// The central catalogue record for one object in the collection (aggregate root).
/// Owns its provenance entries and its image attachments, and enforces the primary-image rules.
/// Other aggregates (regions, media assets) are referenced by id only.
/// </summary>
public sealed class Artefact
{
    public const int MaxInventoryNumberLength = 64;
    public const int MaxTitleLength = 500;
    public const int MaxShortTextLength = 500;
    public const int MaxDescriptionLength = 10_000;

    private readonly List<ProvenanceEntry> _provenance = [];
    private readonly List<ImageAttachment> _images = [];

    public ArtefactId Id { get; private set; }
    public string InventoryNumber { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string? Maker { get; private set; }
    public HistoricalDate? CreationDate { get; private set; }
    public string? Medium { get; private set; }
    public string? Dimensions { get; private set; }
    public string? Description { get; private set; }
    public Acquisition? Acquisition { get; private set; }

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

    public void Describe(string? maker, HistoricalDate? creationDate, string? medium, string? dimensions, string? description)
    {
        Maker = Guard.Optional(maker, "maker", MaxShortTextLength);
        Medium = Guard.Optional(medium, "medium", MaxShortTextLength);
        Dimensions = Guard.Optional(dimensions, "dimensions", MaxShortTextLength);
        Description = Guard.Optional(description, "description", MaxDescriptionLength);
        CreationDate = creationDate;
    }

    public void SetAcquisition(Acquisition? acquisition) => Acquisition = acquisition;

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
