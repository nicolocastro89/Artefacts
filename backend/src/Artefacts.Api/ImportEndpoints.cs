using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Application.Imports;
using Artefacts.Application.Media;
using Artefacts.Domain.Imports;
using Microsoft.AspNetCore.Mvc;

namespace Artefacts.Api;

internal static class ImportEndpoints
{
    public static void MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/imports").WithTags("Imports");

        g.MapPost("/", async (IFormFile? file, [FromForm] Guid? objectTypeId, [FromForm] Guid? templateId, [FromForm] string? sheet,
                ImportHandler h, CancellationToken ct) =>
            {
                var upload = file is null ? null : new UploadFile(file.FileName, file.Length, file.OpenReadStream);
                var job = await h.CreateAsync(upload, objectTypeId, templateId, sheet, ct);
                return TypedResults.Created($"/api/imports/{job.Id}", job);
            })
            .WithName("createImport")
            .WithSummary("Uploads an .xlsx workbook and starts reading it in the background.")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(Endpoints.MaxUploadRequestBytes))
            .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = Endpoints.MaxUploadRequestBytes })
            .ProducesValidationProblem();

        g.MapGet("/", async (ImportHandler h, CancellationToken ct) => TypedResults.Ok(await h.ListAsync(ct)))
            .WithName("listImports");

        g.MapGet("/fields", () => TypedResults.Ok(ImportHandler.Fields()))
            .WithName("listImportFields")
            .WithSummary("The catalogue fields a workbook column can be mapped to.");

        g.MapGet("/lookup-tables", async (ImportLookupHandler h, CancellationToken ct) => TypedResults.Ok(await h.ListAsync(ct)))
            .WithName("listImportLookupTables")
            .WithSummary("The tables of words parsing reads (month names, currency words, measurement labels, spellings of the inch), built-in entries with the user's.");

        g.MapGet("/lookup-tables/{id}", async (string id, ImportLookupHandler h, CancellationToken ct) => TypedResults.Ok(await h.GetAsync(id, ct)))
            .WithName("getImportLookupTable")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPut("/lookup-tables/{id}", async (string id, SaveImportLookupTableRequest request, ImportLookupHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.ReplaceAsync(id, request, ct)))
            .WithName("saveImportLookupTable")
            .WithSummary("Replaces the user entries of a table; none puts the table back to the built-in entries.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapGet("/detectors", async (ImportDetectorHandler h, CancellationToken ct) => TypedResults.Ok(await h.ListAsync(ct)))
            .WithName("listImportDetectors")
            .WithSummary("The detectors a line rule can recognise lines with: the built-in ones and the user's own.");

        g.MapPost("/detectors/try", async (TryImportDetectorRequest request, ImportDetectorHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.TryAsync(request, ct)))
            .WithName("tryImportDetector")
            .WithSummary("Runs a detector definition against sample lines without saving anything, for live feedback while editing.")
            .ProducesValidationProblem();

        g.MapPost("/detectors", async (SaveImportDetectorRequest request, ImportDetectorHandler h, CancellationToken ct) =>
            {
                var detector = await h.CreateAsync(request, ct);
                return TypedResults.Created($"/api/imports/detectors/{detector.Id}", detector);
            })
            .WithName("createImportDetector")
            .WithSummary("Saves a user detector: a pattern with named captures (value, remainder, item) or a list of keywords.")
            .ProducesValidationProblem();

        g.MapGet("/detectors/{id}", async (string id, ImportDetectorHandler h, CancellationToken ct) => TypedResults.Ok(await h.GetAsync(id, ct)))
            .WithName("getImportDetector")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPut("/detectors/{id}", async (string id, SaveImportDetectorRequest request, ImportDetectorHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.UpdateAsync(id, request, ct)))
            .WithName("updateImportDetector")
            .WithSummary("Changes a user detector. Built-in detectors cannot be changed.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        g.MapDelete("/detectors/{id}", async (string id, ImportDetectorHandler h, CancellationToken ct) =>
            {
                await h.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("deleteImportDetector")
            .WithSummary("Deletes a user detector that no template or unfinished import uses.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        g.MapPost("/detectors/{id}/duplicate", async (string id, DuplicateImportDetectorRequest request, ImportDetectorHandler h, CancellationToken ct) =>
            {
                var copy = await h.DuplicateAsync(id, request, ct);
                return TypedResults.Created($"/api/imports/detectors/{copy.Id}", copy);
            })
            .WithName("duplicateImportDetector")
            .WithSummary("Copies a user detector under a new name.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        g.MapGet("/{id:guid}", async (Guid id, ImportHandler h, CancellationToken ct) => TypedResults.Ok(await h.GetAsync(id, ct)))
            .WithName("getImport")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapGet("/{id:guid}/columns", async (Guid id, ImportHandler h, CancellationToken ct) => TypedResults.Ok(await h.ColumnsAsync(id, ct)))
            .WithName("getImportColumns")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPut("/{id:guid}/mapping", async (Guid id, ImportMapping mapping, ImportHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.SetMappingAsync(id, mapping, ct)))
            .WithName("setImportMapping")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        g.MapPost("/{id:guid}/preview", async (Guid id, PreviewMappingRequest request, ImportHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.PreviewAsync(id, request, ct)))
            .WithName("previewImportMapping")
            .WithSummary("Evaluates the first rows with an unsaved mapping.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapGet("/{id:guid}/places", async (Guid id, ImportHandler h, CancellationToken ct) => TypedResults.Ok(await h.PlacesAsync(id, ct)))
            .WithName("getImportPlaces")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPut("/{id:guid}/places", async (Guid id, SetPlaceDecisionsRequest request, ImportHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.SetPlacesAsync(id, request, ct)))
            .WithName("setImportPlaces")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        g.MapGet("/{id:guid}/lists", async (Guid id, ImportHandler h, CancellationToken ct) => TypedResults.Ok(await h.ListsAsync(id, ct)))
            .WithName("getImportLists")
            .WithSummary("The workbook's collection group, dynasty, storage location and party texts with their decisions.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPut("/{id:guid}/lists", async (Guid id, SetListDecisionsRequest request, ImportHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.SetListsAsync(id, request, ct)))
            .WithName("setImportLists")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        g.MapGet("/{id:guid}/resolutions/{kind}", async (Guid id, string kind, ImportHandler h, CancellationToken ct) => TypedResults.Ok(await h.ResolutionsAsync(id, kind, ct)))
            .WithName("getImportResolutions")
            .WithSummary("The texts of one kind (dates, dimensions) the import cannot settle by rule, with their rows and decisions.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPut("/{id:guid}/resolutions/{kind}", async (Guid id, string kind, SetImportResolutionsRequest request, ImportHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.SetResolutionsAsync(id, kind, request, ct)))
            .WithName("setImportResolutions")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        g.MapGet("/{id:guid}/review", async Task<IResult> (Guid id, string? status, int? page, int? pageSize, ImportHandler h, CancellationToken ct) =>
            {
                ImportRowStatus? filter = null;
                if (!string.IsNullOrEmpty(status))
                {
                    if (!QueryEnum.TryParse(status, ImportRowStatus.Pending, out var parsed))
                        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                        {
                            ["status"] = [$"Must be one of: {string.Join(", ", QueryEnum.AllowedValues<ImportRowStatus>())}."],
                        });
                    filter = parsed;
                }

                return TypedResults.Ok(await h.ReviewAsync(id, filter, page ?? 1, pageSize ?? 50, ct));
            })
            .WithName("getImportReview")
            .Produces<ImportReviewDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPost("/{id:guid}/commit", async (Guid id, ImportHandler h, CancellationToken ct) => TypedResults.Ok(await h.CommitAsync(id, ct)))
            .WithName("commitImport")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        g.MapPost("/{id:guid}/discard", async (Guid id, ImportHandler h, CancellationToken ct) => TypedResults.Ok(await h.DiscardAsync(id, ct)))
            .WithName("discardImport")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        g.MapGet("/{id:guid}/report", async (Guid id, ImportHandler h, CancellationToken ct) => TypedResults.Ok(await h.ReportAsync(id, ct)))
            .WithName("getImportReport")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapGet("/{id:guid}/report.xlsx", async (Guid id, ImportHandler h, IImportReportWriter writer, CancellationToken ct) =>
            {
                var report = await h.ReportAsync(id, ct);
                var output = new MemoryStream();
                writer.Write(report, output);
                output.Position = 0;
                var name = Path.GetFileNameWithoutExtension(report.Job.FileName).Trim();
                return TypedResults.File(output, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{name} - import report.xlsx");
            })
            .WithName("downloadImportReport")
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            .ProducesProblem(StatusCodes.Status404NotFound);

        var t = app.MapGroup("/api/import-templates").WithTags("Imports");

        t.MapGet("/", async (ImportTemplateHandler h, CancellationToken ct) => TypedResults.Ok(await h.ListAsync(ct)))
            .WithName("listImportTemplates");

        t.MapGet("/{id:guid}", async (Guid id, ImportTemplateHandler h, CancellationToken ct) => TypedResults.Ok(await h.GetAsync(id, ct)))
            .WithName("getImportTemplate")
            .ProducesProblem(StatusCodes.Status404NotFound);

        t.MapPost("/", async (SaveImportTemplateRequest request, ImportTemplateHandler h, CancellationToken ct) =>
            {
                var template = await h.CreateAsync(request, ct);
                return TypedResults.Created($"/api/import-templates/{template.Id}", template);
            })
            .WithName("createImportTemplate")
            .ProducesValidationProblem();

        t.MapPut("/{id:guid}", async (Guid id, SaveImportTemplateRequest request, ImportTemplateHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.UpdateAsync(id, request, ct)))
            .WithName("updateImportTemplate")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        t.MapDelete("/{id:guid}", async (Guid id, ImportTemplateHandler h, CancellationToken ct) =>
            {
                await h.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("deleteImportTemplate")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
