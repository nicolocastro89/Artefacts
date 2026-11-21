using Artefacts.Application.Contracts;
using Artefacts.Application.Pages;

namespace Artefacts.Api;

internal static class PageEndpoints
{
    public static void MapPageEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/pages").WithTags("Pages");

        g.MapGet("/", async (bool? navigation, StoredPageHandler h, CancellationToken ct) => TypedResults.Ok(await h.ListAsync(navigation == true, ct)))
            .WithName("listPages")
            .WithSummary("The stored pages: system pages first, then user pages by name. With navigation=true, only the pages shown in navigation.");

        g.MapGet("/{id}", async (string id, StoredPageHandler h, CancellationToken ct) => TypedResults.Ok(await h.GetAsync(id, ct)))
            .WithName("getPage")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapGet("/by-slug/{slug}", async (string slug, StoredPageHandler h, CancellationToken ct) => TypedResults.Ok(await h.GetBySlugAsync(slug, ct)))
            .WithName("getPageBySlug")
            .WithSummary("A page by the slug in its address.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPost("/", async (CreateStoredPageRequest request, StoredPageHandler h, CancellationToken ct) =>
            {
                var page = await h.CreateAsync(request, ct);
                return TypedResults.Created($"/api/pages/{page.Id}", page);
            })
            .WithName("createPage")
            .WithSummary("Saves a user page. The definition must have the shape of a page and name only stored queries that exist.")
            .ProducesValidationProblem();

        g.MapPut("/{id}", async (string id, ChangeStoredPageRequest request, StoredPageHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.UpdateAsync(id, request, ct)))
            .WithName("updatePage")
            .WithSummary("Changes a user page; values left out are kept. System pages cannot be changed.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        g.MapDelete("/{id}", async (string id, StoredPageHandler h, CancellationToken ct) =>
            {
                await h.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("deletePage")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        g.MapPost("/{id}/duplicate", async (string id, DuplicateStoredPageRequest request, StoredPageHandler h, CancellationToken ct) =>
            {
                var copy = await h.DuplicateAsync(id, request, ct);
                return TypedResults.Created($"/api/pages/{copy.Id}", copy);
            })
            .WithName("duplicatePage")
            .WithSummary("Copies any stored page, system pages included, into a new user page.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
