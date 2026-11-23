using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Common;
using Artefacts.Domain.Measures;

namespace Artefacts.Domain.Tests;

public class UnitOfMeasureTests
{
    private static Dictionary<string, string?> Names(string en) => new() { ["en"] = en };

    [Fact]
    public void Creates_unit_with_trimmed_abbreviation_and_factor()
    {
        var unit = UnitOfMeasure.Create(Names("Millimetre"), "  mm ", 0.001m);
        Assert.Equal("mm", unit.Abbreviation);
        Assert.Equal("mm", unit.NormalizedAbbreviation);
        Assert.Equal(0.001m, unit.Factor);
        Assert.Equal("Millimetre", unit.Names.Single().Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_factor_not_greater_than_zero(int factor)
    {
        var ex = Assert.Throws<DomainValidationException>(() => UnitOfMeasure.Create(Names("Foot"), "ft", factor));
        Assert.Contains("factor", ex.Errors.Keys);
    }

    [Fact]
    public void Rejects_missing_or_long_abbreviation()
    {
        Assert.Contains("abbreviation", Assert.Throws<DomainValidationException>(() => UnitOfMeasure.Create(Names("Foot"), " ", 1m)).Errors.Keys);
        Assert.Contains("abbreviation", Assert.Throws<DomainValidationException>(() => UnitOfMeasure.Create(Names("Foot"), new string('x', 21), 1m)).Errors.Keys);
    }

    [Fact]
    public void Reports_every_invalid_part_at_once()
    {
        var ex = Assert.Throws<DomainValidationException>(() => UnitOfMeasure.Create(new Dictionary<string, string?>(), "", 0m));
        Assert.Contains("names", ex.Errors.Keys);
        Assert.Contains("abbreviation", ex.Errors.Keys);
        Assert.Contains("factor", ex.Errors.Keys);
    }
}

public class MeasurementTests
{
    private static readonly Guid Cm = Guid.NewGuid();

    [Fact]
    public void Creates_positive_measurement()
    {
        var m = Measurement.Create(17.6m, Cm);
        Assert.Equal(17.6m, m.Value);
        Assert.Equal(new UnitOfMeasureId(Cm), m.UnitId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Rejects_zero_or_negative_value(int value) =>
        Assert.Contains("value", Assert.Throws<DomainValidationException>(() => Measurement.Create(value, Cm)).Errors.Keys);

    [Fact]
    public void Rejects_missing_value_or_unit()
    {
        Assert.Contains("value", Assert.Throws<DomainValidationException>(() => Measurement.Create(null, Cm)).Errors.Keys);
        Assert.Contains("unitId", Assert.Throws<DomainValidationException>(() => Measurement.Create(7m, null)).Errors.Keys);
    }

    [Fact]
    public void Rejects_more_than_four_decimals() =>
        Assert.Contains("value", Assert.Throws<DomainValidationException>(() => Measurement.Create(1.23456m, Cm)).Errors.Keys);

    [Fact]
    public void Artefact_keeps_text_and_measurements_independently()
    {
        var artefact = Artefact.Create("CER-01", "Coppa");
        artefact.Describe(null, null, null, "diam 17,6 x h 7");
        artefact.Measure(new ArtefactMeasurements(null, Measurement.Create(7m, Cm), null, Measurement.Create(17.6m, Cm)));

        Assert.Equal("diam 17,6 x h 7", artefact.Dimensions);
        Assert.Equal(7m, artefact.Height!.Value);
        Assert.Equal(17.6m, artefact.Diameter!.Value);
        Assert.Null(artefact.Width);

        artefact.Measure(ArtefactMeasurements.None);
        Assert.Null(artefact.Height);
        Assert.Equal("diam 17,6 x h 7", artefact.Dimensions);
    }
}
