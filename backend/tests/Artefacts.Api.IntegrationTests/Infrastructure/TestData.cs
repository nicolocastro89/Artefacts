using System.Net;
using System.Net.Http.Json;
using Artefacts.Application.Contracts;

namespace Artefacts.Api.IntegrationTests.Infrastructure;

internal static class TestData
{
    public static async Task<ArtefactDetail> CreateArtefactAsync(this HttpClient client, ArtefactWriteRequest request)
    {
        var response = await client.PostJsonAsync("/api/artefacts", request);
        if (response.StatusCode != HttpStatusCode.Created)
            throw new InvalidOperationException($"Create failed: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadAsync<ArtefactDetail>();
    }

    public static Task<ArtefactDetail> CreateArtefactAsync(this HttpClient client, string? title = null) =>
        client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), title ?? "Untitled"));

    /// <summary>A names map with an English name only.</summary>
    public static Dictionary<string, string?> En(string name) => new() { ["en"] = name };

    public static async Task<RegionDto> AddRegionAsync(this HttpClient client, IReadOnlyDictionary<string, string?> names, Guid? parentId = null)
    {
        var response = await client.PostJsonAsync("/api/regions", new AddRegionRequest(names, parentId));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<RegionDto>();
    }

    public static async Task<ObjectTypeDto> AddObjectTypeAsync(this HttpClient client, IReadOnlyDictionary<string, string?> names)
    {
        var response = await client.PostJsonAsync("/api/object-types", new AddObjectTypeRequest(names));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ObjectTypeDto>();
    }

    public static async Task<RegionDto> AddRegionAsync(this HttpClient client, string name, Guid? parentId = null)
    {
        var response = await client.PostJsonAsync("/api/regions", new AddRegionRequest(En(name), parentId));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<RegionDto>();
    }

    public static async Task<ObjectTypeDto> AddObjectTypeAsync(this HttpClient client, string name)
    {
        var response = await client.PostJsonAsync("/api/object-types", new AddObjectTypeRequest(En(name)));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ObjectTypeDto>();
    }

    /// <summary>Adds an entry to a translated list ("dynasties", "collection-groups") with names per language.</summary>
    public static async Task<ReferenceEntryDto> AddListEntryAsync(this HttpClient client, string list, IReadOnlyDictionary<string, string?> names)
    {
        var response = await client.PostJsonAsync($"/api/lists/{list}", new ReferenceEntryNamesRequest(names));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ReferenceEntryDto>();
    }

    /// <summary>Adds an entry with an Italian name to a translated list, or with its single name to "storage-locations".</summary>
    public static async Task<ReferenceEntryDto> AddListEntryAsync(this HttpClient client, string list, string name)
    {
        var request = list == "storage-locations"
            ? new ReferenceEntryNamesRequest(Name: name)
            : new ReferenceEntryNamesRequest(new Dictionary<string, string?> { ["it"] = name });
        var response = await client.PostJsonAsync($"/api/lists/{list}", request);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ReferenceEntryDto>();
    }

    public static async Task<PartyDto> AddPartyAsync(this HttpClient client, string name, Domain.Parties.PartyKind kind = Domain.Parties.PartyKind.Organisation)
    {
        var response = await client.PostJsonAsync("/api/parties", new PartyRequest(name, kind));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<PartyDto>();
    }

    public static async Task<ArtefactDetail> UploadAsync(this HttpClient client, Guid artefactId, params (string Name, byte[] Bytes)[] files)
    {
        var response = await client.PostAsync($"/api/artefacts/{artefactId}/images", Multipart(files));
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Upload failed: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadAsync<ArtefactDetail>();
    }

    public static MultipartFormDataContent Multipart(params (string Name, byte[] Bytes)[] files)
    {
        var content = new MultipartFormDataContent();
        foreach (var (name, bytes) in files)
            content.Add(new ByteArrayContent(bytes), "files", name);
        return content;
    }

    public static byte[] Jpeg(int size = 2048) => WithHeader([0xFF, 0xD8, 0xFF, 0xE0], size);
    public static byte[] Tiff(int size = 4096) => WithHeader([0x49, 0x49, 0x2A, 0x00], size);
    public static byte[] Pdf(int size = 2048) => WithHeader("%PDF-1.7\n"u8.ToArray(), size);

    private static byte[] WithHeader(byte[] header, int size)
    {
        var bytes = new byte[Math.Max(size, header.Length)];
        Random.Shared.NextBytes(bytes);
        header.CopyTo(bytes, 0);
        return bytes;
    }
}
