using System.Text.Json;
using Artefacts.Application.Contracts;

namespace Artefacts.Api.IntegrationTests;

/// <summary>An artefact in the list, as the <c>artefact-list</c> system query returns it.</summary>
public sealed record ListedArtefact(
    Guid Id,
    string InventoryNumber,
    string Title,
    string? Maker,
    string? Medium,
    ImageUrls? PrimaryImage,
    int ImageCount,
    string? ObjectType,
    string? CollectionGroup,
    string? PlaceOfProduction,
    string? Dynasty,
    string? GroupKey,
    string? GroupLabel);

public sealed record ListedGroup(string? Key, string? Label, int Count);

/// <summary>A page of the artefact list.</summary>
public sealed record ArtefactListing(IReadOnlyList<ListedArtefact> Items, int Total, int Page, int PageSize, IReadOnlyList<ListedGroup> Groups);

internal static class ArtefactListingExtensions
{
    /// <summary>
    /// Runs the system query <c>artefact-list</c>, the query the list page reads. Grouping, sort and direction are given
    /// as the camel-case names the URL uses (<c>objectType</c>, <c>creationDate</c>, <c>asc</c>).
    /// </summary>
    public static async Task<ArtefactListing> ListArtefactsAsync(
        this HttpClient client, string? search = null, string? group = null, string? sort = null, string? direction = null,
        int? page = null, int? pageSize = null, string? lang = null)
    {
        var variables = new Dictionary<string, object?>();
        if (search is not null) variables["search"] = search;
        if (group is not null) variables["group"] = EnumName(group);
        if (sort is not null) variables["sort"] = EnumName(sort);
        if (direction is not null) variables["direction"] = EnumName(direction);
        if (page is not null) variables["page"] = page;
        if (pageSize is not null) variables["pageSize"] = pageSize;

        var list = (await client.RunStoredAsync("artefact-list", variables, lang)).Data().GetProperty("artefacts");
        return new ArtefactListing(
            [.. list.GetProperty("items").EnumerateArray().Select(Item)],
            list.GetProperty("total").GetInt32(),
            list.GetProperty("page").GetInt32(),
            list.GetProperty("pageSize").GetInt32(),
            [.. list.GetProperty("groups").EnumerateArray().Select(g => new ListedGroup(Text(g, "key"), Text(g, "label"), g.GetProperty("count").GetInt32()))]);
    }

    /// <summary><c>objectType</c> → <c>OBJECT_TYPE</c>, the name a GraphQL enum value has.</summary>
    private static string EnumName(string value) => System.Text.RegularExpressions.Regex.Replace(value, "([a-z0-9])([A-Z])", "$1_$2").ToUpperInvariant();

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? NameOf(JsonElement item, string field) =>
        item.GetProperty(field) is { ValueKind: JsonValueKind.Object } reference ? Text(reference, "name") : null;

    private static ListedArtefact Item(JsonElement i)
    {
        ImageUrls? primary = i.GetProperty("primaryImage") is { ValueKind: JsonValueKind.Object } image
            ? new ImageUrls(Text(image.GetProperty("urls"), "thumb")!, Text(image.GetProperty("urls"), "web")!, Text(image.GetProperty("urls"), "print")!)
            : null;
        return new ListedArtefact(
            Guid.Parse(Text(i, "id")!), Text(i, "inventoryNumber")!, Text(i, "title")!, Text(i, "maker"), Text(i, "medium"), primary,
            i.GetProperty("imageCount").GetInt32(), NameOf(i, "objectType"), NameOf(i, "collectionGroup"),
            i.GetProperty("placeOfProduction") is { ValueKind: JsonValueKind.Object } place ? Text(place, "text") : null,
            NameOf(i, "dynasty"), Text(i, "groupKey"), Text(i, "groupLabel"));
    }
}
