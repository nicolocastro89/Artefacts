using Artefacts.Application.Abstractions;
using Artefacts.Application.Artefacts;
using Artefacts.Application.Contracts;
using Artefacts.Application.Languages;
using Artefacts.Application.Lists;
using Artefacts.Application.Media;
using Artefacts.Application.ObjectTypes;
using Artefacts.Application.Parties;
using Artefacts.Application.Regions;
using Artefacts.Domain.Lists;
using Artefacts.Domain.Parties;
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

        g.MapPut("/{id:guid}", async (Guid id, SetNamesRequest request, RegionHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.SetNamesAsync(id, request, ct)))
            .WithName("setRegionNames")
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

        g.MapPut("/{id:guid}", async (Guid id, SetNamesRequest request, ObjectTypeHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.SetNamesAsync(id, request, ct)))
            .WithName("setObjectTypeNames")
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

    /// <summary>The URL segment of each managed list.</summary>
    private static readonly Dictionary<string, ListKind> ListSlugs = new()
    {
        ["dynasties"] = ListKind.Dynasty,
        ["collection-groups"] = ListKind.CollectionGroup,
        ["storage-locations"] = ListKind.StorageLocation,
    };

    public static void MapListEndpoints(this IEndpointRouteBuilder app)
    {
        // Unknown lists do not match the route and get 404.
        var g = app.MapGroup("/api/lists/{list:regex(^(dynasties|collection-groups|storage-locations)$)}").WithTags("Lists");

        g.MapGet("/", async (string list, string? search, ListHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.SearchAsync(ListSlugs[list], search, ct)))
            .WithName("listListEntries")
            .WithSummary("Entries of a list whose name in any language contains the search text, with usage counts.");

        g.MapGet("/{id:guid}", async (string list, Guid id, ListHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.GetAsync(ListSlugs[list], id, ct)))
            .WithName("getListEntry")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPost("/", async Task<Results<Created<ReferenceEntryDto>, Ok<ReferenceEntryDto>>> (
                string list, ReferenceEntryNamesRequest request, ListHandler h, CancellationToken ct) =>
            {
                var result = await h.AddAsync(ListSlugs[list], request, ct);
                return result.Created
                    ? TypedResults.Created($"/api/lists/{list}/{result.Entry.Id}", result.Entry)
                    : TypedResults.Ok(result.Entry);
            })
            .WithName("addListEntry")
            .WithSummary("Adds an entry, or returns the existing entry with the same name.")
            .ProducesValidationProblem();

        g.MapPut("/{id:guid}", async (string list, Guid id, ReferenceEntryNamesRequest request, ListHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.SetNamesAsync(ListSlugs[list], id, request, ct)))
            .WithName("setListEntryNames")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapDelete("/{id:guid}", async (string list, Guid id, ListHandler h, CancellationToken ct) =>
            {
                await h.DeleteAsync(ListSlugs[list], id, ct);
                return TypedResults.NoContent();
            })
            .WithName("deleteListEntry")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public static void MapPartyEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/parties").WithTags("Parties");

        g.MapGet("/", async Task<Results<Ok<IReadOnlyList<PartyDto>>, ValidationProblem>> (
                string? search, [QueryEnum(typeof(PartyKind))] string? kind, PartyHandler h, CancellationToken ct) =>
            {
                if (!QueryEnum.TryParse(kind, PartyKind.Person, out var parsed))
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["kind"] = [$"Must be one of: {string.Join(", ", QueryEnum.AllowedValues<PartyKind>())}."],
                    });
                return TypedResults.Ok(await h.SearchAsync(search, string.IsNullOrWhiteSpace(kind) ? null : parsed, ct));
            })
            .WithName("listParties")
            .WithSummary("Parties whose name contains the search text, optionally of one kind, with source and holder counts.");

        g.MapGet("/{id:guid}", async (Guid id, PartyHandler h, CancellationToken ct) => TypedResults.Ok(await h.GetAsync(id, ct)))
            .WithName("getParty")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPost("/", async Task<Results<Created<PartyDto>, Ok<PartyDto>>> (PartyRequest request, PartyHandler h, CancellationToken ct) =>
            {
                var result = await h.AddAsync(request, ct);
                return result.Created ? TypedResults.Created($"/api/parties/{result.Party.Id}", result.Party) : TypedResults.Ok(result.Party);
            })
            .WithName("addParty")
            .WithSummary("Adds a party, or returns the existing party with the same name.")
            .ProducesValidationProblem();

        g.MapPut("/{id:guid}", async (Guid id, PartyRequest request, PartyHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.UpdateAsync(id, request, ct)))
            .WithName("updateParty")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapDelete("/{id:guid}", async (Guid id, PartyHandler h, CancellationToken ct) =>
            {
                await h.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("deleteParty")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public static void MapLanguageEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/languages").WithTags("Languages");

        g.MapGet("/", async (LanguageHandler h, CancellationToken ct) => TypedResults.Ok(await h.ListAsync(ct)))
            .WithName("listLanguages");

        g.MapPost("/", async (AddLanguageRequest request, LanguageHandler h, CancellationToken ct) =>
            {
                var language = await h.AddAsync(request, ct);
                return TypedResults.Created($"/api/languages/{language.Code}", language);
            })
            .WithName("addLanguage")
            .ProducesValidationProblem();

        g.MapPut("/order", async (SetLanguageOrderRequest request, LanguageHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.SetOrderAsync(request, ct)))
            .WithName("setLanguageOrder")
            .ProducesValidationProblem();

        g.MapDelete("/{code}", async (string code, LanguageHandler h, CancellationToken ct) =>
            {
                await h.DeleteAsync(code, ct);
                return TypedResults.NoContent();
            })
            .WithName("deleteLanguage")
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
