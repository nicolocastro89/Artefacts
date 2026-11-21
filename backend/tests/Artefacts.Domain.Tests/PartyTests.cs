using Artefacts.Domain.Common;
using Artefacts.Domain.Parties;

namespace Artefacts.Domain.Tests;

public class PartyTests
{
    [Fact]
    public void Name_is_trimmed_and_normalised()
    {
        var party = Party.Create("  Sotheby's   London ", PartyKind.Organisation);

        Assert.Equal("Sotheby's London", party.Name);
        Assert.Equal("sotheby's london", party.NormalizedName);
        Assert.Equal(PartyKind.Organisation, party.Kind);
    }

    [Fact]
    public void Rename_and_change_kind()
    {
        var party = Party.Create("Christies", PartyKind.Person);
        party.Rename("Christie's");
        party.ChangeKind(PartyKind.Organisation);

        Assert.Equal("Christie's", party.Name);
        Assert.Equal("christie's", party.NormalizedName);
        Assert.Equal(PartyKind.Organisation, party.Kind);
    }

    [Fact]
    public void Rejects_blank_or_too_long_names_and_unknown_kinds()
    {
        Assert.Contains("name", Assert.Throws<DomainValidationException>(() => Party.Create(" ", PartyKind.Person)).Errors.Keys);
        Assert.Contains("name", Assert.Throws<DomainValidationException>(() =>
            Party.Create(new string('x', Party.MaxNameLength + 1), PartyKind.Person)).Errors.Keys);
        Assert.Contains("kind", Assert.Throws<DomainValidationException>(() => Party.Create("X", (PartyKind)7)).Errors.Keys);
    }
}
