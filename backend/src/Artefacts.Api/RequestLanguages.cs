using Artefacts.Application.Abstractions;
using Artefacts.Application.Languages;

namespace Artefacts.Api;

/// <summary>
/// The preferred content languages of the current HTTP request (the <c>lang</c> parameter, else Accept-Language,
/// then the fallback order). Set once per request by <see cref="RequestLanguagesMiddleware"/>; work outside a request
/// (the import worker) reads in the fallback order. GraphQL executes each request in a scope of its own, so the
/// languages are also kept in the request's items, where a new instance in that scope finds them.
/// </summary>
internal sealed class RequestLanguages(IContentLanguages contentLanguages, IHttpContextAccessor httpContext) : ILanguagePreferences
{
    internal const string ItemKey = "Artefacts.RequestLanguages";

    private IReadOnlyList<string>? _current;

    public IReadOnlyList<string> Current =>
        _current ??= httpContext.HttpContext?.Items[ItemKey] as IReadOnlyList<string>
                     ?? contentLanguages.ListAsync(CancellationToken.None).GetAwaiter().GetResult().Select(l => l.Code).ToList();

    public void Set(IReadOnlyList<string> languages)
    {
        _current = languages;
        if (httpContext.HttpContext is { } context) context.Items[ItemKey] = languages;
    }
}

internal static class RequestLanguagesMiddleware
{
    public static IApplicationBuilder UseRequestLanguages(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var content = await context.RequestServices.GetRequiredService<IContentLanguages>().ListAsync(context.RequestAborted);
            var languages = LanguagePreference.Resolve(
                context.Request.Query["lang"].FirstOrDefault(),
                context.Request.Headers.AcceptLanguage.ToString(),
                content.Select(l => l.Code).ToList());
            context.RequestServices.GetRequiredService<RequestLanguages>().Set(languages);
            // Responses depend on the reader's language.
            context.Response.Headers.Append("Vary", "Accept-Language");
            await next();
        });
}
