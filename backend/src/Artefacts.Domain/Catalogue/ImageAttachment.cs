using Artefacts.Domain.Common;

namespace Artefacts.Domain.Catalogue;

/// <summary>An image (a <see cref="Media.MediaAsset"/>) attached to an artefact, with its caption and primary flag.</summary>
public sealed class ImageAttachment
{
    public const int MaxCaptionLength = 1000;

    public MediaAssetId MediaAssetId { get; private set; }
    public string? Caption { get; private set; }
    public bool IsPrimary { get; internal set; }
    public DateTimeOffset AttachedAt { get; private set; }

    // For EF Core.
    private ImageAttachment()
    {
    }

    internal ImageAttachment(MediaAssetId mediaAssetId, DateTimeOffset attachedAt)
    {
        MediaAssetId = mediaAssetId;
        AttachedAt = attachedAt;
    }

    internal void SetCaption(string? caption) => Caption = Guard.Optional(caption, "caption", MaxCaptionLength);
}
