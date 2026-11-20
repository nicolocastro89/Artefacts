using Artefacts.Application.Abstractions;
using Artefacts.Application.Languages;

namespace Artefacts.Api;

/// <summary>
/// The preferred content languages of the current HTTP request (the <c>lang</c> parameter, else Accept-Language,
/// then the fallback order). Set once per request by <see cref="RequestLanguagesMiddleware"/>.
/// </summary>
internal sealed class RequestLanguages : ILanguagePreferences
{
    private IReadOnlyList<string>? _current;

    public IReadOnlyList<string> Current =>
        _current ?? throw new InvalidOperationException("Request languages are resolved by RequestLanguagesMiddleware.");

    public void Set(IReadOnlyList<string> languages) => _current = languages;
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
