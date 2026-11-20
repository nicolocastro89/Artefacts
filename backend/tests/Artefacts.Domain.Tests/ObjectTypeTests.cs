using Artefacts.Domain.Classification;
using Artefacts.Domain.Common;

namespace Artefacts.Domain.Tests;

public class ObjectTypeTests
{
    [Fact]
    public void Normalises_name_for_uniqueness()
    {
        var type = ObjectType.Create("  Carpets   and  Rugs ");
        Assert.Equal("Carpets and Rugs", type.Name);
        Assert.Equal("carpets and rugs", type.NormalizedName);
        Assert.Equal(type.NormalizedName, ObjectType.Normalize(" carpets AND rugs"));
    }

    [Fact]
    public void Rejects_empty_name()
    {
        var ex = Assert.Throws<DomainValidationException>(() => ObjectType.Create(" "));
        Assert.Contains("name", ex.Errors.Keys);
    }

    [Fact]
    public void Rename_updates_name_and_key()
    {
        var type = ObjectType.Create("Tappeti");
        type.Rename("Carpets");
        Assert.Equal("Carpets", type.Name);
        Assert.Equal("carpets", type.NormalizedName);
    }
}
