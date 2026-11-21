using Artefacts.Domain.Common;
using Artefacts.Domain.Lists;

namespace Artefacts.Domain.Tests;

public class ReferenceEntryTests
{
    [Fact]
    public void Translated_entry_has_names_per_language()
    {
        var entry = ReferenceEntry.Create(ListKind.Dynasty, new Dictionary<string, string?> { ["it"] = "  Safavide ", ["en"] = "Safavid" });

        Assert.Equal("Safavid", entry.NameIn(["en"])!.Value.Text);
        Assert.Equal("Safavide", entry.NameIn(["it"])!.Value.Text);
        Assert.Null(entry.Name);
        Assert.All(entry.Names, n => Assert.Equal(ListKind.Dynasty, n.Kind));
    }

    [Fact]
    public void Translated_names_are_normalised()
    {
        var entry = ReferenceEntry.Create(ListKind.CollectionGroup, new Dictionary<string, string?> { ["it"] = "  Tessuti   Antichi " });

        Assert.Equal("Tessuti Antichi", entry.Names.Single().Name);
        Assert.Equal("tessuti antichi", entry.Names.Single().NormalizedName);
        Assert.True(entry.HasName("tessuti antichi"));
    }

    [Fact]
    public void Storage_location_has_one_name_in_every_language()
    {
        var entry = ReferenceEntry.Create(ListKind.StorageLocation, "  MI   F3 ");

        Assert.Equal("MI F3", entry.Name);
        Assert.Equal("mi f3", entry.NormalizedName);
        Assert.Empty(entry.Names);
        Assert.Equal("MI F3", entry.NameIn(["it"])!.Value.Text);
        Assert.Equal("MI F3", entry.NameIn(["en"])!.Value.Text);
    }

    [Fact]
    public void Storage_location_rejects_names_per_language()
    {
        var entry = ReferenceEntry.Create(ListKind.StorageLocation, "Beta2");
        var ex = Assert.Throws<DomainValidationException>(() => entry.SetNames(new Dictionary<string, string?> { ["it"] = "Beta2" }));
        Assert.Contains("names", ex.Errors.Keys);
        Assert.Throws<DomainValidationException>(() => ReferenceEntry.Create(ListKind.StorageLocation, new Dictionary<string, string?> { ["en"] = "x" }));
    }

    [Fact]
    public void Translated_entry_rejects_a_single_name()
    {
        var ex = Assert.Throws<DomainValidationException>(() => ReferenceEntry.Create(ListKind.Dynasty, "Ottomana"));
        Assert.Contains("name", ex.Errors.Keys);
    }

    [Fact]
    public void Blank_names_are_rejected()
    {
        Assert.Contains("names", Assert.Throws<DomainValidationException>(() =>
            ReferenceEntry.Create(ListKind.Dynasty, new Dictionary<string, string?> { ["it"] = "  " })).Errors.Keys);
        Assert.Contains("name", Assert.Throws<DomainValidationException>(() =>
            ReferenceEntry.Create(ListKind.StorageLocation, "  ")).Errors.Keys);
    }

    [Fact]
    public void Too_long_names_are_rejected()
    {
        var tooLong = new string('x', ReferenceEntry.MaxNameLength + 1);
        Assert.Contains("names.it", Assert.Throws<DomainValidationException>(() =>
            ReferenceEntry.Create(ListKind.Dynasty, new Dictionary<string, string?> { ["it"] = tooLong })).Errors.Keys);
        Assert.Contains("name", Assert.Throws<DomainValidationException>(() =>
            ReferenceEntry.Create(ListKind.StorageLocation, tooLong)).Errors.Keys);
    }
}
