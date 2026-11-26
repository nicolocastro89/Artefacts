using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Artefacts.Api;

/// <summary>
/// Serves the built frontend (the output of <c>npm run build</c>) from the API, so an installed copy is a single origin
/// the way the Vite dev server's proxy makes development one. The files come from <c>Frontend:Root</c>, or from
/// <c>wwwroot</c> where the container image puts them; when neither exists (development, tests) nothing is mapped.
/// </summary>
public static class FrontendHosting
{
    public static WebApplication MapFrontend(this WebApplication app)
    {
        var root = app.Configuration["Frontend:Root"] ?? app.Environment.WebRootPath;
        if (string.IsNullOrEmpty(root) || !File.Exists(Path.Combine(root, "index.html")))
            return app;

        var files = new PhysicalFileProvider(Path.GetFullPath(root));

        // Vite fingerprints everything under assets/, so those never change; anything else (index.html, public/ files)
        // must be revalidated so a new version is picked up as soon as it is installed.
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files, OnPrepareResponse = SetCaching });

        // Client-side routes (no file extension) get index.html; unknown API paths stay 404 rather than a page.
        app.MapFallback("/api/{**path}", () => Results.NotFound()).ExcludeFromDescription();
        app.MapFallbackToFile("{**path:nonfile}", "index.html", new StaticFileOptions
        {
            FileProvider = files,
            OnPrepareResponse = SetCaching,
        });
        return app;
    }

    private static void SetCaching(StaticFileResponseContext context) =>
        context.Context.Response.Headers.CacheControl = context.Context.Request.Path.StartsWithSegments("/assets")
            ? "public, max-age=31536000, immutable"
            : "no-cache";
}
