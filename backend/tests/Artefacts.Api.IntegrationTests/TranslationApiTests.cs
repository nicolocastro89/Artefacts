using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class TranslationApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static Dictionary<string, string?> Names(string it, string en) => new() { ["it"] = it, ["en"] = en };

    private async Task<T> GetAsync<T>(string url, string? acceptLanguage = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (acceptLanguage is not null) request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        var response = await _client.SendAsync(request);
        return await response.ReadAsync<T>();
    }

    // ---------- languages ----------

    [Fact]
    public async Task Languages_are_listed_in_fallback_order()
    {
        var languages = await (await _client.GetAsync("/api/languages")).ReadAsync<List<LanguageDto>>();

        Assert.Equal(["en", "it"], languages.Select(l => l.Code).Take(2));
    }

    [Fact]
    public async Task A_language_can_be_added_reordered_and_removed_while_unused()
    {
        var added = await (await _client.PostJsonAsync("/api/languages", new AddLanguageRequest("FR", "French"))).ReadAsync<LanguageDto>();
        try
        {
            Assert.Equal("fr", added.Code);
            var all = await (await _client.GetAsync("/api/languages")).ReadAsync<List<LanguageDto>>();
            Assert.Equal("fr", all.Last().Code);

            var reordered = await (await _client.PutJsonAsync("/api/languages/order",
                new SetLanguageOrderRequest(["it", "en", "fr"]))).ReadAsync<List<LanguageDto>>();
            Assert.Equal(["it", "en", "fr"], reordered.Select(l => l.Code));

            // With Italian first in the fallback order, a reader asking for French gets the Italian name.
            var region = await _client.AddRegionAsync(Names(HttpExtensions.Unique("Turchia"), HttpExtensions.Unique("Turkey")));
            var read = await GetAsync<RegionDto>($"/api/regions/{region.Id}?lang=fr");
            Assert.Equal("it", read.NameLanguage);
        }
        finally
        {
            await _client.PutJsonAsync("/api/languages/order", new SetLanguageOrderRequest(["en", "it", "fr"]));
            await _client.DeleteAsync("/api/languages/fr");
        }

        var after = await (await _client.GetAsync("/api/languages")).ReadAsync<List<LanguageDto>>();
        Assert.Equal(["en", "it"], after.Select(l => l.Code));
    }

    [Fact]
    public async Task A_language_in_use_cannot_be_removed()
    {
        await _client.AddRegionAsync(Names(HttpExtensions.Unique("Fiandre"), HttpExtensions.Unique("Flanders")));

        var response = await _client.DeleteAsync("/api/languages/it");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("language_in_use", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_invalid_language_code_is_rejected()
    {
        var response = await _client.PostJsonAsync("/api/languages", new AddLanguageRequest("italian", "Italian"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("code", (await response.ValidationErrorsAsync()).Keys);
    }

    // ---------- request language ----------

    [Fact]
    public async Task The_lang_parameter_wins_over_the_browser_language()
    {
        var region = await _client.AddRegionAsync(Names(HttpExtensions.Unique("Turchia"), HttpExtensions.Unique("Turkey")));

        var read = await GetAsync<RegionDto>($"/api/regions/{region.Id}?lang=it", acceptLanguage: "en-GB,en;q=0.9");

        Assert.Equal("it", read.NameLanguage);
        Assert.StartsWith("Turchia", read.Name);
    }

    [Fact]
    public async Task The_browser_language_is_used_without_a_parameter()
    {
        var region = await _client.AddRegionAsync(Names(HttpExtensions.Unique("Cina"), HttpExtensions.Unique("China")));

        var italian = await GetAsync<RegionDto>($"/api/regions/{region.Id}", acceptLanguage: "it-IT,it;q=0.9,en;q=0.8");
        var english = await GetAsync<RegionDto>($"/api/regions/{region.Id}", acceptLanguage: "en-GB,en;q=0.9,it;q=0.8");

        Assert.Equal("it", italian.NameLanguage);
        Assert.Equal("en", english.NameLanguage);
        Assert.Equal(new Dictionary<string, string> { ["en"] = english.Name, ["it"] = italian.Name }, english.Names);
    }

    [Fact]
    public async Task Responses_vary_by_accept_language()
    {
        var response = await _client.GetAsync("/api/languages");

        Assert.Contains("Accept-Language", response.Headers.Vary);
    }

    // ---------- names ----------

    [Fact]
    public async Task Region_search_matches_a_name_in_any_language()
    {
        var italian = HttpExtensions.Unique("Turchia");
        var region = await _client.AddRegionAsync(Names(italian, HttpExtensions.Unique("Turkey")));

        var found = await GetAsync<List<RegionDto>>($"/api/regions?search={italian.ToLowerInvariant()}&lang=en");

        var hit = Assert.Single(found);
        Assert.Equal(region.Id, hit.Id);
        Assert.Equal("en", hit.NameLanguage);
    }

    [Fact]
    public async Task The_same_text_is_allowed_in_different_languages()
    {
        var text = HttpExtensions.Unique("Vienna");
        await _client.AddRegionAsync(new Dictionary<string, string?> { ["en"] = text });

        var response = await _client.PostJsonAsync("/api/regions", new AddRegionRequest(new Dictionary<string, string?> { ["it"] = text }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Adding_an_existing_name_in_another_language_creates_nothing()
    {
        var italian = HttpExtensions.Unique("Tappeti");
        var existing = await _client.AddObjectTypeAsync(Names(italian, HttpExtensions.Unique("Carpets")));

        var response = await _client.PostJsonAsync("/api/object-types",
            new AddObjectTypeRequest(new Dictionary<string, string?> { ["it"] = $" {italian.ToUpperInvariant()} " }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(existing.Id, (await response.ReadAsync<ObjectTypeDto>()).Id);
    }

    [Fact]
    public async Task Unknown_language_and_missing_names_are_validation_errors()
    {
        var unknown = await _client.PostJsonAsync("/api/regions", new AddRegionRequest(new Dictionary<string, string?> { ["xx"] = "Somewhere" }));
        var none = await _client.PostJsonAsync("/api/regions", new AddRegionRequest(new Dictionary<string, string?> { ["en"] = " " }));

        Assert.Contains("names.xx", (await unknown.ValidationErrorsAsync()).Keys);
        Assert.Contains("names", (await none.ValidationErrorsAsync()).Keys);
    }

    // ---------- artefacts ----------

    [Fact]
    public async Task Description_resolves_in_the_requested_language_and_reports_fallback()
    {
        var both = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Dish",
            Descriptions: new Dictionary<string, string?> { ["it"] = "Piatto con fenice", ["en"] = "Dish with a phoenix" }));
        var italianOnly = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Bowl",
            Descriptions: new Dictionary<string, string?> { ["it"] = "Coppa celadon" }));

        var it = await GetAsync<ArtefactDetail>($"/api/artefacts/{both.Id}?lang=it");
        var en = await GetAsync<ArtefactDetail>($"/api/artefacts/{both.Id}?lang=en");
        var fallback = await GetAsync<ArtefactDetail>($"/api/artefacts/{italianOnly.Id}?lang=en");

        Assert.Equal(("Piatto con fenice", "it", "it"), (it.Description, it.DescriptionLanguage, it.Language));
        Assert.Equal(("Dish with a phoenix", "en"), (en.Description, en.DescriptionLanguage));
        Assert.Equal(("Coppa celadon", "it", "en"), (fallback.Description, fallback.DescriptionLanguage, fallback.Language));
        Assert.Equal(2, en.Descriptions.Count);
    }

    [Fact]
    public async Task Unknown_description_language_is_a_validation_error()
    {
        var response = await _client.PostJsonAsync("/api/artefacts", new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "T",
            Descriptions: new Dictionary<string, string?> { ["xx"] = "?" }));

        Assert.Contains("descriptions.xx", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Provenance_and_object_type_are_named_in_the_readers_language()
    {
        var region = await _client.AddRegionAsync(Names(HttpExtensions.Unique("Turchia"), HttpExtensions.Unique("Turkey")));
        var type = await _client.AddObjectTypeAsync(Names(HttpExtensions.Unique("Tappeti"), HttpExtensions.Unique("Carpets")));
        var artefact = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Rug",
            Provenance: [new ProvenanceEntryInput(region.Id, new HistoricalDateInput("18th century"), null)],
            ObjectTypeId: type.Id,
            PlaceOfProduction: new PlaceOfProductionInput("Anatolia", region.Id)));

        var read = await GetAsync<ArtefactDetail>($"/api/artefacts/{artefact.Id}?lang=it");

        Assert.Equal(region.Names["it"], read.Provenance.Single().RegionName);
        Assert.Equal(type.Names["it"], read.ObjectType!.Name);
        Assert.Equal(region.Names["it"], read.PlaceOfProduction!.Region!.Name);
    }

    [Fact]
    public async Task Grouping_by_object_type_labels_and_orders_in_the_readers_language()
    {
        var token = HttpExtensions.Unique("tr");
        // English and Italian names sort in opposite orders.
        var first = await _client.AddObjectTypeAsync(Names($"Zeta {token}", $"Alpha {token}"));
        var second = await _client.AddObjectTypeAsync(Names($"Alfa {token}", $"Omega {token}"));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"One {token}", ObjectTypeId: first.Id));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"Two {token}", ObjectTypeId: second.Id));

        var en = await _client.ListArtefactsAsync(search: token, group: "objectType", lang: "en");
        var it = await _client.ListArtefactsAsync(search: token, group: "objectType", lang: "it");

        Assert.Equal([$"Alpha {token}", $"Omega {token}"], en.Groups.Select(g => g.Label));
        Assert.Equal([$"Alfa {token}", $"Zeta {token}"], it.Groups.Select(g => g.Label));
        Assert.Equal(["Two", "One"], it.Items.Select(i => i.Title.Split(' ')[0]));
        Assert.Equal($"Alfa {token}", it.Items[0].ObjectType);
    }
}
