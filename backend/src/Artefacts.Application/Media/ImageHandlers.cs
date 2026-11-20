using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Common;
using Artefacts.Domain.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Artefacts.Application.Media;

public sealed class MediaOptions
{
    public const string Section = "Media";

    /// <summary>Maximum accepted size of one uploaded file, in bytes. Default 200 MB.</summary>
    public long MaxUploadBytes { get; set; } = 200L * 1024 * 1024;
}

/// <summary>A file being uploaded. <see cref="OpenRead"/> may be called more than once.</summary>
public sealed record UploadFile(string FileName, long Length, Func<Stream> OpenRead);

public sealed class ImageHandler(
    IArtefactRepository artefacts,
    IMediaAssetRepository mediaAssets,
    IMediaStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IOptions<MediaOptions> options,
    Artefacts.ArtefactDetailQueryHandler detail,
    ILogger<ImageHandler> logger)
{
    public async Task<ArtefactDetail> UploadAsync(Guid artefactId, IReadOnlyList<UploadFile> files, CancellationToken ct)
    {
        var artefact = await artefacts.GetAsync(new ArtefactId(artefactId), ct)
                       ?? throw new NotFoundException("Artefact", artefactId);
        if (files.Count == 0) throw new DomainValidationException("files", "Select at least one file.");

        // 1. Validate every file before storing anything.
        var formats = new ImageFormat[files.Count];
        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < files.Count; i++)
        {
            var key = files.Count == 1 ? "file" : $"files[{i}]";
            var file = files[i];
            if (file.Length <= 0) { errors[key] = [$"\"{file.FileName}\" is empty."]; continue; }
            if (file.Length > options.Value.MaxUploadBytes)
            {
                errors[key] = [$"\"{file.FileName}\" is larger than the maximum of {options.Value.MaxUploadBytes / (1024 * 1024)} MB."];
                continue;
            }

            var format = await DetectAsync(file, ct);
            if (format is null)
                errors[key] = [$"\"{file.FileName}\" is not a supported image. Accepted formats: JPEG, PNG, TIFF, WebP."];
            else
                formats[i] = format.Value;
        }

        if (errors.Count > 0) throw new DomainValidationException(errors);

        // 2. Store originals, then commit; undo stored objects if the commit fails.
        var stored = new List<string>();
        try
        {
            var now = clock.GetUtcNow();
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var asset = MediaAsset.CreateImage(file.FileName, formats[i], file.Length, now);
                await using (var content = file.OpenRead())
                {
                    await storage.PutAsync(asset.StorageKey, content, file.Length, ContentType(formats[i]), ct);
                }

                stored.Add(asset.StorageKey);
                mediaAssets.Add(asset);
                // Distinct timestamps keep "attached order" deterministic within one upload.
                artefact.AttachImage(asset.Id, now.AddTicks(i));
            }

            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            foreach (var key in stored)
            {
                try { await storage.DeleteAsync(key, CancellationToken.None); }
                catch (Exception e) { logger.LogWarning(e, "Could not remove orphaned media object {Key}", key); }
            }

            throw;
        }

        return await detail.GetAsync(artefactId, ct);
    }

    public async Task<ArtefactDetail> MakePrimaryAsync(Guid artefactId, Guid mediaAssetId, CancellationToken ct)
    {
        var artefact = await LoadAsync(artefactId, ct);
        artefact.MakePrimary(new MediaAssetId(mediaAssetId));
        await unitOfWork.SaveChangesAsync(ct);
        return await detail.GetAsync(artefactId, ct);
    }

    public async Task<ArtefactDetail> SetCaptionAsync(Guid artefactId, Guid mediaAssetId, SetCaptionRequest request, CancellationToken ct)
    {
        var artefact = await LoadAsync(artefactId, ct);
        artefact.SetImageCaption(new MediaAssetId(mediaAssetId), request.Caption);
        await unitOfWork.SaveChangesAsync(ct);
        return await detail.GetAsync(artefactId, ct);
    }

    public async Task<ArtefactDetail> DeleteAsync(Guid artefactId, Guid mediaAssetId, CancellationToken ct)
    {
        var artefact = await LoadAsync(artefactId, ct);
        var id = new MediaAssetId(mediaAssetId);
        artefact.RemoveImage(id);
        var asset = await mediaAssets.GetAsync(id, ct);
        if (asset is not null) mediaAssets.Remove(asset);
        await unitOfWork.SaveChangesAsync(ct);

        if (asset is not null)
        {
            try { await storage.DeleteAsync(asset.StorageKey, ct); }
            catch (Exception e) { logger.LogWarning(e, "Could not delete media object {Key}", asset.StorageKey); }
        }

        return await detail.GetAsync(artefactId, ct);
    }

    private async Task<Domain.Catalogue.Artefact> LoadAsync(Guid artefactId, CancellationToken ct) =>
        await artefacts.GetAsync(new ArtefactId(artefactId), ct) ?? throw new NotFoundException("Artefact", artefactId);

    private static async Task<ImageFormat?> DetectAsync(UploadFile file, CancellationToken ct)
    {
        await using var stream = file.OpenRead();
        var header = new byte[ImageFormatSniffer.HeaderLength];
        var read = 0;
        while (read < header.Length)
        {
            var n = await stream.ReadAsync(header.AsMemory(read), ct);
            if (n == 0) break;
            read += n;
        }

        return ImageFormatSniffer.Detect(header.AsSpan(0, read));
    }

    public static string ContentType(ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => "image/jpeg",
        ImageFormat.Png => "image/png",
        ImageFormat.Tiff => "image/tiff",
        ImageFormat.WebP => "image/webp",
        _ => "application/octet-stream",
    };
}
