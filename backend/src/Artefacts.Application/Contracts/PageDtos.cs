using System.Text.Json;

namespace Artefacts.Application.Contracts;

public enum StoredPageKind
{
    System = 0,
    User = 1,
}

/// <summary>A stored page in the list: system pages ship with the application, user pages are saved by users.</summary>
public sealed record StoredPageSummary(
    string Id, string Slug, string Name, StoredPageKind Kind, bool ShowInNavigation, string? NavigationLabel, DateTimeOffset UpdatedAt);

/// <summary>A stored page with its definition, the JSON of the page's containers, data sources, layouts and widgets.</summary>
public sealed record StoredPageDto(
    string Id, string Slug, string Name, StoredPageKind Kind, bool ShowInNavigation, string? NavigationLabel, JsonElement Definition, DateTimeOffset UpdatedAt);

public sealed record CreateStoredPageRequest(string? Name, string? Slug, JsonElement? Definition, bool ShowInNavigation = false, string? NavigationLabel = null);

/// <summary>Changes the page; a value left out is kept.</summary>
public sealed record ChangeStoredPageRequest(
    string? Name = null, string? Slug = null, JsonElement? Definition = null, bool? ShowInNavigation = null, string? NavigationLabel = null);

public sealed record DuplicateStoredPageRequest(string? Name, string? Slug);
