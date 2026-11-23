using Artefacts.Domain.Common;

namespace Artefacts.Domain.Measures;

/// <summary>
/// The units every new database starts with, under fixed identifiers. They are ordinary data afterwards: they can be
/// renamed or given a corrected factor like any other unit.
/// </summary>
public static class SeededUnits
{
    public static readonly UnitOfMeasureId Millimetre = new(Guid.Parse("0193a1c0-0000-7000-8000-000000000001"));
    public static readonly UnitOfMeasureId Centimetre = new(Guid.Parse("0193a1c0-0000-7000-8000-000000000002"));
    public static readonly UnitOfMeasureId Metre = new(Guid.Parse("0193a1c0-0000-7000-8000-000000000003"));
    public static readonly UnitOfMeasureId Inch = new(Guid.Parse("0193a1c0-0000-7000-8000-000000000004"));

    /// <summary>Id, abbreviation, factor (metres per unit), English name, Italian name.</summary>
    public static readonly IReadOnlyList<(UnitOfMeasureId Id, string Abbreviation, decimal Factor, string English, string Italian)> All =
    [
        (Millimetre, "mm", 0.001m, "Millimetre", "millimetro"),
        (Centimetre, "cm", 0.01m, "Centimetre", "centimetro"),
        (Metre, "m", 1m, "Metre", "metro"),
        (Inch, "in", 0.0254m, "Inch", "pollice"),
    ];
}
