using System.Net;
using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Contracts;
using Artefacts.Application.Imports;
using Artefacts.Domain.Imports;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ImportApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> ObjectTypeAsync() => (await _client.AddObjectTypeAsync(HttpExtensions.Unique("Ceramics"))).Id;

    private string StagingDirectory(Guid jobId) => Path.Combine(factory.MediaRoot, "imports", jobId.ToString("N"));

    // ---------- upload and reading ----------

    [Fact]
    public async Task Workbook_is_accepted_and_read_into_staged_rows()
    {
        var workbook = ImportTestData.Ceramics(
                ("01. Coppa Celadon\ndiam 17,6 x h 7", "Cina\nfine XII inizio XIII\nSung", null),
                ("02. Piatto Celadon", "Cina\nXIV\nYuan", "Sotheby's London\n12 06 1990\nlot 170 £ 7.865"))
            .Picture(3, 3, TestData.Jpeg(800))
            .Build();

        var response = await _client.PostAsync("/api/imports", ImportTestData.ImportForm(workbook, await ObjectTypeAsync()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<ImportJobDto>();
        var job = await _client.WaitForImportAsync(created.Id, j => j.State != ImportJobState.Reading);
        Assert.Equal(ImportJobState.AwaitingMapping, job.State);
        Assert.Equal("CERAMICHE", job.SheetName);
        Assert.Equal(2, job.TotalRows);
        Assert.Equal(1, job.GroupHeadings);
        Assert.Equal("{sheet}-{group:2}-{item:2}", job.Mapping!.InventoryNumber.Pattern);

        var columns = await (await _client.GetAsync($"/api/imports/{job.Id}/columns")).ReadAsync<ImportColumnsDto>();
        Assert.Contains(columns.Columns, c => c.Heading == "Nome File" && c.Samples.Contains("02. Piatto Celadon"));
        Assert.True(columns.Columns.Single(c => c.Column == 2).HitRates[DetectorIds.ItemTitle] > 0);
        Assert.True(File.Exists(Path.Combine(StagingDirectory(job.Id), "pictures", "1.jpg")));
    }

    [Theory]
    [InlineData("inventario.csv")]
    [InlineData("inventario.xls")]
    public async Task Other_file_types_are_rejected_naming_the_file(string fileName)
    {
        var response = await _client.PostAsync("/api/imports", ImportTestData.ImportForm("a;b\n1;2"u8.ToArray(), await ObjectTypeAsync(), fileName));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(fileName, (await response.ValidationErrorsAsync())["file"][0]);
    }

    [Fact]
    public async Task Object_type_is_required()
    {
        var response = await _client.PostAsync("/api/imports", ImportTestData.ImportForm(ImportTestData.Ceramics().Build(), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("objectTypeId", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Unreadable_workbook_fails_with_the_reason()
    {
        // A zip package that is not a workbook passes the upload check but cannot be read.
        using var zip = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zip, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = new StreamWriter(archive.CreateEntry("hello.txt").Open());
            entry.Write("not a workbook");
        }

        var job = await _client.StartImportAsync(zip.ToArray(), await ObjectTypeAsync());

        Assert.Equal(ImportJobState.Failed, job.State);
        Assert.Contains("workbook", job.Error);
    }

    // ---------- mapping, places, dates, review ----------

    [Fact]
    public async Task Mapping_saved_as_a_template_is_reused_for_another_workbook()
    {
        var type = await ObjectTypeAsync();
        var first = await _client.StartImportAsync(ImportTestData.Ceramics(("01. Coppa", "Cina\nXV", null)).Build(), type);
        var (mapped, prefix) = await _client.UseUniqueNumbersAsync(first);
        await _client.PutJsonAsync($"/api/imports/{first.Id}/places",
            new SetPlaceDecisionsRequest([new PlaceDecision("Cina", PlaceAction.Keep)]));
        var name = HttpExtensions.Unique("Collection workbook 2024");
        var template = await (await _client.PostJsonAsync("/api/import-templates", new SaveImportTemplateRequest(name, first.Id))).ReadAsync<ImportTemplateDto>();

        var second = await _client.StartImportAsync(ImportTestData.Ceramics(("01. Tappeto", "Cina\nXVI", null)).Build(), type, "tessuti.xlsx", template.Id);

        Assert.Equal(template.Id, second.Template!.Id);
        Assert.Equal(prefix + "-{group:2}-{item:2}", second.Mapping!.InventoryNumber.Pattern);
        Assert.Equal(mapped.Mapping!.Columns.Count, second.Mapping.Columns.Count);
        Assert.Equal(PlaceAction.Keep, Assert.Single(second.Decisions.Places).Action);
        Assert.Contains(await (await _client.GetAsync("/api/import-templates")).ReadAsync<List<ImportTemplateDto>>(), t => t.Name == name);
    }

    [Fact]
    public async Task Places_are_matched_exactly_by_name_in_any_language_or_suggested_by_similarity()
    {
        var turkey = await _client.AddRegionAsync(new Dictionary<string, string?> { ["en"] = "Turkey", ["it"] = "Turchia" });
        var china = await _client.AddRegionAsync("China");
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(
            ("01. Coppa", "Cina\nXV", null), ("02. Piatto", "Turchia\nXVI", null), ("03. Vaso", "Cina\nXVI", null)).Build(), await ObjectTypeAsync());

        var places = await (await _client.GetAsync($"/api/imports/{job.Id}/places")).ReadAsync<List<ImportPlaceDto>>();

        var turchia = places.Single(p => p.Text == "Turchia");
        Assert.True(turchia.Suggestions[0].Exact);
        Assert.Equal(turkey.Id, turchia.Suggestions[0].RegionId);
        Assert.Equal(new PlaceDecision("Turchia", PlaceAction.Link, turkey.Id), turchia.Decision);
        Assert.False(turchia.Decided);

        var cina = places.Single(p => p.Text == "Cina");
        Assert.Equal(2, cina.Rows);
        Assert.Contains(cina.Suggestions, s => s.RegionId == china.Id);
        Assert.Null(cina.Decision);
    }

    [Fact]
    public async Task Place_decisions_link_or_create_a_region_for_every_row_using_the_text()
    {
        var parent = await _client.AddRegionAsync(HttpExtensions.Unique("Anatolia"));
        var newName = HttpExtensions.Unique("Anatolia occidentale");
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(
            ("01. Coppa", newName + "\nXV", null), ("02. Piatto", newName + "\nXVI", null), ("03. Vaso", "Anatolia o Asia Centrale?\nXIII", null)).Build(),
            await ObjectTypeAsync());
        var (_, prefix) = await _client.UseUniqueNumbersAsync(job);

        var response = await _client.PutJsonAsync($"/api/imports/{job.Id}/places", new SetPlaceDecisionsRequest(
        [
            new PlaceDecision(newName, PlaceAction.Create, Names: new Dictionary<string, string?> { ["it"] = newName }, ParentId: parent.Id),
            new PlaceDecision("Anatolia o Asia Centrale?", PlaceAction.Keep),
        ]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var finished = await _client.CommitAndWaitAsync(job.Id);

        var report = await (await _client.GetAsync($"/api/imports/{job.Id}/report")).ReadAsync<ImportReportDto>();
        Assert.True(finished.ImportedRows == 3, string.Join("; ", report.Rows.SelectMany(r => r.Problems).Select(p => $"{p.Field}: {p.Reason}")));
        var region = Assert.Single(await (await _client.GetAsync($"/api/regions?search={Uri.EscapeDataString(newName)}")).ReadAsync<List<RegionDto>>());
        Assert.Equal(parent.Id, region.ParentId);
        var details = new List<ArtefactDetail>();
        foreach (var row in report.Rows) details.Add(await (await _client.GetAsync($"/api/artefacts/{row.ArtefactId}")).ReadAsync<ArtefactDetail>());
        Assert.All(details.Where(d => d.PlaceOfProduction!.Text == newName), d => Assert.Equal(region.Id, d.PlaceOfProduction!.Region!.Id));
        var kept = details.Single(d => d.InventoryNumber == prefix + "-01-03");
        Assert.Equal("Anatolia o Asia Centrale?", kept.PlaceOfProduction!.Text);
        Assert.Null(kept.PlaceOfProduction.Region);
    }

    [Fact]
    public async Task Unknown_date_is_listed_once_and_a_year_range_resolves_its_rows()
    {
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(
            ("01. Bacile", "Egitto\n19 Dhu'l hijja 797\nMamelucco", null), ("02. Bacile", "Egitto\n19 Dhu'l hijja 797", null)).Build(),
            await ObjectTypeAsync());
        await _client.UseUniqueNumbersAsync(job);

        var dates = await (await _client.GetAsync($"/api/imports/{job.Id}/dates")).ReadAsync<List<ImportDateDto>>();
        var date = Assert.Single(dates);
        Assert.Equal([3, 4], date.SheetRows);
        Assert.Equal(2, (await _client.ReviewAsync(job.Id)).Counts.Problems);

        var resolved = await (await _client.PutJsonAsync($"/api/imports/{job.Id}/dates",
            new SetDateResolutionsRequest([new DateResolution(date.Text, 1395, 1395)]))).ReadAsync<List<ImportDateDto>>();

        Assert.Equal(1395, Assert.Single(resolved).Resolution!.EarliestYear);
        var review = await _client.ReviewAsync(job.Id);
        Assert.Equal(2, review.Counts.Ready);
        Assert.Contains(review.Rows[0].Fields, f => f.Field == ImportField.CreationDate && f.Value.Contains("1395"));
    }

    [Fact]
    public async Task Review_counts_ready_rows_and_problems_with_their_reasons()
    {
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(
            ("01. Coppa", "Cina\nXV", null),
            ("02. Piatto", "Cina\nXV", "Sotheby's\n8 aprile 1975\nlot 202"),
            ("02. Piatto bis", "Cina\nXV", null),
            ("03. Vaso", "Cina\ndatato 1491", null),
            ("diam 30", null, null)).Build(), await ObjectTypeAsync());
        await _client.UseUniqueNumbersAsync(job);

        var review = await _client.ReviewAsync(job.Id);

        Assert.Equal(new ImportCountsDto(5, 2, 0, 3, 0), review.Counts);
        var repeat = review.Rows.Single(r => r.SheetRow == 5);
        Assert.Contains("Row 4", repeat.Problems.Single().Reason);
        Assert.Contains(review.Rows.Single(r => r.SheetRow == 6).Problems, p => p.Field == "creationDate" && p.Text == "datato 1491");
        Assert.Contains(review.Rows.Single(r => r.SheetRow == 7).Problems, p => p.Field == "title");
        var ready = review.Rows.Single(r => r.SheetRow == 4);
        Assert.Contains(ready.Fields, f => f.Field == ImportField.AcquisitionDate && f.Value == "8 April 1975");
        Assert.Equal(3, (await _client.ReviewAsync(job.Id, "problem")).Rows.Count);
    }

    [Fact]
    public async Task Preview_applies_an_unsaved_mapping_to_the_first_rows()
    {
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(("01. Coppa\ndiam 30", "Cina\nXV", null)).Build(), await ObjectTypeAsync());
        var mapping = job.Mapping! with
        {
            Columns = job.Mapping.Columns.Select(c => c.Column == 2 ? new ColumnRule(2, "Nome File", ImportField.Title) : c).ToList(),
        };

        var preview = await (await _client.PostJsonAsync($"/api/imports/{job.Id}/preview", new PreviewMappingRequest(mapping))).ReadAsync<ImportReviewDto>();

        Assert.Equal("01. Coppa diam 30", preview.Rows.Single().Title);
        Assert.Equal(ImportJobState.AwaitingMapping, (await _client.GetImportAsync(job.Id)).State);
    }

    // ---------- list values and parties ----------

    [Fact]
    public async Task List_texts_are_listed_with_counts_exact_matches_and_suggestions()
    {
        var token = HttpExtensions.Unique("x");
        var safavide = await _client.AddListEntryAsync("dynasties",
            new Dictionary<string, string?> { ["it"] = $"Safavide {token}", ["en"] = $"Safavid {token}" });
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(
            ("01. Coppa", $"Iran\nXVI\nSafavide {token}", $"Sotheby's {token}\n12 06 1990"),
            ("02. Piatto", $"Iran\nXVI\nSfavide {token}", null),
            ("03. Vaso", $"Iran\nXVI\nSfavide {token}", null)).Build(), await ObjectTypeAsync());

        var lists = await _client.ListsAsync(job.Id);

        Assert.Equal([ImportList.CollectionGroup, ImportList.Dynasty, ImportList.StorageLocation, ImportList.Party], lists.Select(l => l.List));
        var dynasties = lists.Single(l => l.List == ImportList.Dynasty);
        var exact = dynasties.Values.Single(v => v.Text == $"Safavide {token}");
        Assert.Equal((1, false, safavide.Id), (exact.Rows, exact.Decided, exact.Decision!.EntryId));
        var variant = dynasties.Values.Single(v => v.Text == $"Sfavide {token}");
        Assert.Equal(2, variant.Rows);
        Assert.Null(variant.Decision);
        Assert.Equal(safavide.Id, variant.Suggestions[0].EntryId);
        Assert.Equal(1, dynasties.Undecided);
        Assert.Equal(3, lists.Single(l => l.List == ImportList.CollectionGroup).Values.Single(v => v.Text == "CINA").Rows);
        var parties = lists.Single(l => l.List == ImportList.Party).Values;
        Assert.Equal([ImportField.AcquisitionSource], parties.Single(v => v.Text == $"Sotheby's {token}").Fields);
        // "GE" in the holding column is a location code.
        Assert.Equal([ImportField.StorageLocation], lists.Single(l => l.List == ImportList.StorageLocation).Values.Single(v => v.Text == "GE").Fields);
    }

    [Fact]
    public async Task Undecided_list_text_holds_back_its_rows_until_linked()
    {
        var token = HttpExtensions.Unique("x");
        var safavide = await _client.AddListEntryAsync("dynasties", $"Safavide {token}");
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(
            ("01. Coppa", $"Iran\nXVI\nSfavide {token}", null), ("02. Piatto", "Iran\nXVI", null)).Build(), await ObjectTypeAsync());
        await _client.UseUniqueNumbersAsync(job, decideLists: false);
        var before = (await _client.ReviewAsync(job.Id)).Rows.Single(r => r.SheetRow == 3);
        Assert.Equal(ImportRowStatus.Problem, before.Status);
        Assert.Equal($"Sfavide {token}", before.Problems.Single(p => p.Field == "dynasty").Text);

        var linked = await _client.SetListsAsync(job.Id, new ListDecision(ImportList.Dynasty, $"sfavide {token}", ListAction.Link, safavide.Id));

        var value = linked.Single(l => l.List == ImportList.Dynasty).Values.Single();
        Assert.True(value.Decided);
        Assert.Equal(safavide.Id, value.Decision!.EntryId);
        var row = (await _client.ReviewAsync(job.Id)).Rows.Single(r => r.SheetRow == 3);
        Assert.DoesNotContain(row.Problems, p => p.Field == "dynasty");
        // The review shows the text as written and the entry it links to.
        Assert.Contains(row.Fields, f => f.Field == ImportField.Dynasty && f.Value == $"Sfavide {token} → {safavide.Name}");
    }

    [Fact]
    public async Task List_decisions_are_validated()
    {
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(("01. Coppa", "Iran\nXVI", null)).Build(), await ObjectTypeAsync());

        var response = await _client.PutJsonAsync($"/api/imports/{job.Id}/lists", new SetListDecisionsRequest(
        [
            new ListDecision(ImportList.Dynasty, "Sfavide", ListAction.Link),
            new ListDecision(ImportList.Party, "Rossi", ListAction.Create, Name: "Rossi"),
            new ListDecision(ImportList.Dynasty, "Nuova", ListAction.Create, Names: new Dictionary<string, string?> { ["it"] = " " }),
            new ListDecision(ImportList.StorageLocation, "Beta2", ListAction.Link, Guid.NewGuid()),
            new ListDecision(ImportList.StorageLocation, "Beta3", ListAction.Create),
        ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.ValidationErrorsAsync();
        foreach (var key in new[] { "lists[0].entryId", "lists[1].partyKind", "lists[2].names", "lists[3].entryId", "lists[4].name" })
            Assert.True(errors.ContainsKey(key), $"{key} missing in {string.Join(", ", errors.Keys)}");
    }

    [Fact]
    public async Task Entries_and_parties_marked_for_creation_are_created_once_and_linked_from_every_row()
    {
        var token = HttpExtensions.Unique("x");
        var dynasty = $"Ilkhanide {token}";
        var seller = $"Rossi Antiquariato {token}";
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(
            ("01. Coppa", $"Iran\nXIV\n{dynasty}", $"{seller}\n12 06 1990"),
            ("02. Piatto", $"Iran\nXIV\n{dynasty}", $"{seller}\n13 06 1990")).Build(), await ObjectTypeAsync());
        await _client.SetListsAsync(job.Id,
            new ListDecision(ImportList.Dynasty, dynasty, ListAction.Create, Names: new Dictionary<string, string?> { ["it"] = dynasty, ["en"] = $"Ilkhanid {token}" }),
            new ListDecision(ImportList.Party, seller, ListAction.Create, Name: seller, PartyKind: Domain.Parties.PartyKind.Organisation));
        await _client.UseUniqueNumbersAsync(job);

        var finished = await _client.CommitAndWaitAsync(job.Id);

        Assert.Equal(2, finished.ImportedRows);
        var entry = Assert.Single(await (await _client.GetAsync($"/api/lists/dynasties?search={Uri.EscapeDataString(token)}")).ReadAsync<List<ReferenceEntryDto>>());
        Assert.Equal(2, entry.ArtefactCount);
        Assert.Equal($"Ilkhanid {token}", entry.Names!["en"]);
        var party = Assert.Single(await (await _client.GetAsync($"/api/parties?search={Uri.EscapeDataString(seller)}")).ReadAsync<List<PartyDto>>());
        Assert.Equal((2, Domain.Parties.PartyKind.Organisation), (party.SourceCount, party.Kind));
        var report = await (await _client.GetAsync($"/api/imports/{job.Id}/report")).ReadAsync<ImportReportDto>();
        foreach (var row in report.Rows)
        {
            var artefact = await (await _client.GetAsync($"/api/artefacts/{row.ArtefactId}")).ReadAsync<ArtefactDetail>();
            Assert.Equal(entry.Id, artefact.Dynasty!.Id);
            Assert.Equal(party.Id, artefact.Acquisition!.Source!.Id);
        }

        // The ids are recorded on the job.
        Assert.All((await _client.GetImportAsync(job.Id)).Decisions.Lists.Where(d => d.Action == ListAction.Create), d => Assert.NotNull(d.EntryId));
    }

    [Fact]
    public async Task Committed_import_teaches_its_template_its_decisions()
    {
        var token = HttpExtensions.Unique("x");
        var type = await ObjectTypeAsync();
        var safavide = await _client.AddListEntryAsync("dynasties", $"Safavide {token}");
        var china = await _client.AddRegionAsync(HttpExtensions.Unique("China"));
        var seed = await _client.StartImportAsync(ImportTestData.Ceramics(("01. Coppa", "Iran\nXV", null)).Build(), type);
        await _client.PutJsonAsync($"/api/imports/{seed.Id}/places", new SetPlaceDecisionsRequest([new PlaceDecision("Iran", PlaceAction.Keep)]));
        var template = await (await _client.PostJsonAsync("/api/import-templates",
            new SaveImportTemplateRequest(HttpExtensions.Unique("Collection workbook 2024"), seed.Id))).ReadAsync<ImportTemplateDto>();

        var first = await _client.StartImportAsync(ImportTestData.Ceramics(
            ("01. Coppa", $"Cina {token}\nXVI\nSfavide {token}", null),
            ("02. Piatto", $"Cina {token}\nXVI\nNuova {token}", null)).Build(), type, templateId: template.Id);
        await _client.SetListsAsync(first.Id,
            new ListDecision(ImportList.Dynasty, $"Sfavide {token}", ListAction.Link, safavide.Id),
            ImportTestData.CreateDecision(ImportList.Dynasty, $"Nuova {token}"));
        await _client.PutJsonAsync($"/api/imports/{first.Id}/places",
            new SetPlaceDecisionsRequest([new PlaceDecision($"Cina {token}", PlaceAction.Link, china.Id)]));
        await _client.UseUniqueNumbersAsync(first);
        Assert.Equal(2, (await _client.CommitAndWaitAsync(first.Id)).ImportedRows);

        var second = await _client.StartImportAsync(ImportTestData.Ceramics(
            ("01. Tappeto", $"Cina {token}\nXVI\nSfavide {token}", null),
            ("02. Tappeto", $"Cina {token}\nXVI\nNuova {token}", null)).Build(), type, "tessuti.xlsx", template.Id);

        var dynasties = (await _client.ListsAsync(second.Id)).Single(l => l.List == ImportList.Dynasty).Values;
        var sfavide = dynasties.Single(v => v.Text == $"Sfavide {token}");
        Assert.True(sfavide.Decided);
        Assert.Equal((ListAction.Link, safavide.Id), (sfavide.Decision!.Action, sfavide.Decision.EntryId));
        // The entry created by the first import is linked, not created again.
        var created = Assert.Single(await (await _client.GetAsync($"/api/lists/dynasties?search={Uri.EscapeDataString("Nuova " + token)}")).ReadAsync<List<ReferenceEntryDto>>());
        Assert.Equal(created.Id, dynasties.Single(v => v.Text == $"Nuova {token}").Decision!.EntryId);
        // Place decisions are learned too, and the template's earlier decisions are kept.
        var learned = await (await _client.GetAsync($"/api/import-templates/{template.Id}")).ReadAsync<ImportTemplateDto>();
        Assert.Contains(learned.Decisions.Places, p => p.Text == $"Cina {token}" && p.RegionId == china.Id);
        Assert.Contains(learned.Decisions.Places, p => p is { Text: "Iran", Action: PlaceAction.Keep });
    }

    // ---------- commit ----------

    [Fact]
    public async Task Good_rows_import_with_their_pictures_despite_problem_rows()
    {
        var left = TestData.Jpeg(700);
        var right = TestData.Tiff(900);
        var builder = ImportTestData.Ceramics(
            ("01. Coppa Celadon\ndiam 17,6 x h 7", "Cina\nfine XII inizio XIII\nSung", "Sotheby's London\n12 06 1990\nlot 170 £ 7.865"),
            ("02. Piatto", "Cina\nXV", null),
            ("03. Vaso", "Cina\ndatato 1491", null));
        builder.Picture(3, 3, right, "image/tiff", columnOffset: 3_000_000).Picture(3, 3, left, columnOffset: 0)
            .Picture(4, 3, WorkbookBuilder.JpegXr(), "image/vnd.ms-photo");
        var job = await _client.StartImportAsync(builder.Build(), await ObjectTypeAsync());
        var (_, prefix) = await _client.UseUniqueNumbersAsync(job);

        var finished = await _client.CommitAndWaitAsync(job.Id);

        Assert.Equal(ImportJobState.Finished, finished.State);
        Assert.Equal((2, 0, 1, 3), (finished.ImportedRows, finished.SkippedRows, finished.ProblemRows, finished.ProcessedRows));
        var report = await (await _client.GetAsync($"/api/imports/{job.Id}/report")).ReadAsync<ImportReportDto>();
        var first = report.Rows.Single(r => r.SheetRow == 3);
        var artefact = await (await _client.GetAsync($"/api/artefacts/{first.ArtefactId}")).ReadAsync<ArtefactDetail>();
        Assert.Equal(prefix + "-01-01", artefact.InventoryNumber);
        Assert.Equal("Coppa Celadon", artefact.Title);
        Assert.Equal("CINA", artefact.CollectionGroup!.Name);
        Assert.Equal("Sotheby's London", artefact.Acquisition!.Source!.Name);
        Assert.Equal("GE", artefact.StorageLocation!.Name);
        Assert.Equal("fine XII inizio XIII", artefact.CreationDate!.Display);
        Assert.Equal(7865m, artefact.Acquisition!.Price!.Amount);
        Assert.Equal(2, artefact.Images.Count);
        Assert.True(artefact.Images[0].IsPrimary);
        Assert.Equal(left, await File.ReadAllBytesAsync(Path.Combine(factory.MediaRoot, "originals", artefact.Images[0].MediaAssetId.ToString("N"))));

        var withJpegXr = report.Rows.Single(r => r.SheetRow == 4);
        Assert.Equal(ImportRowStatus.Imported, withJpegXr.Status);
        Assert.Contains("image/vnd.ms-photo", Assert.Single(withJpegXr.Warnings));
        Assert.Empty((await (await _client.GetAsync($"/api/artefacts/{withJpegXr.ArtefactId}")).ReadAsync<ArtefactDetail>()).Images);

        var problem = report.Rows.Single(r => r.SheetRow == 5);
        Assert.Equal(ImportRowStatus.Problem, problem.Status);
        Assert.Equal("datato 1491", problem.Problems.Single().Text);
        // Staged files are removed once the import has finished.
        Assert.False(Directory.Exists(StagingDirectory(job.Id)));
    }

    [Fact]
    public async Task Re_import_adds_only_the_rows_fixed_in_the_workbook()
    {
        var type = await ObjectTypeAsync();
        var original = ImportTestData.Ceramics(("01. Coppa", "Cina\nXV", null), ("02. Piatto", "Cina\ndatato 1491", null)).Build();
        var first = await _client.StartImportAsync(original, type);
        var (mapped, _) = await _client.UseUniqueNumbersAsync(first);
        var template = await (await _client.PostJsonAsync("/api/import-templates",
            new SaveImportTemplateRequest(HttpExtensions.Unique("Ceramics template"), mapped.Id))).ReadAsync<ImportTemplateDto>();
        Assert.Equal(1, (await _client.CommitAndWaitAsync(first.Id)).ImportedRows);

        var fixedWorkbook = ImportTestData.Ceramics(("01. Coppa", "Cina\nXV", null), ("02. Piatto", "Cina\n1491", null)).Build();
        var second = await _client.StartImportAsync(fixedWorkbook, type, templateId: template.Id);
        var review = await _client.ReviewAsync(second.Id);
        Assert.Equal(new ImportCountsDto(2, 1, 1, 0, 0), review.Counts);

        var finished = await _client.CommitAndWaitAsync(second.Id);

        Assert.Equal((1, 1, 0), (finished.ImportedRows, finished.SkippedRows, finished.ProblemRows));
    }

    [Fact]
    public async Task A_committed_import_can_no_longer_be_changed()
    {
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(("01. Coppa", "Cina\nXV", null)).Build(), await ObjectTypeAsync());
        await _client.UseUniqueNumbersAsync(job);
        await _client.CommitAndWaitAsync(job.Id);

        Assert.Equal(HttpStatusCode.Conflict, (await _client.PutJsonAsync($"/api/imports/{job.Id}/mapping", job.Mapping)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/imports/{job.Id}/discard", null)).StatusCode);
    }

    // ---------- report, discard and clean-up ----------

    [Fact]
    public async Task Report_lists_rows_and_downloads_as_a_spreadsheet()
    {
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(("01. Coppa", "Cina\nXV", null), ("diam 30", null, null)).Build(), await ObjectTypeAsync());
        await _client.UseUniqueNumbersAsync(job);
        await _client.CommitAndWaitAsync(job.Id);

        var report = await (await _client.GetAsync($"/api/imports/{job.Id}/report")).ReadAsync<ImportReportDto>();
        Assert.Equal([ImportRowStatus.Imported, ImportRowStatus.Problem], report.Rows.Select(r => r.Status));

        var response = await _client.GetAsync($"/api/imports/{job.Id}/report.xlsx");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        using var document = SpreadsheetDocument.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), false);
        var cells = document.WorkbookPart!.WorksheetParts.Single().Worksheet!.Descendants<Cell>().Select(c => c.InnerText).ToList();
        Assert.Contains("Sheet row", cells);
        Assert.Contains("The row has no title.", cells);
        Assert.Contains($"/artefacts/{report.Rows[0].ArtefactId}", cells);
    }

    [Fact]
    public async Task Discarding_removes_the_workbook_and_pictures_and_adds_nothing()
    {
        var workbook = ImportTestData.Ceramics(("01. Coppa", "Cina\nXV", null)).Picture(3, 3, TestData.Jpeg(500)).Build();
        var job = await _client.StartImportAsync(workbook, await ObjectTypeAsync());
        var (_, prefix) = await _client.UseUniqueNumbersAsync(job);
        Assert.True(Directory.Exists(StagingDirectory(job.Id)));

        var discarded = await (await _client.PostAsync($"/api/imports/{job.Id}/discard", null)).ReadAsync<ImportJobDto>();

        Assert.Equal(ImportJobState.Discarded, discarded.State);
        Assert.False(Directory.Exists(StagingDirectory(job.Id)));
        var list = await _client.ListArtefactsAsync(search: prefix);
        Assert.Equal(0, list.Total);
    }

    [Fact]
    public async Task Imports_left_uncommitted_for_14_days_are_swept()
    {
        var job = await _client.StartImportAsync(ImportTestData.Ceramics(("01. Coppa", "Cina\nXV", null)).Build(), await ObjectTypeAsync());
        await using (var connection = new NpgsqlConnection(factory.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("UPDATE import_jobs SET updated_at = now() - interval '15 days' WHERE id = @id", connection);
            command.Parameters.AddWithValue("id", job.Id);
            await command.ExecuteNonQueryAsync();
        }

        await factory.Services.GetRequiredService<ImportProcessor>().SweepAsync(CancellationToken.None);

        Assert.Equal(ImportJobState.Discarded, (await _client.GetImportAsync(job.Id)).State);
        Assert.False(Directory.Exists(StagingDirectory(job.Id)));
    }

    [Fact]
    public async Task Review_rows_link_signed_thumbnails_of_staged_pictures()
    {
        var workbook = ImportTestData.Ceramics(("01. Coppa", "Cina\nXV", null)).Picture(3, 3, TestData.Jpeg(500)).Build();
        var job = await _client.StartImportAsync(workbook, await ObjectTypeAsync());

        var picture = (await _client.ReviewAsync(job.Id)).Rows.Single().Pictures.Single();

        Assert.True(picture.Supported);
        Assert.StartsWith("http://imgproxy.test/", picture.ThumbUrl);
        Assert.Contains($"imports/{job.Id:N}/pictures/1.jpg", Uri.UnescapeDataString(picture.ThumbUrl!));
    }
}
