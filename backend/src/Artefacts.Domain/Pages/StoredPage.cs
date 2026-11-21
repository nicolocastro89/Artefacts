using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;

namespace Artefacts.Domain.Pages;

/// <summary>
/// A page definition kept as data (aggregate root): the containers, data sources, layouts and widgets of a page, shown
/// at <c>/p/{slug}</c> or, for the built-in pages, at their own routes. System pages ship with the application, keep a
/// readable identifier and cannot be changed through the API; user pages get a generated identifier. Whether the
/// definition has the right shape is checked by whoever saves it, which needs the stored queries.
/// </summary>
public sealed partial class StoredPage
{
    public const int MaxNameLength = 200;
    public const int MaxSlugLength = 100;
    public const int MaxNavigationLabelLength = 60;
    public const string UserIdPrefix = "p-";

    /// <summary>The slug of a system page (<c>artefact-list</c>) or <c>p-</c> and a short random text of a user page.</summary>
    public string Id { get; private set; } = default!;

    /// <summary>Lower case letters, digits and hyphens: the address of the page and unique across stored pages.</summary>
    public string Slug { get; private set; } = default!;

    public string Name { get; private set; } = default!;

    /// <summary>Lower case, whitespace collapsed: unique across stored pages.</summary>
    public string NormalizedName { get; private set; } = default!;

    /// <summary>The page definition as JSON text.</summary>
    public string Definition { get; private set; } = default!;

    public bool IsSystem { get; private set; }
    public bool ShowInNavigation { get; private set; }
    public string? NavigationLabel { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // For EF Core.
    private StoredPage()
    {
    }

    public static StoredPage CreateUser(string? name, string? slug, string definition, bool showInNavigation, string? navigationLabel, DateTimeOffset now)
    {
        var page = new StoredPage { Id = UserIdPrefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(5)), CreatedAt = now };
        page.Set(name, slug, definition, showInNavigation, navigationLabel, now);
        return page;
    }

    public static StoredPage CreateSystem(string slug, string name, string definition, DateTimeOffset now)
    {
        var page = new StoredPage { Id = slug, IsSystem = true, CreatedAt = now };
        page.Set(name, slug, definition, false, null, now);
        return page;
    }

    /// <summary>A new user page with this page's definition, not shown in navigation until the copy says so.</summary>
    public StoredPage Duplicate(string? name, string? slug, DateTimeOffset now) => CreateUser(name, slug, Definition, false, null, now);

    /// <summary>Changes the page; a value left null is kept. Not for system pages.</summary>
    public void Change(string? name, string? slug, string? definition, bool? showInNavigation, string? navigationLabel, DateTimeOffset now)
    {
        EnsureUserPage();
        Set(name ?? Name, slug ?? Slug, definition ?? Definition, showInNavigation ?? ShowInNavigation, navigationLabel ?? NavigationLabel, now);
    }

    /// <summary>Brings a system page to the name and definition of the running release.</summary>
    public void ReplaceSystem(string name, string definition, DateTimeOffset now)
    {
        if (!IsSystem) throw new InvalidOperationException($"Page '{Id}' is not a system page.");
        if (name != Name || definition != Definition) Set(name, Slug, definition, false, null, now);
    }

    /// <summary>Throws the conflict that tells a client system pages are read-only.</summary>
    public void EnsureUserPage()
    {
        if (IsSystem) throw new ConflictException("system_page", "System pages cannot be changed or deleted; duplicate it to change a copy.");
    }

    /// <summary>The slug as it is stored: trimmed and lower case. Does not check the rules; <see cref="Set"/> does.</summary>
    public static string NormalizeSlug(string? slug) => (slug ?? "").Trim().ToLowerInvariant();

    private void Set(string? name, string? slug, string definition, bool showInNavigation, string? navigationLabel, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();
        var cleanName = Collect(errors, "name", () => Guard.Required(name, "name", MaxNameLength));
        var cleanSlug = NormalizeSlug(slug);
        if (cleanSlug.Length == 0) errors["slug"] = ["Is required."];
        else if (cleanSlug.Length > MaxSlugLength) errors["slug"] = [$"Must be at most {MaxSlugLength} characters."];
        else if (!SlugPattern().IsMatch(cleanSlug)) errors["slug"] = ["Use lower case letters, digits and single hyphens between them."];
        var label = Collect(errors, "navigationLabel", () => Guard.Optional(navigationLabel, "navigationLabel", MaxNavigationLabelLength));
        if (showInNavigation && label is null && !errors.ContainsKey("navigationLabel")) errors["navigationLabel"] = ["Is required for a page shown in navigation."];
        if (errors.Count > 0) throw new DomainValidationException(errors);

        Name = cleanName!;
        NormalizedName = LocalizedName.Normalize(Name);
        Slug = cleanSlug;
        Definition = definition;
        ShowInNavigation = showInNavigation;
        NavigationLabel = label;
        UpdatedAt = now;
    }

    private static string? Collect(Dictionary<string, string[]> errors, string field, Func<string?> read)
    {
        try
        {
            return read();
        }
        catch (DomainValidationException e)
        {
            errors[field] = e.Errors.Values.SelectMany(v => v).ToArray();
            return null;
        }
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
