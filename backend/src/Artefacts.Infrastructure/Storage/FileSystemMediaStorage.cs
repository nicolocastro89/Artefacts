using Artefacts.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Artefacts.Infrastructure.Storage;

/// <summary>Stores originals on the local file system (tests and simple fallback).</summary>
public sealed class FileSystemMediaStorage(IOptions<StorageOptions> options) : IMediaStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.FileSystem.Root);

    public async Task PutAsync(string key, Stream content, long length, string contentType, CancellationToken ct)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Invalid storage key.", nameof(key));
        return full;
    }
}
