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

    public Task CopyAsync(string sourceKey, string destinationKey, CancellationToken ct)
    {
        var destination = PathFor(destinationKey);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(PathFor(sourceKey), destination, overwrite: true);
        return Task.CompletedTask;
    }

    public Task DeletePrefixAsync(string prefix, CancellationToken ct)
    {
        // Keys use "/" as a folder separator, so a prefix ending in "/" is a directory.
        var trimmed = prefix.TrimEnd('/');
        if (trimmed.Length == 0) throw new ArgumentException("Refusing to delete every stored object.", nameof(prefix));
        var path = PathFor(trimmed);
        if (prefix.EndsWith('/'))
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        else
        {
            var directory = Path.GetDirectoryName(path)!;
            if (Directory.Exists(directory))
                foreach (var file in Directory.GetFiles(directory, Path.GetFileName(path) + "*")) File.Delete(file);
        }

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
