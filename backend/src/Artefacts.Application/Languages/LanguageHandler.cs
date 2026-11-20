using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;

namespace Artefacts.Application.Languages;

/// <summary>Manages the content languages and their fallback order.</summary>
public sealed class LanguageHandler(ILanguageRepository languages, IContentLanguages cache, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<LanguageDto>> ListAsync(CancellationToken ct) => cache.ListAsync(ct);

    /// <summary>Adds a language at the end of the fallback order.</summary>
    public async Task<LanguageDto> AddAsync(AddLanguageRequest request, CancellationToken ct)
    {
        var all = await languages.ListAsync(ct);
        var language = Language.Create(request.Code, request.Name, all.Count == 0 ? 1 : all.Max(l => l.Position) + 1);
        if (all.Any(l => l.Code == language.Code))
            throw new DomainValidationException("code", $"\"{language.Code}\" is already a content language.");

        languages.Add(language);
        await unitOfWork.SaveChangesAsync(ct);
        cache.Invalidate();
        return ToDto(language);
    }

    /// <summary>Sets the fallback order; the request must list every content language exactly once.</summary>
    public async Task<IReadOnlyList<LanguageDto>> SetOrderAsync(SetLanguageOrderRequest request, CancellationToken ct)
    {
        var all = await languages.ListAsync(ct);
        var codes = (request.Codes ?? []).Select(c => c.Trim().ToLowerInvariant()).ToList();
        if (codes.Count != all.Count || codes.Distinct().Count() != codes.Count || !all.All(l => codes.Contains(l.Code)))
            throw new DomainValidationException("codes", "List every content language exactly once.");

        foreach (var language in all) language.MoveTo(codes.IndexOf(language.Code) + 1);
        await unitOfWork.SaveChangesAsync(ct);
        cache.Invalidate();
        return all.OrderBy(l => l.Position).Select(ToDto).ToList();
    }

    public async Task DeleteAsync(string code, CancellationToken ct)
    {
        var language = await languages.GetAsync(code.Trim().ToLowerInvariant(), ct) ?? throw new NotFoundException("Language", code);
        if (await languages.InUseAsync(language.Code, ct))
            throw new ConflictException("language_in_use", $"{language.Name} is used by names or descriptions and cannot be removed.");

        languages.Remove(language);
        await unitOfWork.SaveChangesAsync(ct);
        cache.Invalidate();
    }

    private static LanguageDto ToDto(Language l) => new(l.Code, l.Name, l.Position);
}
