using System.Text;
using Artefacts.Infrastructure.Imaging;
using Artefacts.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace Artefacts.Api.IntegrationTests;

/// <summary>Pure unit tests (no containers).</summary>
public class ImgproxyUrlBuilderTests
{
    [Fact]
    public void Signature_matches_imgproxy_documentation_vector()
    {
        // From https://docs.imgproxy.net/usage/signing_url (key "secret", salt "hello").
        const string path = "/rs:fill:300:400:0/g:sm/aHR0cDovL2V4YW1w/bGUuY29tL2ltYWdl/cy9jdXJpb3NpdHku/anBn.png";

        var signature = ImgproxyUrlBuilder.Sign(Encoding.ASCII.GetBytes("secret"), Encoding.ASCII.GetBytes("hello"), path);

        Assert.Equal("oKfUtW34Dvo2BGQehJFR4Nr0_rIjOtdtzJ3QFsUcXH8", signature);
    }

    [Fact]
    public void Builds_signed_urls_for_every_preset_from_s3_source()
    {
        var builder = new ImgproxyUrlBuilder(
            Options.Create(new ImgproxyOptions { PublicBaseUrl = "http://localhost:8080/", Key = "736563726574", Salt = "68656C6C6F" }),
            Options.Create(new StorageOptions { Provider = StorageProvider.S3, S3 = new S3StorageOptions { Bucket = "media" } }));

        var urls = builder.Build("originals/abc");

        foreach (var (preset, url) in new[] { ("thumb", urls.Thumb), ("web", urls.Web), ("print", urls.Print) })
        {
            var expectedPath = $"/{preset}/plain/s3://media/originals/abc@jpg";
            Assert.EndsWith(expectedPath, url);
            var signature = url["http://localhost:8080/".Length..^expectedPath.Length];
            Assert.Equal(ImgproxyUrlBuilder.Sign(Encoding.ASCII.GetBytes("secret"), Encoding.ASCII.GetBytes("hello"), expectedPath), signature);
        }
    }
}
