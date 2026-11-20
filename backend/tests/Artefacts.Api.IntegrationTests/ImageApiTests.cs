using System.Net;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Media;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ImageApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private string StoredPath(Guid mediaAssetId) =>
        Path.Combine(factory.MediaRoot, "originals", mediaAssetId.ToString("N"));

    [Fact]
    public async Task Valid_tiff_is_stored_unchanged()
    {
        var artefact = await _client.CreateArtefactAsync();
        var tiff = TestData.Tiff(600 * 1024);

        var detail = await _client.UploadAsync(artefact.Id, ("recto.tif", tiff));

        var image = Assert.Single(detail.Images);
        Assert.Equal(ImageFormat.Tiff, image.Format);
        Assert.Equal("recto.tif", image.OriginalFileName);
        Assert.Equal(tiff.Length, image.SizeBytes);
        Assert.Equal(tiff, await File.ReadAllBytesAsync(StoredPath(image.MediaAssetId)));
    }

    [Fact]
    public async Task Disguised_file_is_rejected_and_nothing_is_stored()
    {
        var artefact = await _client.CreateArtefactAsync();
        var before = Directory.Exists(factory.MediaRoot) ? Directory.GetFiles(factory.MediaRoot, "*", SearchOption.AllDirectories).Length : 0;

        var response = await _client.PostAsync($"/api/artefacts/{artefact.Id}/images", TestData.Multipart(("photo.jpg", TestData.Pdf())));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("file", (await response.ValidationErrorsAsync()).Keys);
        var read = await (await _client.GetAsync($"/api/artefacts/{artefact.Id}")).ReadAsync<ArtefactDetail>();
        Assert.Empty(read.Images);
        // Tests share the media root, so compare against a fresh count taken around this request only.
        var after = Directory.Exists(factory.MediaRoot) ? Directory.GetFiles(factory.MediaRoot, "*", SearchOption.AllDirectories).Length : 0;
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Oversized_file_is_rejected()
    {
        var artefact = await _client.CreateArtefactAsync();

        var response = await _client.PostAsync($"/api/artefacts/{artefact.Id}/images",
            TestData.Multipart(("big.jpg", TestData.Jpeg((int)ApiFactory.MaxUploadBytes + 1))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var read = await (await _client.GetAsync($"/api/artefacts/{artefact.Id}")).ReadAsync<ArtefactDetail>();
        Assert.Empty(read.Images);
    }

    [Fact]
    public async Task Upload_to_unknown_artefact_is_not_found()
    {
        var response = await _client.PostAsync($"/api/artefacts/{Guid.NewGuid()}/images", TestData.Multipart(("a.jpg", TestData.Jpeg())));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task First_upload_becomes_primary_and_urls_are_signed()
    {
        var artefact = await _client.CreateArtefactAsync();

        var detail = await _client.UploadAsync(artefact.Id, ("a.jpg", TestData.Jpeg()), ("b.jpg", TestData.Jpeg()));

        Assert.Equal(2, detail.Images.Count);
        Assert.Equal(detail.Images[0].MediaAssetId, detail.PrimaryImage!.MediaAssetId);
        Assert.StartsWith("http://imgproxy.test/", detail.PrimaryImage.Urls.Thumb);
        Assert.Contains("/thumb/plain/", detail.PrimaryImage.Urls.Thumb);
        Assert.Contains("/web/plain/", detail.PrimaryImage.Urls.Web);
        Assert.Contains("/print/plain/", detail.PrimaryImage.Urls.Print);
    }

    [Fact]
    public async Task Changing_primary_image()
    {
        var artefact = await _client.CreateArtefactAsync();
        var uploaded = await _client.UploadAsync(artefact.Id, ("a.jpg", TestData.Jpeg()), ("b.jpg", TestData.Jpeg()));
        var second = uploaded.Images[1].MediaAssetId;

        var response = await _client.PutAsync($"/api/artefacts/{artefact.Id}/images/{second}/primary", null);

        var detail = await response.ReadAsync<ArtefactDetail>();
        Assert.Equal(second, detail.PrimaryImage!.MediaAssetId);
        Assert.Single(detail.Images, i => i.IsPrimary);
    }

    [Fact]
    public async Task Deleting_primary_promotes_earliest_remaining_and_removes_file()
    {
        var artefact = await _client.CreateArtefactAsync();
        var uploaded = await _client.UploadAsync(artefact.Id,
            ("a.jpg", TestData.Jpeg()), ("b.jpg", TestData.Jpeg()), ("c.jpg", TestData.Jpeg()));
        var (first, third) = (uploaded.Images[0].MediaAssetId, uploaded.Images[2].MediaAssetId);
        await _client.PutAsync($"/api/artefacts/{artefact.Id}/images/{third}/primary", null);

        var response = await _client.DeleteAsync($"/api/artefacts/{artefact.Id}/images/{third}");

        var detail = await response.ReadAsync<ArtefactDetail>();
        Assert.Equal(first, detail.PrimaryImage!.MediaAssetId);
        Assert.Equal(2, detail.Images.Count);
        Assert.False(File.Exists(StoredPath(third)));
    }

    [Fact]
    public async Task Caption_can_be_edited()
    {
        var artefact = await _client.CreateArtefactAsync();
        var uploaded = await _client.UploadAsync(artefact.Id, ("a.jpg", TestData.Jpeg()));
        var id = uploaded.Images[0].MediaAssetId;

        var response = await _client.PatchJsonAsync($"/api/artefacts/{artefact.Id}/images/{id}",
            new SetCaptionRequest("Recto, after 2021 conservation"));

        var detail = await response.ReadAsync<ArtefactDetail>();
        Assert.Equal("Recto, after 2021 conservation", detail.Images[0].Caption);
    }
}
