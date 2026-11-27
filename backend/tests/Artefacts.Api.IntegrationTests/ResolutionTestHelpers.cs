using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Application.Imports;

namespace Artefacts.Api.IntegrationTests;

/// <summary>Builds and reads the generic resolutions of the dates and dimensions kinds.</summary>
internal static class ResolutionTestHelpers
{
    private static readonly JsonSerializerOptions Json = ImportJson.Options;

    public static ImportResolution DateRange(string text, int earliest, int latest) =>
        new(text, "range", JsonSerializer.SerializeToElement(new DateRangePayload(earliest, latest), Json));

    public static ImportResolution Measures(string text, MeasurementInput? width = null, MeasurementInput? height = null, MeasurementInput? length = null, MeasurementInput? diameter = null) =>
        new(text, "measure", JsonSerializer.SerializeToElement(new DimensionsPayload(width, height, length, diameter), Json));

    public static ImportResolution KeepText(string text) => new(text, "keepText");

    public static DateRangePayload Range(this ImportResolutionItemDto item) => item.Resolution!.Payload!.Value.Deserialize<DateRangePayload>(Json)!;

    public static DimensionsPayload Measurements(this ImportResolutionItemDto item) => item.Resolution!.Payload!.Value.Deserialize<DimensionsPayload>(Json)!;

    public static async Task<List<ImportResolutionItemDto>> ResolutionsAsync(this HttpClient client, Guid id, string kind) =>
        await (await client.GetAsync($"/api/imports/{id}/resolutions/{kind}")).ReadAsync<List<ImportResolutionItemDto>>();

    public static Task<HttpResponseMessage> PutResolutionsAsync(this HttpClient client, Guid id, string kind, params ImportResolution[] resolutions) =>
        client.PutJsonAsync($"/api/imports/{id}/resolutions/{kind}", new SetImportResolutionsRequest(resolutions));
}
