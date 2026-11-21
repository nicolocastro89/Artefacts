using System.Text.Json;
using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Common;
using Artefacts.Domain.Pages;

namespace Artefacts.Application.Pages;

/// <summary>Stored pages: the system pages (read-only) and the user pages people save.</summary>
public sealed class StoredPageHandler(IStoredPageRepository pages, StoredPageValidator validator, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<IReadOnlyList<StoredPageSummary>> ListAsync(bool navigationOnly, CancellationToken ct) =>
        (await pages.ListAsync(navigationOnly, ct)).Select(p => new StoredPageSummary(p.Id, p.Slug, p.Name, Kind(p), p.ShowInNavigation, p.NavigationLabel, p.UpdatedAt)).ToList();

    public async Task<StoredPageDto> GetAsync(string id, CancellationToken ct) =>
        ToDto(await pages.GetAsync(id, ct) ?? throw new NotFoundException("Stored page", id));

    public async Task<StoredPageDto> GetBySlugAsync(string slug, CancellationToken ct) =>
        ToDto(await pages.GetBySlugAsync(StoredPage.NormalizeSlug(slug), ct) ?? throw new NotFoundException("Stored page", slug));

    public async Task<StoredPageDto> CreateAsync(CreateStoredPageRequest request, CancellationToken ct)
    {
        await CheckDefinitionAsync(request.Definition, ct);
        var page = StoredPage.CreateUser(request.Name, request.Slug, request.Definition!.Value.GetRawText(), request.ShowInNavigation, request.NavigationLabel, clock.GetUtcNow());
        pages.Add(page);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(page);
    }

    public async Task<StoredPageDto> UpdateAsync(string id, ChangeStoredPageRequest request, CancellationToken ct)
    {
        var page = await LoadAsync(id, ct);
        page.EnsureUserPage();
        if (request.Definition is not null) await CheckDefinitionAsync(request.Definition, ct);
        page.Change(request.Name, request.Slug, request.Definition?.GetRawText(), request.ShowInNavigation, request.NavigationLabel, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(page);
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        var page = await LoadAsync(id, ct);
        page.EnsureUserPage();
        pages.Remove(page);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>A new user page with the definition of any stored page, system pages included.</summary>
    public async Task<StoredPageDto> DuplicateAsync(string id, DuplicateStoredPageRequest request, CancellationToken ct)
    {
        var copy = (await LoadAsync(id, ct)).Duplicate(request.Name, request.Slug, clock.GetUtcNow());
        pages.Add(copy);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(copy);
    }

    /// <summary>Rejects a definition that cannot be stored, with each problem and its path under <c>definition</c>.</summary>
    private async Task CheckDefinitionAsync(JsonElement? definition, CancellationToken ct)
    {
        var problems = await validator.ValidateAsync(definition, ct);
        if (problems.Count == 0) return;
        throw new DomainValidationException(new Dictionary<string, string[]> { ["definition"] = [.. problems] });
    }

    private async Task<StoredPage> LoadAsync(string id, CancellationToken ct) =>
        await pages.GetAsync(id, ct) ?? throw new NotFoundException("Stored page", id);

    private static StoredPageKind Kind(StoredPage p) => p.IsSystem ? StoredPageKind.System : StoredPageKind.User;

    private static StoredPageDto ToDto(StoredPage p) =>
        new(p.Id, p.Slug, p.Name, Kind(p), p.ShowInNavigation, p.NavigationLabel, JsonDocument.Parse(p.Definition).RootElement.Clone(), p.UpdatedAt);
}
