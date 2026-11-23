using Artefacts.Domain.Common;

namespace Artefacts.Domain.Measures;

/// <summary>A measured length: a positive number in a unit of measure, kept in the unit it was entered in.</summary>
public sealed class Measurement : IEquatable<Measurement>
{
    public decimal Value { get; private set; }
    public UnitOfMeasureId UnitId { get; private set; }

    // For EF Core.
    private Measurement()
    {
    }

    private Measurement(decimal value, UnitOfMeasureId unitId)
    {
        Value = value;
        UnitId = unitId;
    }

    /// <exception cref="DomainValidationException">Keyed <c>value</c> or <c>unitId</c>.</exception>
    public static Measurement Create(decimal? value, Guid? unitId)
    {
        var errors = new Dictionary<string, string[]>();
        if (value is null)
            errors["value"] = ["A value is required."];
        else if (value <= 0)
            errors["value"] = ["Must be greater than zero."];
        else if (decimal.Round(value.Value, 4) != value.Value)
            errors["value"] = ["At most 4 decimal places are allowed."];
        if (unitId is null || unitId == Guid.Empty)
            errors["unitId"] = ["Choose a unit."];

        if (errors.Count > 0) throw new DomainValidationException(errors);
        return new Measurement(value!.Value, new UnitOfMeasureId(unitId!.Value));
    }

    public bool Equals(Measurement? other) => other is not null && Value == other.Value && UnitId == other.UnitId;
    public override bool Equals(object? obj) => Equals(obj as Measurement);
    public override int GetHashCode() => HashCode.Combine(Value, UnitId);
}

/// <summary>The four measurements an artefact can record; each is optional and has its own unit.</summary>
public sealed record ArtefactMeasurements(Measurement? Width, Measurement? Height, Measurement? Length, Measurement? Diameter)
{
    public static readonly ArtefactMeasurements None = new(null, null, null, null);

    public IEnumerable<UnitOfMeasureId> UnitIds =>
        new[] { Width, Height, Length, Diameter }.Where(m => m is not null).Select(m => m!.UnitId);
}
