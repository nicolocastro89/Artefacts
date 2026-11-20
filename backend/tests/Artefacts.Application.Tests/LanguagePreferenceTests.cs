using Artefacts.Application.Languages;

namespace Artefacts.Application.Tests;

public class LanguagePreferenceTests
{
    private static readonly string[] Content = ["en", "it"];

    [Fact]
    public void Parameter_wins_over_the_browser()
    {
        Assert.Equal(["it", "en"], LanguagePreference.Resolve("it", "en-GB,en;q=0.9", Content));
    }

    [Fact]
    public void Browser_languages_are_used_in_quality_order()
    {
        Assert.Equal(["it", "en"], LanguagePreference.Resolve(null, "en;q=0.5,it-IT,it;q=0.9", Content));
    }

    [Fact]
    public void Regional_codes_match_their_primary_language_and_unknown_ones_are_dropped()
    {
        Assert.Equal(["en", "it"], LanguagePreference.Resolve("fr,en-GB", null, Content));
    }

    [Fact]
    public void Without_any_preference_the_fallback_order_applies()
    {
        Assert.Equal(["en", "it"], LanguagePreference.Resolve(null, null, Content));
        Assert.Equal(["en", "it"], LanguagePreference.Resolve(" ", "*", Content));
    }

    [Fact]
    public void A_comma_list_keeps_its_order_and_drops_repeats()
    {
        Assert.Equal(["it", "en"], LanguagePreference.Resolve("it,it-CH,en", null, Content));
    }
}
