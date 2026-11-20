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

    public AcquisitionMethod Method { get; private set; }
    public DateOnly? Date { get; private set; }
    public string? Source { get; private set; }
    public Money? Price { get; private set; }

    // For EF Core.
    private Acquisition()
    {
    }

    public static Acquisition Create(AcquisitionMethod method, DateOnly? date, string? source, Money? price)
    {
        if (!Enum.IsDefined(method))
            throw new DomainValidationException("method", "Is not a valid acquisition method.");
        return new Acquisition
        {
            Method = method,
            Date = date,
            Source = Guard.Optional(source, "source", MaxSourceLength),
            Price = price,
        };
    }
}
