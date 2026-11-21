using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Artefacts.Infrastructure.Graph;

/// <summary>Finds the document of a stored query by its identifier. Registered as a singleton.</summary>
public interface IStoredQueryDocuments
{
    ValueTask<string?> FindAsync(string id, CancellationToken ct);
}

/// <summary>
/// Runs a stored query sent by its identifier (<c>{ "id": "artefact-list", "variables": … }</c>): the identifier is
/// replaced, on every request, by the stored document, so the server handles it as any ad-hoc document. The server's
/// caches are keyed by a document's text (not by an identifier), so a query that was edited is run as edited and one
/// that was deleted is not found. Bodies that carry their own query text are left alone.
/// </summary>
public static class StoredQueryMiddleware
{
    public static IApplicationBuilder UseStoredQueries(this IApplicationBuilder app, PathString path) =>
        app.UseWhen(context => context.Request.Path.StartsWithSegments(path) && HttpMethods.IsPost(context.Request.Method), branch =>
            branch.Use(async (context, next) =>
            {
                context.Request.EnableBuffering();
                JsonObject? body;
                try
                {
                    body = await JsonNode.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted) as JsonObject;
                }
                catch (JsonException)
                {
                    body = null;
                }

                context.Request.Body.Position = 0;
                if (body is null || body.ContainsKey("query") || body["id"]?.GetValue<string>() is not { } id)
                {
                    await next();
                    return;
                }

                var document = await context.RequestServices.GetRequiredService<IStoredQueryDocuments>().FindAsync(id, context.RequestAborted);
                if (document is null)
                {
                    context.Response.ContentType = "application/graphql-response+json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        errors = new[] { new { message = $"The stored query '{id}' does not exist.", extensions = new { code = "STORED_QUERY_NOT_FOUND" } } },
                    }, context.RequestAborted);
                    return;
                }

                body.Remove("id");
                body["query"] = document;
                var bytes = JsonSerializer.SerializeToUtf8Bytes(body);
                context.Request.Body = new MemoryStream(bytes);
                context.Request.ContentLength = bytes.Length;
                await next();
            }));
}
