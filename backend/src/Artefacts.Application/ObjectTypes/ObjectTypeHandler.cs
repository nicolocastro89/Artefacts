using Artefacts.Application.Abstractions;
using Artefacts.Application.Common;
using Artefacts.Application.Contracts;
using Artefacts.Application.Languages;
using Artefacts.Domain.Classification;
using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;

namespace Artefacts.Application.ObjectTypes;

public sealed class ObjectTypeHandler(
    IObjectTypeRepository objectTypes,
    IArtefactRepository artefacts,
    IContentLanguages contentLanguages,
    ILanguagePreferences preferences,
    IUnitOfWork unitOfWork)
{
    /// <summary>Object types whose name in any language contains <paramref name="text"/>, ordered by the resolved name.</summary>
    public async Task<IReadOnlyList<ObjectTypeDto>> SearchAsync(string? text, CancellationToken ct)
    {
        var found = await objectTypes.SearchAsync(text, ct);
        var counts = await artefacts.CountByObjectTypeAsync(ct);
        return found.Select(t => ToDto(t, counts)).OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<ObjectTypeDto> GetAsync(Guid id, CancellationToken ct) => await ToDtoAsync(await LoadAsync(id, ct), ct);

    /// <summary>
    /// Adds an object type, or returns the existing one when one of the given names already names an object type in
    /// the same language.
    /// </summary>
    public async Task<AddObjectTypeResult> AddAsync(AddObjectTypeRequest request, CancellationToken ct)
    {
        var v = new ValidationCollector();
        var names = TranslationInput.Check(request.Names, await contentLanguages.ListAsync(ct), "names", v);
        v.ThrowIfAny();

        foreach (var (language, name) in names.Where(n => !string.IsNullOrWhiteSpace(n.Value)))
        {
            var existing = await objectTypes.FindByNameAsync(language, LocalizedName.Normalize(name), ct);
            if (existing is not null) return new AddObjectTypeResult(await ToDtoAsync(existing, ct), Created: false);
        }

        var objectType = ObjectType.Create(names);
        objectTypes.Add(objectType);
        await unitOfWork.SaveChangesAsync(ct);
        return new AddObjectTypeResult(ToDto(objectType, new Dictionary<ObjectTypeId, int>()), Created: true);
    }

    /// <summary>Replaces the object type's names; each must be unique within its language.</summary>
    public async Task<ObjectTypeDto> SetNamesAsync(Guid id, SetNamesRequest request, CancellationToken ct)
    {
        var objectType = await LoadAsync(id, ct);
        var v = new ValidationCollector();
        var names = TranslationInput.Check(request.Names, await contentLanguages.ListAsync(ct), "names", v);
        foreach (var (language, name) in names.Where(n => !string.IsNullOrWhiteSpace(n.Value)))
        {
            var clash = await objectTypes.FindByNameAsync(language, LocalizedName.Normalize(name), ct);
            if (clash is not null && clash.Id != objectType.Id)
                v.Add($"names.{language}", $"Another object type is already named \"{name!.Trim()}\" in this language.");
        }
        v.Try("", () => objectType.SetNames(names));
        v.ThrowIfAny();

        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(objectType, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var objectType = await LoadAsync(id, ct);
        if (await artefacts.AnyReferencesAsync(objectType.Id, ct))
            throw new ConflictException("object_type_in_use",
                $"\"{objectType.NameIn(preferences.Current)?.Text}\" is the object type of artefacts and cannot be deleted.");

        objectTypes.Remove(objectType);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<ObjectType> LoadAsync(Guid id, CancellationToken ct) =>
        await objectTypes.GetAsync(new ObjectTypeId(id), ct) ?? throw new NotFoundException("Object type", id);

    private async Task<ObjectTypeDto> ToDtoAsync(ObjectType t, CancellationToken ct) =>
        ToDto(t, await artefacts.CountByObjectTypeAsync(ct));

    private ObjectTypeDto ToDto(ObjectType t, IReadOnlyDictionary<ObjectTypeId, int> counts)
    {
        var name = t.NameIn(preferences.Current);
        return new ObjectTypeDto(
            t.Id.Value,
            name?.Text ?? "",
            name?.Language ?? "",
            t.Names.ToDictionary(n => n.Language, n => n.Name),
            counts.TryGetValue(t.Id, out var n) ? n : 0);
    }
}
