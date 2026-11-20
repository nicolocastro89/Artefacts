using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;

namespace Artefacts.Domain.Tests;

public class LanguageTests
{
    [Theory]
    [InlineData("IT", "it")]
    [InlineData(" pt_BR ", "pt-br")]
    [InlineData("en-gb", "en-gb")]
    public void Codes_are_normalised(string input, string expected) =>
        Assert.Equal(expected, Language.Create(input, "X", 1).Code);

    [Theory]
    [InlineData("italian")]
    [InlineData("i")]
    [InlineData("")]
    public void Invalid_codes_are_rejected(string input)
    {
        var ex = Assert.Throws<DomainValidationException>(() => Language.Create(input, "X", 1));
        Assert.Contains("code", ex.Errors.Keys);
    }

    [Fact]
    public void Resolve_prefers_the_requested_language()
    {
        Assert.Equal(new ResolvedText("Turchia", "it"), Localized.Resolve([("en", "Turkey"), ("it", "Turchia")], ["it", "en"]));
    }

    [Fact]
    public void Resolve_falls_back_in_order_and_reports_the_language()
    {
        Assert.Equal(new ResolvedText("Piatto", "it"), Localized.Resolve([("it", "Piatto")], ["en", "it"]));
    }

    [Fact]
    public void Resolve_returns_nothing_without_values()
    {
        Assert.Null(Localized.Resolve([("en", "  ")], ["en"]));
    }

    [Fact]
    public void Descriptions_are_kept_per_language_and_blank_ones_dropped()
    {
        var a = Artefact.Create("INV-1", "Dish");
        a.ReplaceDescriptions(new Dictionary<string, string?> { ["EN"] = " Dish with a phoenix\nand peonies ", ["it"] = " " });

        Assert.Equal([("en", "Dish with a phoenix\nand peonies")], a.Translations.Select(t => (t.Language, t.Description)));
        Assert.Equal(new ResolvedText("Dish with a phoenix\nand peonies", "en"), a.DescriptionIn(["it", "en"]));
    }

    [Fact]
    public void Overlong_description_is_keyed_by_language()
    {
        var a = Artefact.Create("INV-1", "Dish");
        var ex = Assert.Throws<DomainValidationException>(() =>
            a.ReplaceDescriptions(new Dictionary<string, string?> { ["it"] = new string('x', Artefact.MaxDescriptionLength + 1) }));
        Assert.Contains("descriptions.it", ex.Errors.Keys);
    }
}
