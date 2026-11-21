using System.Net.Http.Headers;
using Artefacts.Application.Contracts;
using Artefacts.Application.Imports;
using Artefacts.Domain.Imports;

namespace Artefacts.Api.IntegrationTests.Infrastructure;

public static class ImportTestData
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public static MultipartFormDataContent ImportForm(byte[] workbook, Guid? objectTypeId, string fileName = "ceramiche.xlsx",
        Guid? templateId = null, string? sheet = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        if (objectTypeId is { } type) form.Add(new StringContent(type.ToString()), "objectTypeId");
        if (templateId is { } template) form.Add(new StringContent(template.ToString()), "templateId");
        if (sheet is not null) form.Add(new StringContent(sheet), "sheet");
        return form;
    }

    public static async Task<ImportJobDto> GetImportAsync(this HttpClient client, Guid id) =>
        await (await client.GetAsync($"/api/imports/{id}")).ReadAsync<ImportJobDto>();

    public static async Task<ImportJobDto> WaitForImportAsync(this HttpClient client, Guid id, Func<ImportJobDto, bool> done)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (true)
        {
            var job = await client.GetImportAsync(id);
            if (done(job)) return job;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Import {id} is still {job.State} ({job.ProcessedRows}/{job.TotalRows}); error: {job.Error}");
            await Task.Delay(150);
        }
    }

    /// <summary>Uploads a workbook and waits until it has been read.</summary>
    public static async Task<ImportJobDto> StartImportAsync(this HttpClient client, byte[] workbook, Guid objectTypeId,
        string fileName = "ceramiche.xlsx", Guid? templateId = null)
    {
        var created = await (await client.PostAsync("/api/imports", ImportForm(workbook, objectTypeId, fileName, templateId))).ReadAsync<ImportJobDto>();
        return await client.WaitForImportAsync(created.Id, j => j.State != ImportJobState.Reading);
    }

    /// <summary>
    /// Saves the job's mapping with an inventory number prefix unique to the test, since tests share one catalogue,
    /// then decides every undecided list text (see <see cref="DecideAllListsAsync"/>) so that rows are not held back by
    /// list curation.
    /// </summary>
    public static async Task<(ImportJobDto Job, string Prefix)> UseUniqueNumbersAsync(this HttpClient client, ImportJobDto job,
        Func<ImportMapping, ImportMapping>? change = null, bool decideLists = true)
    {
        var prefix = HttpExtensions.Unique("T").ToUpperInvariant();
        var mapping = job.Mapping! with { InventoryNumber = job.Mapping!.InventoryNumber with { Pattern = prefix + "-{group:2}-{item:2}" } };
        if (change is not null) mapping = change(mapping);
        var result = await (await client.PutJsonAsync($"/api/imports/{job.Id}/mapping", mapping)).ReadAsync<ImportMappingResult>();
        if (!decideLists) return (result.Job, prefix);
        await client.DecideAllListsAsync(job.Id);
        return (await client.GetImportAsync(job.Id), prefix);
    }

    public static async Task<List<ImportListGroupDto>> ListsAsync(this HttpClient client, Guid id) =>
        await (await client.GetAsync($"/api/imports/{id}/lists")).ReadAsync<List<ImportListGroupDto>>();

    public static async Task<List<ImportListGroupDto>> SetListsAsync(this HttpClient client, Guid id, params ListDecision[] decisions)
    {
        var response = await client.PutJsonAsync($"/api/imports/{id}/lists", new SetListDecisionsRequest(decisions));
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Setting list decisions failed: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadAsync<List<ImportListGroupDto>>();
    }

    /// <summary>A decision creating an entry named as the text: an Italian name, a single name, or an organisation.</summary>
    public static ListDecision CreateDecision(ImportList list, string text) => list switch
    {
        ImportList.Party => new ListDecision(list, text, ListAction.Create, Name: text, PartyKind: Domain.Parties.PartyKind.Organisation),
        ImportList.StorageLocation => new ListDecision(list, text, ListAction.Create, Name: text),
        _ => new ListDecision(list, text, ListAction.Create, Names: new Dictionary<string, string?> { ["it"] = text }),
    };

    /// <summary>Marks every list text without a decision in force for creation, named as written.</summary>
    public static async Task DecideAllListsAsync(this HttpClient client, Guid id)
    {
        var undecided = (await client.ListsAsync(id))
            .SelectMany(g => g.Values.Where(v => v.Decision is null).Select(v => CreateDecision(g.List, v.Text)))
            .ToArray();
        if (undecided.Length > 0) await client.SetListsAsync(id, undecided);
    }

    public static async Task<ImportReviewDto> ReviewAsync(this HttpClient client, Guid id, string? status = null) =>
        await (await client.GetAsync($"/api/imports/{id}/review?pageSize=200" + (status is null ? "" : $"&status={status}"))).ReadAsync<ImportReviewDto>();

    public static async Task<ImportJobDto> CommitAndWaitAsync(this HttpClient client, Guid id)
    {
        await (await client.PostAsync($"/api/imports/{id}/commit", null)).ReadAsync<ImportJobDto>();
        return await client.WaitForImportAsync(id, j => j.State is ImportJobState.Finished or ImportJobState.Failed);
    }

    /// <summary>
    /// A small ceramics workbook in the collection's layout: holding, title, picture, origin and acquisition columns,
    /// a group heading, and the given record rows (title cell, origin cell, acquisition cell).
    /// </summary>
    public static WorkbookBuilder Ceramics(params (string Title, string? Origin, string? Acquisition)[] rows)
    {
        var builder = new WorkbookBuilder("CERAMICHE")
            .Row("Gruppo e numero", "Nome File", "Immagine", "Zona di Produzione\nData\nDinastia", "Provenienza", "Formato Immagine", "Mostre", "William")
            .Row("01. CINA");
        foreach (var (title, origin, acquisition) in rows)
            builder.Row("01.\nGE", title, null, origin, acquisition, "jpg");
        return builder;
    }
}
