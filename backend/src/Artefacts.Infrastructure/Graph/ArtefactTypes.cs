using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Dating;
using Artefacts.Domain.Media;
using Artefacts.Domain.Parties;
using Artefacts.Infrastructure.Read;
using HotChocolate;

namespace Artefacts.Infrastructure.Graph;

/// <summary>A historical date: the text as written, the year range it stands for, and how precise it is.</summary>
public sealed record HistoricalDate(string Display, int? EarliestYear, int? LatestYear, HistoricalDatePrecision Precision)
{
    internal static HistoricalDate? From(string? display, int? earliest, int? latest, string? precision) =>
        display is null
            ? null
            : new HistoricalDate(display, earliest, latest,
                Enum.TryParse<HistoricalDatePrecision>(precision, out var p) ? p : HistoricalDatePrecision.Approximate);
}

/// <summary>An amount in an ISO 4217 currency.</summary>
public sealed record Money(decimal Amount, string Currency)
{
    internal static Money? From(decimal? amount, string? currency) =>
        amount is { } a && currency is not null ? new Money(a, currency.Trim()) : null;
}

/// <summary>A linked record with its name in the reader's language.</summary>
public sealed record NamedRef(Guid Id, string Name);

/// <summary>A linked party with its name and kind.</summary>
public sealed record PartyRef(Guid Id, string Name, PartyKind Kind);

/// <summary>A text in one language.</summary>
public sealed record Translation(string Language, string Text);

/// <summary>One step of an artefact's provenance: where it was, and when.</summary>
public sealed class ProvenanceEntry(ProvenanceRow row)
{
    public Task<NamedRef?> GetRegion(RegionNamesDataLoader names, CancellationToken cancellationToken) =>
        names.RefAsync(row.RegionId, cancellationToken);

    public HistoricalDate Period =>
        HistoricalDate.From(row.PeriodDisplay, row.PeriodEarliestYear, row.PeriodLatestYear, row.PeriodPrecision)!;

    public string? Note => row.Note;
}

/// <summary>An image attached to an artefact, with its file details and signed URLs.</summary>
public sealed class ArtefactImage(ImageRow row)
{
    public Guid MediaAssetId => row.MediaAssetId;
    public string OriginalFileName => row.OriginalFileName;
    public ImageFormat Format => Enum.Parse<ImageFormat>(row.Format, ignoreCase: true);
    public long SizeBytes => row.SizeBytes;
    public DateTimeOffset UploadedAt => row.UploadedAt;
    public string? Caption => row.Caption;
    public bool IsPrimary => row.IsPrimary;

    public ImageUrls GetUrls([Service] IImageUrlBuilder imageUrls) => imageUrls.Build(row.StorageKey);
}

/// <summary>Where an artefact was made: the text as written and the linked region, if any.</summary>
public sealed class PlaceOfProduction(string? text, Guid? regionId)
{
    public string? Text => text;

    public async Task<NamedRef?> GetRegion(RegionNamesDataLoader names, CancellationToken cancellationToken) =>
        await names.RefAsync(regionId, cancellationToken);
}

/// <summary>How the collection acquired an artefact. The price is financially sensitive.</summary>
public sealed class Acquisition(ArtefactRow row)
{
    public AcquisitionMethod Method => Enum.Parse<AcquisitionMethod>(row.AcquisitionMethod!);
    public DateOnly? Date => row.AcquisitionDate;
    public string? Lot => row.AcquisitionLot;
    public string? Note => row.AcquisitionNote;

    [SensitiveField(nameof(ArtefactRow.AcquisitionPriceAmount))]
    public Money? Price => Money.From(row.AcquisitionPriceAmount, row.AcquisitionPriceCurrency);

    public async Task<PartyRef?> GetSource(PartiesDataLoader parties, CancellationToken cancellationToken) =>
        await parties.RefAsync(row.AcquisitionSourceId, cancellationToken);
}

/// <summary>An artefact as read from the read views, with its linked records and lists.</summary>
public sealed class Artefact(ArtefactRow row, string? groupKey = null, string? groupLabel = null)
{
    internal ArtefactRow Row => row;

    public Guid Id => row.Id;
    public string InventoryNumber => row.InventoryNumber;
    public string Title => row.Title;
    public string? Maker => row.Maker;
    public HistoricalDate? CreationDate => HistoricalDate.From(row.CreationDateDisplay, row.CreationDateEarliestYear, row.CreationDateLatestYear, row.CreationDatePrecision);
    public string? Medium => row.Medium;
    public string? Dimensions => row.Dimensions;

    /// <summary>The century of the creation date (17 = 1601-1700; negative for BCE).</summary>
    public int? Century => row.Century;

    public int ExhibitionCount => row.ExhibitionCount;
    public int ImageCount => row.ImageCount;

    /// <summary>The artefact's group in a grouped list (null when the list is not grouped).</summary>
    public string? GroupKey => groupKey;

    /// <summary>The label of the artefact's group in a grouped list.</summary>
    public string? GroupLabel => groupLabel;

    /// <summary>The image shown for the artefact, with its file details, caption and signed URLs; null when it has none.</summary>
    public async Task<ArtefactImage?> GetPrimaryImage(ImagesDataLoader loader, CancellationToken cancellationToken) =>
        row.ImageCount == 0
            ? null
            : ((await loader.LoadAsync(row.Id, cancellationToken)) ?? []).Where(i => i.IsPrimary).Select(i => new ArtefactImage(i)).FirstOrDefault();

