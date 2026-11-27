using System.Net;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Application.Imports;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ImportLookupApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string Url(string id) => $"/api/imports/lookup-tables/{id}";

    private static SaveImportLookupTableRequest Save(params (string Word, string Value)[] entries) =>
        new(entries.Select(e => new ImportLookupEntryInput(e.Word, e.Value)).ToList());

    [Fact]
    public async Task Every_built_in_table_is_listed_with_its_entries()
    {
        var list = await (await _client.GetAsync("/api/imports/lookup-tables")).ReadAsync<List<ImportLookupTableDto>>();

        Assert.Equal(
            [LookupTableIds.Months, LookupTableIds.CurrenciesBeforeAmount, LookupTableIds.CurrenciesAfterAmount, LookupTableIds.MeasureLabels, LookupTableIds.InchAliases],
            list.Select(t => t.Id));
        Assert.Contains(list.Single(t => t.Id == LookupTableIds.Months).Entries, e => e.Word == "gennaio" && e.Value == "1" && !e.IsUser);
    }

    [Fact]
    public async Task User_entries_extend_and_override_a_table_and_saving_none_restores_it()
    {
        var saved = await (await _client.PutJsonAsync(Url(LookupTableIds.Months), Save(("mai", "5"), ("marzo", "4")))).ReadAsync<ImportLookupTableDto>();

        Assert.Contains(saved.Entries, e => e.Word == "mai" && e.Value == "5" && e.IsUser && e.BuiltInValue is null);
        Assert.Contains(saved.Entries, e => e.Word == "marzo" && e.Value == "4" && e.IsUser && e.BuiltInValue == "3");
        Assert.Contains(saved.Entries, e => e.Word == "aprile" && !e.IsUser);

        var read = await (await _client.GetAsync(Url(LookupTableIds.Months))).ReadAsync<ImportLookupTableDto>();
        Assert.Equal(2, read.Entries.Count(e => e.IsUser));

        var restored = await (await _client.PutJsonAsync(Url(LookupTableIds.Months), Save())).ReadAsync<ImportLookupTableDto>();
        Assert.DoesNotContain(restored.Entries, e => e.IsUser);
        Assert.Contains(restored.Entries, e => e.Word == "marzo" && e.Value == "3");
    }

    [Fact]
    public async Task Values_that_do_not_fit_the_table_and_repeated_words_are_reported_by_entry()
    {
        var response = await _client.PutJsonAsync(Url(LookupTableIds.Months), Save(("mai", "13"), ("x", "1"), ("X", "2")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var keys = (await response.ValidationErrorsAsync()).Keys;
        Assert.Contains("entries[0].value", keys);
        Assert.Contains("entries[2].word", keys);
        Assert.DoesNotContain("entries[1].word", keys);

        var currency = await _client.PutJsonAsync(Url(LookupTableIds.CurrenciesAfterAmount), Save(("kr", "sek")));
        Assert.Contains("entries[0].value", (await currency.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task An_unknown_table_is_not_found()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(Url("nope"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutJsonAsync(Url("nope"), Save(("a", "b")))).StatusCode);
    }
}
