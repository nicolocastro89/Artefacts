using Artefacts.Application.Abstractions;
using Artefacts.Application.Common;
using Artefacts.Application.Contracts;
using Artefacts.Application.Languages;
using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;
using Artefacts.Domain.Lists;

namespace Artefacts.Application.Lists;

/// <summary>
/// The managed lists (dynasties, collection groups, storage locations): search with usage counts, add (returning an
/// existing entry with the same name), rename, and delete when unused.
/// </summary>
public sealed class ListHandler(
    IReferenceEntryRepository entries,
    IContentLanguages contentLanguages,
    ILanguagePreferences preferences,
    IUnitOfWork unitOfWork)
{
    /// <summary>Entries whose name in any language contains <paramref name="text"/>, ordered by the resolved name.</summary>
    public async Task<IReadOnlyList<ReferenceEntryDto>> SearchAsync(ListKind kind, string? text, CancellationToken ct)
    {
        var found = await entries.SearchAsync(kind, text, ct);
        var counts = await entries.CountUsesAsync(kind, ct);
        return found.Select(e => ToDto(e, counts)).OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<ReferenceEntryDto> GetAsync(ListKind kind, Guid id, CancellationToken ct) =>
        await ToDtoAsync(await LoadAsync(kind, id, ct), ct);

    /// <summary>Adds an entry, or returns the existing one when one of the names already names an entry of the list.</summary>
    public async Task<AddReferenceEntryResult> AddAsync(ListKind kind, ReferenceEntryNamesRequest request, CancellationToken ct)
    {
        var v = new ValidationCollector();
        ReferenceEntry? entry;
        if (kind.IsTranslated())
        {
            var names = TranslationInput.Check(request.Names, await contentLanguages.ListAsync(ct), "names", v);
            v.ThrowIfAny();
            foreach (var (language, name) in names.Where(n => !string.IsNullOrWhiteSpace(n.Value)))
            {
                var existing = await entries.FindByNameAsync(kind, language, LocalizedName.Normalize(name), ct);
                if (existing is not null) return new AddReferenceEntryResult(await ToDtoAsync(existing, ct), Created: false);
            }

            entry = v.Try("", () => ReferenceEntry.Create(kind, names));
        }
        else
        {
            var existing = await entries.FindByNameAsync(kind, null, LocalizedName.Normalize(request.Name), ct);
            if (existing is not null) return new AddReferenceEntryResult(await ToDtoAsync(existing, ct), Created: false);
            entry = v.Try("", () => ReferenceEntry.Create(kind, request.Name));
        }

        v.ThrowIfAny();
        entries.Add(entry!);
        await unitOfWork.SaveChangesAsync(ct);
        return new AddReferenceEntryResult(ToDto(entry!, new Dictionary<ReferenceEntryId, int>()), Created: true);
    }

    /// <summary>Replaces the entry's names (or its single name); each must be unique within the list (and language).</summary>
    public async Task<ReferenceEntryDto> SetNamesAsync(ListKind kind, Guid id, ReferenceEntryNamesRequest request, CancellationToken ct)
    {
        var entry = await LoadAsync(kind, id, ct);
        var v = new ValidationCollector();
        if (kind.IsTranslated())
        {
            var names = TranslationInput.Check(request.Names, await contentLanguages.ListAsync(ct), "names", v);
            foreach (var (language, name) in names.Where(n => !string.IsNullOrWhiteSpace(n.Value)))
            {
                var clash = await entries.FindByNameAsync(kind, language, LocalizedName.Normalize(name), ct);
                if (clash is not null && clash.Id != entry.Id)
                    v.Add($"names.{language}", $"Another entry of this list is already named \"{name!.Trim()}\" in this language.");
            }
            v.Try("", () => entry.SetNames(names));
        }
        else
        {
            var clash = await entries.FindByNameAsync(kind, null, LocalizedName.Normalize(request.Name), ct);
            if (clash is not null && clash.Id != entry.Id)
                v.Add("name", $"Another entry of this list is already named \"{clash.Name}\".");
            v.Try("", () => entry.Rename(request.Name));
        }

        v.ThrowIfAny();
        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(entry, ct);
    }

    public async Task DeleteAsync(ListKind kind, Guid id, CancellationToken ct)
    {
        var entry = await LoadAsync(kind, id, ct);
        if ((await entries.CountUsesAsync(kind, ct)).GetValueOrDefault(entry.Id) > 0)
            throw new ConflictException("entry_in_use",
                $"\"{entry.NameIn(preferences.Current)?.Text}\" is used by artefacts and cannot be deleted.");

        entries.Remove(entry);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<ReferenceEntry> LoadAsync(ListKind kind, Guid id, CancellationToken ct) =>
        await entries.GetAsync(kind, new ReferenceEntryId(id), ct) ?? throw new NotFoundException("List entry", id);

    private async Task<ReferenceEntryDto> ToDtoAsync(ReferenceEntry e, CancellationToken ct) =>
        ToDto(e, await entries.CountUsesAsync(e.Kind, ct));

    private ReferenceEntryDto ToDto(ReferenceEntry e, IReadOnlyDictionary<ReferenceEntryId, int> counts)
    {
        var name = e.NameIn(preferences.Current);
        return new ReferenceEntryDto(
            e.Id.Value,
            e.Kind,
            name?.Text ?? "",
            e.Kind.IsTranslated() ? name?.Language : null,
            e.Kind.IsTranslated() ? e.Names.ToDictionary(n => n.Language, n => n.Name) : null,
            counts.GetValueOrDefault(e.Id));
    }
}
