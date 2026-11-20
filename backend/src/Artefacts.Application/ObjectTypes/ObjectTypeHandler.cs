using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Classification;
using Artefacts.Domain.Common;

namespace Artefacts.Application.ObjectTypes;

public sealed class ObjectTypeHandler(IObjectTypeRepository objectTypes, IArtefactRepository artefacts, IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<ObjectTypeDto>> SearchAsync(string? text, CancellationToken ct)
    {
        var found = await objectTypes.SearchAsync(text, ct);
        var counts = await artefacts.CountByObjectTypeAsync(ct);
        return found.Select(t => ToDto(t, counts)).ToList();
    }

    public async Task<ObjectTypeDto> GetAsync(Guid id, CancellationToken ct) => await ToDtoAsync(await LoadAsync(id, ct), ct);

    /// <summary>Adds an object type, or returns the existing one when the normalised name already exists.</summary>
    public async Task<AddObjectTypeResult> AddAsync(AddObjectTypeRequest request, CancellationToken ct)
    {
        var existing = await objectTypes.FindByNormalizedNameAsync(ObjectType.Normalize(request.Name), ct);
        if (existing is not null)
            return new AddObjectTypeResult(await ToDtoAsync(existing, ct), Created: false);

        var objectType = ObjectType.Create(request.Name);
        objectTypes.Add(objectType);
        await unitOfWork.SaveChangesAsync(ct);
        return new AddObjectTypeResult(new ObjectTypeDto(objectType.Id.Value, objectType.Name, 0), Created: true);
    }

    public async Task<ObjectTypeDto> RenameAsync(Guid id, RenameObjectTypeRequest request, CancellationToken ct)
    {
        var objectType = await LoadAsync(id, ct);
        var clash = await objectTypes.FindByNormalizedNameAsync(ObjectType.Normalize(request.Name), ct);
        if (clash is not null && clash.Id != objectType.Id)
            throw new DomainValidationException("name", $"An object type named \"{clash.Name}\" already exists.");

        objectType.Rename(request.Name);
        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(objectType, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var objectType = await LoadAsync(id, ct);
        if (await artefacts.AnyReferencesAsync(objectType.Id, ct))
            throw new ConflictException("object_type_in_use",
                $"\"{objectType.Name}\" is the object type of artefacts and cannot be deleted.");

        objectTypes.Remove(objectType);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<ObjectType> LoadAsync(Guid id, CancellationToken ct) =>
        await objectTypes.GetAsync(new ObjectTypeId(id), ct) ?? throw new NotFoundException("Object type", id);

    private async Task<ObjectTypeDto> ToDtoAsync(ObjectType t, CancellationToken ct) =>
        ToDto(t, await artefacts.CountByObjectTypeAsync(ct));

    private static ObjectTypeDto ToDto(ObjectType t, IReadOnlyDictionary<ObjectTypeId, int> counts) =>
        new(t.Id.Value, t.Name, counts.TryGetValue(t.Id, out var n) ? n : 0);
}
