using Artefacts.Domain.Common;

namespace Artefacts.Domain.Catalogue;

/// <summary>
/// Where an artefact was made: the text exactly as the curator wrote it (which may be uncertain,
/// e.g. "Anatolia o Asia Centrale?"), optionally linked to a region from the reference list.
/// </summary>
public sealed class PlaceOfProduction
{
    public const int MaxTextLength = 300;

    public string? Text { get; private set; }
    public RegionId? RegionId { get; private set; }

    // For EF Core.
    private PlaceOfProduction()
    {
    }

    /// <summary>Returns null when there is neither text nor a region.</summary>
    public static PlaceOfProduction? Create(string? text, RegionId? regionId)
    {
        var clean = Guard.Optional(text, "placeOfProduction.text", MaxTextLength);
        if (clean is null && regionId is null) return null;
        return new PlaceOfProduction { Text = clean, RegionId = regionId };
    }
}
