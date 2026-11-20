using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Artefacts.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Artefacts.Infrastructure.Storage;

/// <summary>Stores originals in any S3-compatible store (SeaweedFS locally, AWS S3 in the cloud).</summary>
public sealed class S3MediaStorage : IMediaStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public S3MediaStorage(IOptions<StorageOptions> options)
    {
        var o = options.Value.S3;
        _bucket = o.Bucket;
        var config = new AmazonS3Config
        {
            ServiceURL = o.ServiceUrl,
            ForcePathStyle = o.ForcePathStyle,
            AuthenticationRegion = o.Region,
            // S3-compatible stores do not all support the newer default request checksums.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        _client = new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), config);
    }

    public async Task PutAsync(string key, Stream content, long length, string contentType, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            UseChunkEncoding = false,
        };
        request.Headers.ContentLength = length;
        await _client.PutObjectAsync(request, ct);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucket, key, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string key, CancellationToken ct) =>
        _client.DeleteObjectAsync(_bucket, key, ct);

    public Task CopyAsync(string sourceKey, string destinationKey, CancellationToken ct) =>
        _client.CopyObjectAsync(_bucket, sourceKey, _bucket, destinationKey, ct);

    public async Task DeletePrefixAsync(string prefix, CancellationToken ct)
    {
        var request = new ListObjectsV2Request { BucketName = _bucket, Prefix = prefix };
        ListObjectsV2Response response;
        do
        {
            response = await _client.ListObjectsV2Async(request, ct);
            var keys = (response.S3Objects ?? []).Select(o => new KeyVersion { Key = o.Key }).ToList();
            if (keys.Count > 0)
                await _client.DeleteObjectsAsync(new DeleteObjectsRequest { BucketName = _bucket, Objects = keys }, ct);
            request.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated == true);
    }

    /// <summary>Creates the bucket if it does not exist (used by tests and first-run setup).</summary>
    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        try
        {
            await _client.PutBucketAsync(_bucket, ct);
        }
        catch (AmazonS3Exception e) when (e.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
        }
    }

    public void Dispose() => _client.Dispose();
}
