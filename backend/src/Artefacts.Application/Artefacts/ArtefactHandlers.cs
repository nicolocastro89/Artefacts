using Artefacts.Application.Abstractions;
using Artefacts.Application.Common;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Common;
using Artefacts.Domain.Dating;
using Artefacts.Domain.Finance;

namespace Artefacts.Application.Artefacts;

/// <summary>Create and update artefacts (identity, description, dating, acquisition, provenance).</summary>
public sealed class ArtefactCommandHandler(
    IArtefactRepository artefacts,
    IRegionRepository regions,
    IUnitOfWork unitOfWork,
    ArtefactDetailQueryHandler detail)
{
    public async Task<ArtefactDetail> CreateAsync(ArtefactWriteRequest request, CancellationToken ct)
    {
        var v = new ValidationCollector();
        var artefact = v.Try("", () => Artefact.Create(request.InventoryNumber, request.Title));
        // Keep validating the rest of the request even if identity failed, using a throwaway aggregate.
        var target = artefact ?? Artefact.Create("_", "_");
        await ApplyAsync(target, request, v, ct);
        if (artefact is not null)
            await EnsureUniqueInventoryNumberAsync(artefact.InventoryNumber, null, v, ct);
        v.ThrowIfAny();

        artefacts.Add(artefact!);
        await unitOfWork.SaveChangesAsync(ct);
        return await detail.GetAsync(artefact!.Id.Value, ct);
    }

    public async Task<ArtefactDetail> UpdateAsync(Guid id, ArtefactWriteRequest request, CancellationToken ct)
    {
        var artefact = await artefacts.GetAsync(new ArtefactId(id), ct) ?? throw new NotFoundException("Artefact", id);

        var v = new ValidationCollector();
        v.Try("", () => artefact.SetIdentity(request.InventoryNumber, request.Title));
        await ApplyAsync(artefact, request, v, ct);
        if (!v.HasErrors)
            await EnsureUniqueInventoryNumberAsync(artefact.InventoryNumber, artefact.Id, v, ct);
        v.ThrowIfAny();

        await unitOfWork.SaveChangesAsync(ct);
        return await detail.GetAsync(id, ct);
    }

    private async Task ApplyAsync(Artefact artefact, ArtefactWriteRequest request, ValidationCollector v, CancellationToken ct)
    {
        var creationDate = request.CreationDate is { Text: not null } cd && !string.IsNullOrWhiteSpace(cd.Text)
            ? v.Try("creationDate", () => HistoricalDate.Create(cd.Text, cd.EarliestYear, cd.LatestYear))
            : null;
        v.Try("", () => artefact.Describe(request.Maker, creationDate, request.Medium, request.Dimensions, request.Description));

        if (request.Acquisition is { } acq)
        {
            var price = acq.Price is { } p ? v.Try("acquisition.price", () => Money.Create(p.Amount, p.Currency)) : null;
            var acquisition = v.Try("acquisition", () => Acquisition.Create(acq.Method, acq.Date, acq.Source, price));
            if (acquisition is not null) artefact.SetAcquisition(acquisition);
        }
        else
        {
            artefact.SetAcquisition(null);
        }

        var inputs = request.Provenance ?? [];
        var known = await regions.GetManyAsync(inputs.Select(p => new RegionId(p.RegionId)).Distinct(), ct);
        var entries = new List<ProvenanceEntry>();
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            var prefix = $"provenance[{i}]";
            var regionId = new RegionId(input.RegionId);
            if (!known.ContainsKey(regionId))
                v.Add($"{prefix}.regionId", "The region does not exist.");
            var period = v.Try($"{prefix}.period",
                () => HistoricalDate.Create(input.Period?.Text, input.Period?.EarliestYear, input.Period?.LatestYear));
            if (period is null) continue;
            var entry = v.Try(prefix, () => ProvenanceEntry.Create(regionId, period, input.Note));
            if (entry is not null) entries.Add(entry);
        }

        if (!v.HasErrors) artefact.ReplaceProvenance(entries);
    }

    private async Task EnsureUniqueInventoryNumberAsync(string inventoryNumber, ArtefactId? excluding, ValidationCollector v, CancellationToken ct)
    {
        if (await artefacts.InventoryNumberExistsAsync(inventoryNumber, excluding, ct))
            v.Add("inventoryNumber", $"Inventory number \"{inventoryNumber}\" is already used by another artefact.");
    }
}

/// <summary>Builds the full artefact detail (with region names and signed image URLs) from the write model.</summary>
public sealed class ArtefactDetailQueryHandler(
    IArtefactRepository artefacts,
    IRegionRepository regions,
    IMediaAssetRepository mediaAssets,
    IImageUrlBuilder imageUrls)
{
    public async Task<ArtefactDetail> GetAsync(Guid id, CancellationToken ct)
    {
        var a = await artefacts.GetAsync(new ArtefactId(id), ct) ?? throw new NotFoundException("Artefact", id);

        var regionMap = await regions.GetManyAsync(a.Provenance.Select(p => p.RegionId).Distinct(), ct);
        var assetMap = await mediaAssets.GetManyAsync(a.Images.Select(i => i.MediaAssetId), ct);

        var images = a.Images
            .Where(i => assetMap.ContainsKey(i.MediaAssetId))
            .Select(i =>
            {
                var asset = assetMap[i.MediaAssetId];
                return new ArtefactImageDto(asset.Id.Value, asset.OriginalFileName, asset.Format, asset.SizeBytes,
                    asset.UploadedAt, i.Caption, i.IsPrimary, imageUrls.Build(asset.StorageKey));
            })
            .ToList();

        return new ArtefactDetail(
            a.Id.Value,
            a.InventoryNumber,
            a.Title,
            a.Maker,
            HistoricalDateDto.From(a.CreationDate),
            a.Medium,
            a.Dimensions,
            a.Description,
            a.Acquisition is { } acq
                ? new AcquisitionDto(acq.Method, acq.Date, acq.Source,
                    acq.Price is { } p ? new MoneyDto(p.Amount, p.Currency) : null)
                : null,
            a.Provenance
                .Select(p => new ProvenanceEntryDto(
                    p.RegionId.Value,
                    regionMap.TryGetValue(p.RegionId, out var r) ? r.Name : "(unknown region)",
                    HistoricalDateDto.From(p.Period)!,
                    p.Note))
                .ToList(),
            images.SingleOrDefault(i => i.IsPrimary),
            images);
    }
}

/// <summary>Interprets historical date text for live feedback, using exactly the domain rules.</summary>
public sealed class HistoricalDateParseHandler
{
    public HistoricalDateDto Parse(HistoricalDateInput input) =>
        HistoricalDateDto.From(HistoricalDate.Create(input.Text, input.EarliestYear, input.LatestYear))!;
}
