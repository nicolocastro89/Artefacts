using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;
using Artefacts.Domain.Regions;

namespace Artefacts.Domain.Tests;

public class RegionTests
{
    private static Dictionary<string, string?> En(string name) => new() { ["en"] = name };

    [Fact]
    public void Normalises_names_for_uniqueness_per_language()
    {
        var region = Region.Create(new Dictionary<string, string?> { ["EN"] = "  Flanders  ", ["it"] = " Fiandre " });

        Assert.Equal([("en", "Flanders", "flanders"), ("it", "Fiandre", "fiandre")],
            region.Names.Select(n => (n.Language, n.Name, n.NormalizedName)));
        Assert.Equal("flanders", LocalizedName.Normalize(" FLANDERS "));
    }

    [Fact]
    public void Rejects_a_region_without_any_name()
    {
        var ex = Assert.Throws<DomainValidationException>(() => Region.Create(new Dictionary<string, string?> { ["en"] = "  ", ["it"] = null }));
        Assert.Contains("names", ex.Errors.Keys);
    }

    [Fact]
    public void Setting_names_replaces_all_of_them()
    {
        var region = Region.Create(new Dictionary<string, string?> { ["en"] = "Holland", ["it"] = "Olanda" });
        region.SetNames(new Dictionary<string, string?> { ["en"] = "Netherlands" });

        Assert.Equal(["Netherlands"], region.Names.Select(n => n.Name));
    }

    [Fact]
    public void Name_resolves_in_the_preferred_language_then_falls_back()
    {
        var region = Region.Create(new Dictionary<string, string?> { ["it"] = "Turchia", ["en"] = "Turkey" });

        Assert.Equal(new ResolvedText("Turchia", "it"), region.NameIn(["it", "en"]));
        Assert.Equal(new ResolvedText("Turkey", "en"), region.NameIn(["fr", "en", "it"]));
    }

    [Fact]
    public void Region_cannot_be_its_own_parent()
    {
        var region = Region.Create(En("Flanders"));
        Assert.Throws<DomainValidationException>(() => region.SetParent(region.Id, _ => null));
    }

    [Fact]
    public void Region_cannot_become_its_own_ancestor()
    {
        var belgium = Region.Create(En("Belgium"));
        var flanders = Region.Create(En("Flanders"));
        flanders.SetParent(belgium.Id, _ => null);
        var parents = new Dictionary<RegionId, RegionId?> { [flanders.Id] = belgium.Id, [belgium.Id] = null };

        var ex = Assert.Throws<DomainValidationException>(() => belgium.SetParent(flanders.Id, id => parents[id]));
        Assert.Contains("parentId", ex.Errors.Keys);
    }

    [Fact]
    public void Valid_parent_is_set_and_can_be_cleared()
    {
        var belgium = Region.Create(En("Belgium"));
        var flanders = Region.Create(En("Flanders"));
        flanders.SetParent(belgium.Id, _ => null);
        Assert.Equal(belgium.Id, flanders.ParentId);
        flanders.SetParent(null, _ => null);
        Assert.Null(flanders.ParentId);
    }
}
