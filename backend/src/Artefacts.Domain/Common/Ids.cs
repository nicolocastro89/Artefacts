namespace Artefacts.Domain.Common;

/// <summary>Marker for strongly typed identifiers backed by a <see cref="Guid"/>.</summary>
public interface IGuidId
{
    Guid Value { get; }
}

public readonly record struct ArtefactId(Guid Value) : IGuidId
{
    public static ArtefactId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct RegionId(Guid Value) : IGuidId
{
    public static RegionId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct MediaAssetId(Guid Value) : IGuidId
{
    public static MediaAssetId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct ObjectTypeId(Guid Value) : IGuidId
{
    public static ObjectTypeId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct ReferenceEntryId(Guid Value) : IGuidId
{
    public static ReferenceEntryId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct PartyId(Guid Value) : IGuidId
{
    public static PartyId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}
