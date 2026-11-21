using Artefacts.Application.Contracts;
using Artefacts.Application.Queries;

namespace Artefacts.Api;

internal static class QueryEndpoints
{
    public static void MapQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/queries").WithTags("Queries");

        g.MapGet("/", async (StoredQueryHandler h, CancellationToken ct) => TypedResults.Ok(await h.ListAsync(ct)))
            .WithName("listQueries")
            .WithSummary("The stored queries: system queries first, then user queries by name.");

        g.MapGet("/{id}", async (string id, StoredQueryHandler h, CancellationToken ct) => TypedResults.Ok(await h.GetAsync(id, ct)))
            .WithName("getQuery")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapGet("/{id}/description", async (string id, StoredQueryHandler h, CancellationToken ct) => TypedResults.Ok(await h.DescribeAsync(id, ct)))
            .WithName("describeQuery")
            .WithSummary("The type of every value the query returns, by path, and of its variables, so bindings can be checked.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        g.MapPost("/", async (CreateStoredQueryRequest request, StoredQueryHandler h, CancellationToken ct) =>
            {
                var query = await h.CreateAsync(request, ct);
                return TypedResults.Created($"/api/queries/{query.Id}", query);
            })
            .WithName("createQuery")
            .WithSummary("Saves a user query. The document must hold one query operation that is valid against the GraphQL schema.")
            .ProducesValidationProblem();

        g.MapPut("/{id}", async (string id, ChangeStoredQueryRequest request, StoredQueryHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.UpdateAsync(id, request, ct)))
            .WithName("updateQuery")
            .WithSummary("Renames a user query and/or replaces its document. System queries cannot be changed.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        g.MapDelete("/{id}", async (string id, StoredQueryHandler h, CancellationToken ct) =>
            {
                await h.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("deleteQuery")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        g.MapPost("/{id}/duplicate", async (string id, DuplicateStoredQueryRequest request, StoredQueryHandler h, CancellationToken ct) =>
            {
                var copy = await h.DuplicateAsync(id, request, ct);
                return TypedResults.Created($"/api/queries/{copy.Id}", copy);
            })
            .WithName("duplicateQuery")
            .WithSummary("Copies any stored query, system queries included, into a new user query.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
