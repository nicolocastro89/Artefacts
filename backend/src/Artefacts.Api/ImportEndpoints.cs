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

        g.MapGet("/{id:guid}/dates", async (Guid id, ImportHandler h, CancellationToken ct) => TypedResults.Ok(await h.DatesAsync(id, ct)))
            .WithName("getImportDates")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPut("/{id:guid}/dates", async (Guid id, SetDateResolutionsRequest request, ImportHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.SetDatesAsync(id, request, ct)))
            .WithName("setImportDates")
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
