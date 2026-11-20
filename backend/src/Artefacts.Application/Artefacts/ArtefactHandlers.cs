using Artefacts.Application.Abstractions;
using Artefacts.Application.Common;
using Artefacts.Application.Contracts;
using Artefacts.Application.Languages;
using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Classification;
using Artefacts.Domain.Common;
using Artefacts.Domain.Dating;
using Artefacts.Domain.Finance;

namespace Artefacts.Application.Artefacts;

/// <summary>Create and update artefacts (identity, description, dating, acquisition, provenance).</summary>
public sealed class ArtefactCommandHandler(
    IArtefactRepository artefacts,
    IRegionRepository regions,
    IObjectTypeRepository objectTypes,
    IContentLanguages contentLanguages,
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
        v.Try("", () => artefact.Describe(request.Maker, creationDate, request.Medium, request.Dimensions));
        var descriptions = TranslationInput.Check(request.Descriptions, await contentLanguages.ListAsync(ct), "descriptions", v);
        v.Try("", () => artefact.ReplaceDescriptions(descriptions));

        if (request.Acquisition is { } acq)
        {
            var price = acq.Price is { } p ? v.Try("acquisition.price", () => Money.Create(p.Amount, p.Currency)) : null;
            var acquisition = v.Try("acquisition", () => Acquisition.Create(acq.Method, acq.Date, acq.Source, price, acq.Lot, acq.Note));
            if (acquisition is not null) artefact.SetAcquisition(acquisition);
        }
        else
        {
            artefact.SetAcquisition(null);
        }

        await ApplyClassificationAsync(artefact, request, v, ct);

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

    /// <summary>Object type, collection group, origin, exhibitions, catalogue entry and holding.</summary>
    private async Task ApplyClassificationAsync(Artefact artefact, ArtefactWriteRequest request, ValidationCollector v, CancellationToken ct)
    {
        ObjectTypeId? objectTypeId = request.ObjectTypeId is { } t ? new ObjectTypeId(t) : null;
        if (objectTypeId is { } typeId && await objectTypes.GetAsync(typeId, ct) is null)
            v.Add("objectTypeId", "The object type does not exist.");
        v.Try("", () => artefact.Classify(objectTypeId, request.CollectionGroup));

        RegionId? placeRegion = request.PlaceOfProduction?.RegionId is { } r ? new RegionId(r) : null;
        if (placeRegion is { } regionId && await regions.GetAsync(regionId, ct) is null)
            v.Add("placeOfProduction.regionId", "The region does not exist.");
        var place = v.Try("", () => PlaceOfProduction.Create(request.PlaceOfProduction?.Text, placeRegion));
        v.Try("", () => artefact.SetOrigin(place, request.Dynasty));

        v.Try("", () => artefact.ReplaceExhibitions(request.Exhibitions ?? []));
        v.Try("", () => artefact.SetCatalogueEntry(request.CatalogueEntryEn));

        var valuation = request.Valuation is { } val ? v.Try("valuation", () => Money.Create(val.Amount, val.Currency)) : null;
        v.Try("", () => artefact.SetHolding(request.Holder, request.StorageLocation, valuation));
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
    IObjectTypeRepository objectTypes,
    ILanguagePreferences preferences,
    IMediaAssetRepository mediaAssets,
    IImageUrlBuilder imageUrls)
{
    public async Task<ArtefactDetail> GetAsync(Guid id, CancellationToken ct)
    {
        var a = await artefacts.GetAsync(new ArtefactId(id), ct) ?? throw new NotFoundException("Artefact", id);

        var regionIds = a.Provenance.Select(p => p.RegionId).ToList();
        if (a.PlaceOfProduction?.RegionId is { } placeRegionId) regionIds.Add(placeRegionId);
        var regionMap = await regions.GetManyAsync(regionIds.Distinct(), ct);
        var objectType = a.ObjectTypeId is { } typeId ? await objectTypes.GetAsync(typeId, ct) : null;
        var languages = preferences.Current;
        var description = a.DescriptionIn(languages);
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
            description?.Text,
            description?.Language,
            a.Translations.Where(t => t.Description is not null).ToDictionary(t => t.Language, t => t.Description!),
            languages.FirstOrDefault() ?? "",
            a.Acquisition is { } acq
                ? new AcquisitionDto(acq.Method, acq.Date, acq.Source,
                    acq.Price is { } p ? new MoneyDto(p.Amount, p.Currency) : null, acq.Lot, acq.Note)
                : null,
            a.Provenance
                .Select(p => new ProvenanceEntryDto(
                    p.RegionId.Value,
                    regionMap.TryGetValue(p.RegionId, out var r) ? r.NameIn(languages)?.Text ?? "" : "(unknown region)",
                    HistoricalDateDto.From(p.Period)!,
                    p.Note))
                .ToList(),
            images.SingleOrDefault(i => i.IsPrimary),
            images,
            objectType is null ? null : new NamedRefDto(objectType.Id.Value, objectType.NameIn(languages)?.Text ?? ""),
            a.CollectionGroup,
            a.PlaceOfProduction is { } place
                ? new PlaceOfProductionDto(place.Text,
                    place.RegionId is { } pr && regionMap.TryGetValue(pr, out var placeRegion)
                        ? new NamedRefDto(placeRegion.Id.Value, placeRegion.NameIn(languages)?.Text ?? "")
                        : null)
                : null,
            a.Dynasty,
            a.Exhibitions.Select(e => e.Text).ToList(),
            a.CatalogueEntryEn,
            a.Holder,
            a.StorageLocation,
            a.Valuation is { } val ? new MoneyDto(val.Amount, val.Currency) : null);
    }
}

/// <summary>Interprets historical date text for live feedback, using exactly the domain rules.</summary>
public sealed class HistoricalDateParseHandler
{
    public HistoricalDateDto Parse(HistoricalDateInput input) =>
        HistoricalDateDto.From(HistoricalDate.Create(input.Text, input.EarliestYear, input.LatestYear))!;
}
