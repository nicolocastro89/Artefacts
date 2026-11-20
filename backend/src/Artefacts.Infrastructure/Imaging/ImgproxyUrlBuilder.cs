using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Microsoft.Extensions.Options;

namespace Artefacts.Infrastructure.Imaging;

public sealed class ImgproxyOptions : IValidatableObject
{
    public const string Section = "Imgproxy";

    /// <summary>Base URL the browser uses to reach imgproxy, e.g. http://localhost:8080.</summary>
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>Hex-encoded signing key (same value as IMGPROXY_KEY).</summary>
    public string Key { get; set; } = "";

    /// <summary>Hex-encoded signing salt (same value as IMGPROXY_SALT).</summary>
    public string Salt { get; set; } = "";

    /// <summary>
    /// Prefix that turns a storage key into an imgproxy source URL, e.g. <c>s3://artefacts-media/</c>.
    /// When empty, it is derived from the S3 bucket.
    /// </summary>
    public string SourcePrefix { get; set; } = "";

    /// <summary>Extension requested for every rendition. imgproxy upgrades it to WebP for browsers that accept it.</summary>
    public string Extension { get; set; } = "jpg";

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(PublicBaseUrl))
            yield return new("Configuration value 'Imgproxy:PublicBaseUrl' is required.", [nameof(PublicBaseUrl)]);
        if (!IsHex(Key)) yield return new("Configuration value 'Imgproxy:Key' must be a non-empty hex string.", [nameof(Key)]);
        if (!IsHex(Salt)) yield return new("Configuration value 'Imgproxy:Salt' must be a non-empty hex string.", [nameof(Salt)]);
    }

    private static bool IsHex(string s) => s.Length > 0 && s.Length % 2 == 0 && s.All(Uri.IsHexDigit);
}

/// <summary>Builds signed imgproxy URLs for the thumb/web/print presets. URLs are computed, never stored.</summary>
public sealed class ImgproxyUrlBuilder : IImageUrlBuilder
{
    private readonly byte[] _key;
    private readonly byte[] _salt;
    private readonly string _baseUrl;
    private readonly string _sourcePrefix;
    private readonly string _extension;

    public ImgproxyUrlBuilder(IOptions<ImgproxyOptions> options, IOptions<Storage.StorageOptions> storage)
    {
        var o = options.Value;
        _key = Convert.FromHexString(o.Key);
        _salt = Convert.FromHexString(o.Salt);
        _baseUrl = o.PublicBaseUrl.TrimEnd('/');
        _extension = o.Extension;
        _sourcePrefix = !string.IsNullOrEmpty(o.SourcePrefix)
            ? o.SourcePrefix
            : storage.Value.Provider == Storage.StorageProvider.S3
                ? $"s3://{storage.Value.S3.Bucket}/"
                : "local:///";
    }

    public ImageUrls Build(string storageKey) =>
        new(Url("thumb", storageKey), Url("web", storageKey), Url("print", storageKey));

    private string Url(string preset, string storageKey)
    {
        var source = EscapeSource(_sourcePrefix + storageKey);
        // imgproxy runs with IMGPROXY_ONLY_PRESETS, where the options segment is a bare preset name
        // (`/web/plain/...`); the `preset:web` form is rejected there as an unknown preset.
        var path = $"/{preset}/plain/{source}@{_extension}";
        return $"{_baseUrl}/{Sign(_key, _salt, path)}{path}";
    }

    /// <summary>imgproxy signature: URL-safe Base64 (no padding) of HMAC-SHA256(key, salt + path).</summary>
    public static string Sign(byte[] key, byte[] salt, string path)
    {
        var data = new byte[salt.Length + Encoding.UTF8.GetByteCount(path)];
        salt.CopyTo(data, 0);
        Encoding.UTF8.GetBytes(path, data.AsSpan(salt.Length));
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // Plain source URLs must escape %, ? and @ (imgproxy docs).
    private static string EscapeSource(string s) => s.Replace("%", "%25").Replace("?", "%3F").Replace("@", "%40");
}
