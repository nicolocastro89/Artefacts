using System.Text;
using Artefacts.Domain.Common;
using Artefacts.Domain.Media;

namespace Artefacts.Domain.Tests;

public class MediaTests
{
    public static TheoryData<byte[], ImageFormat?> Headers => new()
    {
        { [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0], ImageFormat.Jpeg },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0], ImageFormat.Png },
        { [0x49, 0x49, 0x2A, 0x00, 0, 0, 0, 0, 0, 0, 0, 0], ImageFormat.Tiff },
        { [0x4D, 0x4D, 0x00, 0x2A, 0, 0, 0, 0, 0, 0, 0, 0], ImageFormat.Tiff },
        { [.. "RIFF"u8, 0x10, 0, 0, 0, .. "WEBP"u8], ImageFormat.WebP },
        { Encoding.ASCII.GetBytes("%PDF-1.7\n%..."), null },
        { [0xFF, 0xD8], null },
        { [], null },
    };

    [Theory]
    [MemberData(nameof(Headers))]
    public void Detects_format_from_content(byte[] header, ImageFormat? expected)
    {
        Assert.Equal(expected, ImageFormatSniffer.Detect(header));
    }

    [Fact]
    public void Creates_media_asset_with_storage_key_and_safe_file_name()
    {
        var at = DateTimeOffset.UtcNow;
        var asset = MediaAsset.CreateImage(@"C:\photos\recto.tif", ImageFormat.Tiff, 60_000_000, at);

        Assert.Equal("recto.tif", asset.OriginalFileName);
        Assert.Equal($"originals/{asset.Id.Value:N}", asset.StorageKey);
        Assert.Equal(ImageFormat.Tiff, asset.Format);
        Assert.Equal(60_000_000, asset.SizeBytes);
        Assert.Equal(at, asset.UploadedAt);
    }

    [Theory]
    [InlineData(@"C:\photos\recto.tif", "recto.tif")]
    [InlineData("/home/curator/scans/recto.tif", "recto.tif")]
    [InlineData(@"..\..\recto.tif", "recto.tif")]
    [InlineData("recto.tif", "recto.tif")]
    public void File_name_is_the_last_path_segment_whichever_separator_the_client_used(string sent, string expected) =>
        Assert.Equal(expected, MediaAsset.CreateImage(sent, ImageFormat.Tiff, 1, DateTimeOffset.UtcNow).OriginalFileName);

    [Fact]
    public void Rejects_empty_file()
    {
        Assert.Throws<DomainValidationException>(() => MediaAsset.CreateImage("x.jpg", ImageFormat.Jpeg, 0, DateTimeOffset.UtcNow));
    }
}
