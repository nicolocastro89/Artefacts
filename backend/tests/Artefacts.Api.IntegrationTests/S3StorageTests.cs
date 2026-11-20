using System.Text;
using Artefacts.Infrastructure.Storage;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Configurations;
using Microsoft.Extensions.Options;

namespace Artefacts.Api.IntegrationTests;

/// <summary>Runs S3MediaStorage against a real SeaweedFS container (the same S3 store used in docker compose).</summary>
public sealed class S3StorageTests : IAsyncLifetime
{
    private const string AccessKey = "test-access";
    private const string SecretKey = "test-secret";

    private static readonly string S3Config =
        $$"""{"identities":[{"name":"test","credentials":[{"accessKey":"{{AccessKey}}","secretKey":"{{SecretKey}}"}],"actions":["Admin","Read","Write","List","Tagging"]}]}""";

    private readonly IContainer _seaweed = new ContainerBuilder("chrislusf/seaweedfs:4.54")
        .WithCommand("server", "-dir=/data", "-ip.bind=0.0.0.0", "-s3", "-s3.port=8333", "-s3.config=/etc/s3.json")
        .WithResourceMapping(Encoding.UTF8.GetBytes(S3Config), FilePath.Of("/etc/s3.json"))
        .WithPortBinding(8333, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8333).ForPath("/healthz")))
        .Build();

    private S3MediaStorage _storage = default!;

    public async ValueTask InitializeAsync()
    {
        await _seaweed.StartAsync();
        _storage = new S3MediaStorage(Options.Create(new StorageOptions
        {
            Provider = StorageProvider.S3,
            S3 = new S3StorageOptions
            {
                ServiceUrl = $"http://{_seaweed.Hostname}:{_seaweed.GetMappedPublicPort(8333)}",
                Bucket = "test-bucket",
                AccessKey = AccessKey,
                SecretKey = SecretKey,
            },
        }));
        await _storage.EnsureBucketAsync(CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        _storage.Dispose();
        await _seaweed.DisposeAsync();
    }

    [Fact]
    public async Task Put_get_and_delete_round_trip()
    {
        var ct = TestContext.Current.CancellationToken;
        var bytes = new byte[256 * 1024];
        Random.Shared.NextBytes(bytes);

        await _storage.PutAsync("originals/abc", new MemoryStream(bytes), bytes.Length, "image/jpeg", ct);

        await using (var read = await _storage.OpenReadAsync("originals/abc", ct))
        {
            Assert.NotNull(read);
            var copy = new MemoryStream();
            await read.CopyToAsync(copy, ct);
            Assert.Equal(bytes, copy.ToArray());
        }

        await _storage.DeleteAsync("originals/abc", ct);
        Assert.Null(await _storage.OpenReadAsync("originals/abc", ct));
    }
}
