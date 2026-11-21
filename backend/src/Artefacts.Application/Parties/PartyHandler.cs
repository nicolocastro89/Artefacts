using Artefacts.Application.Abstractions;
using Artefacts.Application.Common;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;
using Artefacts.Domain.Parties;

namespace Artefacts.Application.Parties;

/// <summary>People and organisations: search with source and holder counts, add, rename, change kind, delete when unused.</summary>
public sealed class PartyHandler(IPartyRepository parties, IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<PartyDto>> SearchAsync(string? text, PartyKind? kind, CancellationToken ct)
    {
        var found = await parties.SearchAsync(text, kind, ct);
        var uses = await parties.CountUsesAsync(ct);
        return found.Select(p => ToDto(p, uses)).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<PartyDto> GetAsync(Guid id, CancellationToken ct) => await ToDtoAsync(await LoadAsync(id, ct), ct);

    /// <summary>Adds a party, or returns the existing one with the same name (its kind is left as it is).</summary>
    public async Task<AddPartyResult> AddAsync(PartyRequest request, CancellationToken ct)
    {
        var existing = await parties.FindByNameAsync(LocalizedName.Normalize(request.Name), ct);
        if (existing is not null) return new AddPartyResult(await ToDtoAsync(existing, ct), Created: false);

        var v = new ValidationCollector();
        var party = v.Try("", () => Party.Create(request.Name, request.Kind ?? PartyKind.Organisation));
        v.ThrowIfAny();
        parties.Add(party!);
        await unitOfWork.SaveChangesAsync(ct);
        return new AddPartyResult(ToDto(party!, new Dictionary<PartyId, PartyUses>()), Created: true);
    }

    /// <summary>Renames the party and changes its kind (kept when not given); the name must be unique.</summary>
    public async Task<PartyDto> UpdateAsync(Guid id, PartyRequest request, CancellationToken ct)
    {
        var party = await LoadAsync(id, ct);
        var v = new ValidationCollector();
        var clash = await parties.FindByNameAsync(LocalizedName.Normalize(request.Name), ct);
        if (clash is not null && clash.Id != party.Id) v.Add("name", $"Another party is already named \"{clash.Name}\".");
        v.Try("", () => party.Rename(request.Name));
        if (request.Kind is { } kind) v.Try("", () => party.ChangeKind(kind));
        v.ThrowIfAny();
        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(party, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var party = await LoadAsync(id, ct);
        var uses = (await parties.CountUsesAsync(ct)).GetValueOrDefault(party.Id);
        if (uses.Source + uses.Holder > 0)
            throw new ConflictException("party_in_use",
                $"\"{party.Name}\" is the acquisition source or holder of artefacts and cannot be deleted.");

        parties.Remove(party);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Party> LoadAsync(Guid id, CancellationToken ct) =>
        await parties.GetAsync(new PartyId(id), ct) ?? throw new NotFoundException("Party", id);

    private async Task<PartyDto> ToDtoAsync(Party p, CancellationToken ct) => ToDto(p, await parties.CountUsesAsync(ct));

    private static PartyDto ToDto(Party p, IReadOnlyDictionary<PartyId, PartyUses> uses)
    {
        var u = uses.GetValueOrDefault(p.Id);
        return new PartyDto(p.Id.Value, p.Name, p.Kind, u.Source, u.Holder);
    }
}
