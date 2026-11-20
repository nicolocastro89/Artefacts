using Artefacts.Domain.Common;
using Artefacts.Domain.Regions;

namespace Artefacts.Domain.Tests;

public class RegionTests
{
    [Fact]
    public void Normalises_name_for_uniqueness()
    {
        var region = Region.Create("  Flanders  ");
        Assert.Equal("Flanders", region.Name);
        Assert.Equal("flanders", region.NormalizedName);
        Assert.Equal(region.NormalizedName, Region.Normalize(" flanders "));
    }

    [Fact]
    public void Rejects_empty_name()
    {
        var ex = Assert.Throws<DomainValidationException>(() => Region.Create("  "));
        Assert.Contains("name", ex.Errors.Keys);
    }

    [Fact]
    public void Rename_updates_normalized_name()
    {
        var region = Region.Create("Holland");
        region.Rename("Netherlands");
        Assert.Equal("netherlands", region.NormalizedName);
    }

    [Fact]
    public void Region_cannot_be_its_own_parent()
    {
        var region = Region.Create("Flanders");
        Assert.Throws<DomainValidationException>(() => region.SetParent(region.Id, _ => null));
    }

    [Fact]
    public void Region_cannot_become_its_own_ancestor()
    {
        var belgium = Region.Create("Belgium");
        var flanders = Region.Create("Flanders");
        flanders.SetParent(belgium.Id, _ => null);
        var parents = new Dictionary<RegionId, RegionId?> { [flanders.Id] = belgium.Id, [belgium.Id] = null };

        var ex = Assert.Throws<DomainValidationException>(() => belgium.SetParent(flanders.Id, id => parents[id]));
        Assert.Contains("parentId", ex.Errors.Keys);
    }

    [Fact]
    public void Valid_parent_is_set_and_can_be_cleared()
    {
        var belgium = Region.Create("Belgium");
        var flanders = Region.Create("Flanders");
        flanders.SetParent(belgium.Id, _ => null);
        Assert.Equal(belgium.Id, flanders.ParentId);
        flanders.SetParent(null, _ => null);
        Assert.Null(flanders.ParentId);
    }
}
