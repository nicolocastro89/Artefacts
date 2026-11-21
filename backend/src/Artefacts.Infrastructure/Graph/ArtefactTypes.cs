using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Dating;
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

    [Sensitive("financial")]
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

    public ImageUrls? GetPrimaryImage([Service] IImageUrlBuilder imageUrls) =>
        row.PrimaryImageKey is null ? null : imageUrls.Build(row.PrimaryImageKey);

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

    [Sensitive("financial")]
    public Money? Valuation => Money.From(row.ValuationAmount, row.ValuationCurrency);
}
