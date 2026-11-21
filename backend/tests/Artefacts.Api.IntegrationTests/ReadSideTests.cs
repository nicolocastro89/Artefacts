using System.Text.Json;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Infrastructure.Read;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ReadSideTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task List_is_paginated_with_total()
    {
        var token = HttpExtensions.Unique("page");
        for (var i = 0; i < 30; i++)
            await _client.CreateArtefactAsync(new ArtefactWriteRequest($"{token}-{i:00}", $"Item {i}"));

        var page2 = await (await _client.GetAsync($"/api/artefacts?search={token}&page=2&pageSize=25"))
            .ReadAsync<PagedResult<ArtefactListItem>>();

        Assert.Equal(30, page2.Total);
        Assert.Equal(5, page2.Items.Count);
        Assert.Equal(2, page2.Page);
    }

    [Fact]
    public async Task Search_matches_title_maker_or_inventory_number_ignoring_case()
    {
        var token = HttpExtensions.Unique("lemons");
        var byTitle = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), $"Still Life with {token.ToUpperInvariant()}"));
        var byMaker = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Other", Maker: $"Workshop of {token}"));
        var byInv = await _client.CreateArtefactAsync(new ArtefactWriteRequest($"{token}-INV", "Third"));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Unrelated"));

        var result = await (await _client.GetAsync($"/api/artefacts?search={token}")).ReadAsync<PagedResult<ArtefactListItem>>();

        Assert.Equal(3, result.Total);
        Assert.Equivalent(new[] { byTitle.Id, byMaker.Id, byInv.Id }, result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Sorting_by_creation_date_uses_range_and_puts_undated_last()
    {
        var token = HttpExtensions.Unique("sort");
        await _client.CreateArtefactAsync(new ArtefactWriteRequest($"{token}-a", "Undated"));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest($"{token}-b", "Late", CreationDate: new HistoricalDateInput("1780s")));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest($"{token}-c", "Early", CreationDate: new HistoricalDateInput("17th century")));
        await _client.CreateArtefactAsync(new ArtefactWriteRequest($"{token}-d", "Before", CreationDate: new HistoricalDateInput("before 1700")));

        var asc = await (await _client.GetAsync($"/api/artefacts?search={token}&sort=creationDate&direction=asc"))
            .ReadAsync<PagedResult<ArtefactListItem>>();
        var desc = await (await _client.GetAsync($"/api/artefacts?search={token}&sort=creationDate&direction=desc"))
            .ReadAsync<PagedResult<ArtefactListItem>>();

        Assert.Equal(["Early", "Before", "Late", "Undated"], asc.Items.Select(i => i.Title));
        Assert.Equal(["Late", "Before", "Early", "Undated"], desc.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task Unknown_sort_value_is_a_validation_error_on_the_field()
    {
        var response = await _client.GetAsync("/api/artefacts?sort=colour&direction=sideways");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.ValidationErrorsAsync();
        Assert.Contains("sort", errors.Keys);
        Assert.Contains("direction", errors.Keys);
    }

    [Fact]
    public async Task List_includes_primary_image_and_count()
    {
        var token = HttpExtensions.Unique("img");
        var artefact = await _client.CreateArtefactAsync(new ArtefactWriteRequest(token, "Pictured"));
        var noImage = await _client.CreateArtefactAsync(new ArtefactWriteRequest($"{token}-none", "Plain"));
        await _client.UploadAsync(artefact.Id, ("a.jpg", TestData.Jpeg()), ("b.jpg", TestData.Jpeg()));

        var result = await (await _client.GetAsync($"/api/artefacts?search={token}")).ReadAsync<PagedResult<ArtefactListItem>>();

        var withImage = result.Items.Single(i => i.Id == artefact.Id);
        Assert.Equal(2, withImage.ImageCount);
        Assert.Contains("/thumb/plain/", withImage.PrimaryImage!.Thumb);
        var plain = result.Items.Single(i => i.Id == noImage.Id);
        Assert.Null(plain.PrimaryImage);
        Assert.Equal(0, plain.ImageCount);
    }

    [Fact]
    public async Task Field_catalog_describes_artefact_fields_and_sensitivity()
    {
        var catalog = await (await _client.GetAsync("/api/catalog")).ReadAsync<FieldCatalogDto>();

        var artefact = Assert.Single(catalog.Entities, e => e.Id == "Artefact");
        var title = Assert.Single(artefact.Fields, f => f.Id == "title");
        Assert.Equal(FieldType.Text, title.Type);
        Assert.Null(title.Sensitivity);
        Assert.Equal(FieldType.Image, Assert.Single(artefact.Fields, f => f.Id == "primaryImage").Type);

        var price = Assert.Single(artefact.Fields, f => f.Id == "acquisitionPrice");
        Assert.Equal(FieldType.Money, price.Type);
        Assert.Equal("financial", price.Sensitivity);
        Assert.Equal(["acquisition_price_amount", "acquisition_price_currency"], price.Columns);
    }

    [Fact]
    public async Task Field_catalog_describes_the_list_views()
    {
        var catalog = await (await _client.GetAsync("/api/catalog")).ReadAsync<FieldCatalogDto>();

        var views = catalog.Entities.ToDictionary(e => e.Id, e => e.View);
        Assert.Equal("v_artefact_provenance", views["ProvenanceEntry"]);
        Assert.Equal("v_artefact_image", views["ArtefactImage"]);
        Assert.Equal("v_artefact_exhibition", views["Exhibition"]);
        Assert.Equal("v_artefact_description", views["Description"]);
        var provenance = catalog.Entities.Single(e => e.Id == "ProvenanceEntry");
        Assert.Equal(FieldType.HistoricalDate, provenance.Fields.Single(f => f.Id == "period").Type);
        Assert.Equal(["period_display", "period_earliest_year", "period_latest_year", "period_precision"],
            provenance.Fields.Single(f => f.Id == "period").Columns);
        Assert.Equal(FieldType.Image, catalog.Entities.Single(e => e.Id == "ArtefactImage").Fields.Single(f => f.Id == "image").Type);
    }

    [Fact]
    public async Task Provenance_view_follows_the_entered_order()
    {
        var first = await _client.AddRegionAsync(HttpExtensions.Unique("Flanders"));
        var second = await _client.AddRegionAsync(HttpExtensions.Unique("Paris"));
        var artefact = await _client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Still life",
            Provenance: [new ProvenanceEntryInput(first.Id, new HistoricalDateInput("c. 1650"), null), new ProvenanceEntryInput(second.Id, new HistoricalDateInput("1780s"), null)]));
        await _client.PutJsonAsync($"/api/artefacts/{artefact.Id}", new ArtefactWriteRequest(artefact.InventoryNumber, artefact.Title,
            Provenance: [new ProvenanceEntryInput(second.Id, new HistoricalDateInput("1780s"), null), new ProvenanceEntryInput(first.Id, new HistoricalDateInput("c. 1650"), null)]));

        using var scope = factory.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<ReadDbContext>().Provenance
            .Where(p => p.ArtefactId == artefact.Id).OrderBy(p => p.Position).ToListAsync();

        Assert.Equal([second.Id, first.Id], rows.Select(r => r.RegionId));
        Assert.Equal("1780s", rows[0].PeriodDisplay);
    }

    [Fact]
    public async Task Read_model_mapping_matches_view_columns()
    {
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<ReadDbContext>().Model;
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();

        foreach (var entity in model.GetEntityTypes())
        {
            var view = entity.GetViewName()!;
            var store = StoreObjectIdentifier.View(view);
            var mapped = entity.GetProperties().Select(p => p.GetColumnName(store)!).ToHashSet();

            await using var cmd = new NpgsqlCommand(
                "SELECT column_name FROM information_schema.columns WHERE table_name = @view", connection);
            cmd.Parameters.AddWithValue("view", view);
            var actual = new HashSet<string>();
            await using (var reader = await cmd.ExecuteReaderAsync())
                while (await reader.ReadAsync()) actual.Add(reader.GetString(0));

            Assert.NotEmpty(actual);
            Assert.Equal(actual.Order(), mapped.Order());
        }
    }

    [Fact]
    public async Task OpenApi_document_describes_artefact_detail()
    {
        var doc = await (await _client.GetAsync("/openapi/v1.json")).ReadAsync<JsonElement>();

        var get = doc.GetProperty("paths").GetProperty("/api/artefacts/{id}").GetProperty("get");
        Assert.Equal("getArtefact", get.GetProperty("operationId").GetString());
        var schemas = doc.GetProperty("components").GetProperty("schemas");
        var detail = schemas.GetProperty("ArtefactDetail").GetProperty("properties");
        Assert.True(detail.TryGetProperty("title", out _));
        var urls = schemas.GetProperty("ImageUrls").GetProperty("properties");
        Assert.True(urls.TryGetProperty("thumb", out _) && urls.TryGetProperty("web", out _) && urls.TryGetProperty("print", out _));
    }
}
