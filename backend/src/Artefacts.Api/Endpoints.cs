using Artefacts.Application.Abstractions;
using Artefacts.Application.Artefacts;
using Artefacts.Application.Contracts;
using Artefacts.Application.Media;
using Artefacts.Application.ObjectTypes;
using Artefacts.Application.Regions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Artefacts.Api;

internal static class Endpoints
{
    /// <summary>Total request limit for one multi-file upload; each file is also limited by Media:MaxUploadBytes.</summary>
    public const long MaxUploadRequestBytes = 2L * 1024 * 1024 * 1024;

    public static void MapArtefactEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/artefacts").WithTags("Artefacts");

        g.MapGet("/", async Task<Results<Ok<ArtefactListPage>, ValidationProblem>> (
                [AsParameters] ListArtefactsParameters p, IArtefactListQuery query, CancellationToken ct) =>
            {
                var errors = new Dictionary<string, string[]>();
                if (!QueryEnum.TryParse(p.Sort, ArtefactSort.InventoryNumber, out var sort))
                    errors["sort"] = [$"Must be one of: {string.Join(", ", QueryEnum.AllowedValues<ArtefactSort>())}."];
                if (!QueryEnum.TryParse(p.Direction, SortDirection.Asc, out var direction))
                    errors["direction"] = [$"Must be one of: {string.Join(", ", QueryEnum.AllowedValues<SortDirection>())}."];
                if (!QueryEnum.TryParse(p.Group, ArtefactGrouping.None, out var group))
                    errors["group"] = [$"Must be one of: {string.Join(", ", QueryEnum.AllowedValues<ArtefactGrouping>())}."];
                if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

                return TypedResults.Ok(await query.ListAsync(
                    new ArtefactListRequest(p.Search, sort, direction, p.Page ?? 1, p.PageSize ?? 25, group), ct));
            })
            .WithName("listArtefacts");

        g.MapGet("/{id:guid}", async (Guid id, ArtefactDetailQueryHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.GetAsync(id, ct)))
            .WithName("getArtefact")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPost("/", async (ArtefactWriteRequest request, ArtefactCommandHandler h, CancellationToken ct) =>
            {
                var created = await h.CreateAsync(request, ct);
                return TypedResults.Created($"/api/artefacts/{created.Id}", created);
            })
            .WithName("createArtefact")
            .ProducesValidationProblem();

        g.MapPut("/{id:guid}", async (Guid id, ArtefactWriteRequest request, ArtefactCommandHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.UpdateAsync(id, request, ct)))
            .WithName("updateArtefact")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var images = g.MapGroup("/{id:guid}/images").WithTags("Images");

        images.MapPost("/", async (Guid id, IFormFileCollection files, ImageHandler h, CancellationToken ct) =>
            {
                var uploads = files.Select(f => new UploadFile(f.FileName, f.Length, f.OpenReadStream)).ToList();
                return TypedResults.Ok(await h.UploadAsync(id, uploads, ct));
            })
            .WithName("uploadArtefactImages")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadRequestBytes))
            .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxUploadRequestBytes })
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        images.MapPut("/{mediaAssetId:guid}/primary", async (Guid id, Guid mediaAssetId, ImageHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.MakePrimaryAsync(id, mediaAssetId, ct)))
            .WithName("makeArtefactImagePrimary")
            .ProducesProblem(StatusCodes.Status404NotFound);

        images.MapPatch("/{mediaAssetId:guid}", async (Guid id, Guid mediaAssetId, SetCaptionRequest request, ImageHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.SetCaptionAsync(id, mediaAssetId, request, ct)))
            .WithName("setArtefactImageCaption")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        images.MapDelete("/{mediaAssetId:guid}", async (Guid id, Guid mediaAssetId, ImageHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.DeleteAsync(id, mediaAssetId, ct)))
            .WithName("deleteArtefactImage")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    public static void MapRegionEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/regions").WithTags("Regions");

        g.MapGet("/", async (string? search, RegionHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.SearchAsync(search, ct)))
            .WithName("listRegions");

        g.MapGet("/{id:guid}", async (Guid id, RegionHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.GetAsync(id, ct)))
            .WithName("getRegion")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPost("/", async Task<Results<Created<RegionDto>, Ok<RegionDto>>> (AddRegionRequest request, RegionHandler h, CancellationToken ct) =>
            {
                var result = await h.AddAsync(request, ct);
                return result.Created
                    ? TypedResults.Created($"/api/regions/{result.Region.Id}", result.Region)
                    : TypedResults.Ok(result.Region);
            })
            .WithName("addRegion")
            .ProducesValidationProblem();

        g.MapPut("/{id:guid}", async (Guid id, RenameRegionRequest request, RegionHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.RenameAsync(id, request, ct)))
            .WithName("renameRegion")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPut("/{id:guid}/parent", async (Guid id, SetRegionParentRequest request, RegionHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.SetParentAsync(id, request, ct)))
            .WithName("setRegionParent")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapDelete("/{id:guid}", async (Guid id, RegionHandler h, CancellationToken ct) =>
            {
                await h.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("deleteRegion")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public static void MapObjectTypeEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/object-types").WithTags("Object types");

        g.MapGet("/", async (string? search, ObjectTypeHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.SearchAsync(search, ct)))
            .WithName("listObjectTypes");

        g.MapGet("/{id:guid}", async (Guid id, ObjectTypeHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.GetAsync(id, ct)))
            .WithName("getObjectType")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPost("/", async Task<Results<Created<ObjectTypeDto>, Ok<ObjectTypeDto>>> (AddObjectTypeRequest request, ObjectTypeHandler h, CancellationToken ct) =>
            {
                var result = await h.AddAsync(request, ct);
                return result.Created
                    ? TypedResults.Created($"/api/object-types/{result.ObjectType.Id}", result.ObjectType)
                    : TypedResults.Ok(result.ObjectType);
            })
            .WithName("addObjectType")
            .ProducesValidationProblem();

        g.MapPut("/{id:guid}", async (Guid id, RenameObjectTypeRequest request, ObjectTypeHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.RenameAsync(id, request, ct)))
            .WithName("renameObjectType")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapDelete("/{id:guid}", async (Guid id, ObjectTypeHandler h, CancellationToken ct) =>
            {
                await h.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("deleteObjectType")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public static void MapSupportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/historical-dates/parse", (HistoricalDateInput input, HistoricalDateParseHandler h) =>
                TypedResults.Ok(h.Parse(input)))
            .WithTags("Dating")
            .WithName("parseHistoricalDate")
            .ProducesValidationProblem();

        app.MapGet("/api/catalog", (IFieldCatalog catalog) => TypedResults.Ok(catalog.Get()))
            .WithTags("Catalog")
            .WithName("getFieldCatalog");

        app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }))
            .WithTags("Health")
            .WithName("health")
            .ExcludeFromDescription();
    }
}

internal sealed record ListArtefactsParameters(
    [FromQuery(Name = "search")] string? Search,
    [FromQuery(Name = "sort"), QueryEnum(typeof(ArtefactSort))] string? Sort,
    [FromQuery(Name = "direction"), QueryEnum(typeof(SortDirection))] string? Direction,
    [FromQuery(Name = "page")] int? Page,
    [FromQuery(Name = "pageSize")] int? PageSize,
    [FromQuery(Name = "group"), QueryEnum(typeof(ArtefactGrouping))] string? Group);
