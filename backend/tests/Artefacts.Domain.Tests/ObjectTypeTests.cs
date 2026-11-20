using Artefacts.Domain.Classification;
using Artefacts.Domain.Common;

namespace Artefacts.Domain.Tests;

public class ObjectTypeTests
{
    [Fact]
    public void Normalises_names_per_language()
    {
        var type = ObjectType.Create(new Dictionary<string, string?> { ["en"] = "  Carpets   and  Rugs ", ["it"] = "Tappeti" });

        Assert.Equal([("en", "carpets and rugs"), ("it", "tappeti")], type.Names.Select(n => (n.Language, n.NormalizedName)));
    }

    [Fact]
    public void Rejects_an_object_type_without_any_name()
    {
        var ex = Assert.Throws<DomainValidationException>(() => ObjectType.Create(new Dictionary<string, string?> { ["en"] = " " }));
        Assert.Contains("names", ex.Errors.Keys);
    }

    [Fact]
    public void Too_long_name_is_keyed_by_language()
    {
        var ex = Assert.Throws<DomainValidationException>(() =>
            ObjectType.Create(new Dictionary<string, string?> { ["it"] = new string('x', ObjectType.MaxNameLength + 1) }));
        Assert.Contains("names.it", ex.Errors.Keys);
    }

    [Fact]
    public void Setting_names_replaces_them()
    {
        var type = ObjectType.Create(new Dictionary<string, string?> { ["it"] = "Tappeti" });
        type.SetNames(new Dictionary<string, string?> { ["it"] = "Tappeti e kilim", ["en"] = "Carpets" });

        Assert.Equal("Tappeti e kilim", type.NameIn(["it"])!.Value.Text);
        Assert.Equal("Carpets", type.NameIn(["en"])!.Value.Text);
    }
}