    public Task<NamedRef?> GetObjectType(ObjectTypeNamesDataLoader names, CancellationToken cancellationToken) =>
        names.RefAsync(row.ObjectTypeId, cancellationToken);

    public Task<NamedRef?> GetCollectionGroup(EntryNamesDataLoader names, CancellationToken cancellationToken) =>
        names.RefAsync(row.CollectionGroupId, cancellationToken);

    public Task<NamedRef?> GetDynasty(EntryNamesDataLoader names, CancellationToken cancellationToken) =>
        names.RefAsync(row.DynastyId, cancellationToken);

    public Task<NamedRef?> GetStorageLocation(EntryNamesDataLoader names, CancellationToken cancellationToken) =>
        names.RefAsync(row.StorageLocationId, cancellationToken);

    /// <summary>The holding party; null when the collection itself holds the artefact.</summary>
    public Task<PartyRef?> GetHolder(PartiesDataLoader parties, CancellationToken cancellationToken) =>
        parties.RefAsync(row.HolderId, cancellationToken);

    public PlaceOfProduction? PlaceOfProduction =>
        row.PlaceText is null && row.PlaceRegionId is null ? null : new PlaceOfProduction(row.PlaceText, row.PlaceRegionId);

    public Acquisition? Acquisition => row.AcquisitionMethod is null ? null : new Acquisition(row);

    [SensitiveField(nameof(ArtefactRow.ValuationAmount))]
    public Money? Valuation => Money.From(row.ValuationAmount, row.ValuationCurrency);

    /// <summary>The description in the reader's first language that has one; see <see cref="GetDescriptionLanguage"/>.</summary>
    public async Task<string?> GetDescription(DescriptionsDataLoader loader, [Service] ILanguagePreferences preferences, CancellationToken cancellationToken) =>
        (await DescriptionIn(loader, preferences, cancellationToken))?.Description;

    /// <summary>The language of <see cref="GetDescription"/>, which differs from the reader's when the description is a fallback.</summary>
    public async Task<string?> GetDescriptionLanguage(DescriptionsDataLoader loader, [Service] ILanguagePreferences preferences, CancellationToken cancellationToken) =>
        (await DescriptionIn(loader, preferences, cancellationToken))?.Language;

    /// <summary>The description in every language that has one.</summary>
    public async Task<IReadOnlyList<Translation>> GetDescriptions(DescriptionsDataLoader loader, CancellationToken cancellationToken) =>
        ((await loader.LoadAsync(row.Id, cancellationToken)) ?? []).Select(d => new Translation(d.Language, d.Description)).ToList();

    public async Task<IReadOnlyList<string>> GetExhibitions(ExhibitionsDataLoader loader, CancellationToken cancellationToken) =>
        ((await loader.LoadAsync(row.Id, cancellationToken)) ?? []).Select(e => e.Text).ToList();

    public async Task<IReadOnlyList<ProvenanceEntry>> GetProvenance(ProvenanceDataLoader loader, CancellationToken cancellationToken) =>
        ((await loader.LoadAsync(row.Id, cancellationToken)) ?? []).Select(p => new ProvenanceEntry(p)).ToList();

    /// <summary>Every image, in the order attached.</summary>
    public async Task<IReadOnlyList<ArtefactImage>> GetImages(ImagesDataLoader loader, CancellationToken cancellationToken) =>
        ((await loader.LoadAsync(row.Id, cancellationToken)) ?? []).Select(i => new ArtefactImage(i)).ToList();

    /// <summary>The catalogue entry in the reader's first language that has one; see <see cref="GetCatalogueEntryLanguage"/>.</summary>
    public async Task<string?> GetCatalogueEntry(CatalogueEntriesDataLoader loader, [Service] ILanguagePreferences preferences, CancellationToken cancellationToken) =>
        (await CatalogueEntryIn(loader, preferences, cancellationToken))?.CatalogueEntry;

    /// <summary>The language of <see cref="GetCatalogueEntry"/>, which differs from the reader's when the entry is a fallback.</summary>
    public async Task<string?> GetCatalogueEntryLanguage(CatalogueEntriesDataLoader loader, [Service] ILanguagePreferences preferences, CancellationToken cancellationToken) =>
        (await CatalogueEntryIn(loader, preferences, cancellationToken))?.Language;

    /// <summary>The catalogue entry in every language that has one.</summary>
    public async Task<IReadOnlyList<Translation>> GetCatalogueEntries(CatalogueEntriesDataLoader loader, CancellationToken cancellationToken) =>
        ((await loader.LoadAsync(row.Id, cancellationToken)) ?? []).Select(e => new Translation(e.Language, e.CatalogueEntry)).ToList();

    private async Task<CatalogueEntryRow?> CatalogueEntryIn(CatalogueEntriesDataLoader loader, ILanguagePreferences preferences, CancellationToken cancellationToken)
    {
        var entries = await loader.LoadAsync(row.Id, cancellationToken) ?? [];
        return preferences.Current.Select(l => entries.FirstOrDefault(e => e.Language == l)).FirstOrDefault(e => e is not null);
    }

    private async Task<DescriptionRow?> DescriptionIn(DescriptionsDataLoader loader, ILanguagePreferences preferences, CancellationToken cancellationToken)
    {
        var descriptions = await loader.LoadAsync(row.Id, cancellationToken) ?? [];
        return preferences.Current.Select(l => descriptions.FirstOrDefault(d => d.Language == l)).FirstOrDefault(d => d is not null);
    }
}
