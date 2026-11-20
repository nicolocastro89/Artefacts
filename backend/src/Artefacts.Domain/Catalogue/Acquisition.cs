using Artefacts.Domain.Common;
using Artefacts.Domain.Finance;

namespace Artefacts.Domain.Catalogue;

public enum AcquisitionMethod
{
    Purchase = 0,
    Gift = 1,
    Bequest = 2,
    Transfer = 3,
    Other = 4,
}

/// <summary>How and when the institution acquired an artefact. <see cref="Price"/> is financially sensitive.</summary>
public sealed class Acquisition
{
    public const int MaxSourceLength = 300;
    public const int MaxLotLength = 100;
    public const int MaxNoteLength = 2000;

    public AcquisitionMethod Method { get; private set; }
    public DateOnly? Date { get; private set; }
    public string? Source { get; private set; }
    public Money? Price { get; private set; }

    /// <summary>Auction lot or similar reference, as written (e.g. "170").</summary>
    public string? Lot { get; private set; }

    /// <summary>Free remarks about the acquisition, as written.</summary>
    public string? Note { get; private set; }

    // For EF Core.
    private Acquisition()
    {
    }

    public static Acquisition Create(
        AcquisitionMethod method, DateOnly? date, string? source, Money? price, string? lot = null, string? note = null)
    {
        if (!Enum.IsDefined(method))
            throw new DomainValidationException("method", "Is not a valid acquisition method.");
        return new Acquisition
        {
            Method = method,
            Date = date,
            Source = Guard.Optional(source, "source", MaxSourceLength),
            Price = price,
            Lot = Guard.Optional(lot, "lot", MaxLotLength),
            Note = Guard.Optional(note, "note", MaxNoteLength),
        };
    }
}
