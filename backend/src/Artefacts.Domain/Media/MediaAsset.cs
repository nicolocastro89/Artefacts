using Artefacts.Domain.Common;

namespace Artefacts.Domain.Media;

public enum ImageFormat
{
    Jpeg = 0,
    Png = 1,
    Tiff = 2,
    WebP = 3,
}

/// <summary>A stored original media file (archival copy, never modified). Sized renditions are produced on demand.</summary>
public sealed class MediaAsset
{
    public const int MaxFileNameLength = 255;

    public MediaAssetId Id { get; private set; }
    public string StorageKey { get; private set; } = default!;
    public string OriginalFileName { get; private set; } = default!;
    public ImageFormat Format { get; private set; }
    public long SizeBytes { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }

    // For EF Core.
    private MediaAsset()
    {
    }

    public static MediaAsset CreateImage(string? originalFileName, ImageFormat format, long sizeBytes, DateTimeOffset uploadedAt)
    {
        if (sizeBytes <= 0) throw new DomainValidationException("file", "The file is empty.");
        var name = FileNames.Leaf(originalFileName).Trim();
        if (name.Length == 0) name = "image";
        if (name.Length > MaxFileNameLength) name = name[^MaxFileNameLength..];

        var id = MediaAssetId.New();
        return new MediaAsset
        {
            Id = id,
            StorageKey = StorageKeyFor(id),
            OriginalFileName = name,
            Format = format,
            SizeBytes = sizeBytes,
            UploadedAt = uploadedAt,
        };
    }

    public static string StorageKeyFor(MediaAssetId id) => $"originals/{id.Value:N}";
}

/// <summary>Detects image formats from file content (magic bytes), never from names or declared content types.</summary>
public static class ImageFormatSniffer
{
    /// <summary>Number of leading bytes needed for detection.</summary>
    public const int HeaderLength = 12;

    public static ImageFormat? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ImageFormat.Jpeg;
        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ImageFormat.Png;
        if (header.Length >= 4 &&
            ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) ||
             (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A)))
            return ImageFormat.Tiff;
        if (header.Length >= 12 &&
            header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
            return ImageFormat.WebP;
        return null;
    }
}
