using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Common;
using Artefacts.Domain.Queries;

namespace Artefacts.Application.Queries;

/// <summary>Stored GraphQL queries: the system queries (read-only) and the user queries people save.</summary>
public sealed class StoredQueryHandler(IStoredQueryRepository queries, IStoredQueryValidator validator, IStoredQueryDescriber describer, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<IReadOnlyList<StoredQuerySummary>> ListAsync(CancellationToken ct) =>
        (await queries.ListAsync(ct)).Select(q => new StoredQuerySummary(q.Id, q.Name, Kind(q), q.UpdatedAt)).ToList();

    public async Task<StoredQueryDto> GetAsync(string id, CancellationToken ct) => ToDto(await LoadAsync(id, ct));

    public async Task<QueryDescriptionDto> DescribeAsync(string id, CancellationToken ct) =>
        await describer.DescribeAsync((await LoadAsync(id, ct)).Document, ct);

    public async Task<StoredQueryDto> CreateAsync(CreateStoredQueryRequest request, CancellationToken ct)
    {
        await CheckDocumentAsync(request.Document, ct);
        var query = StoredQuery.CreateUser(request.Name, request.Document!, clock.GetUtcNow());
        queries.Add(query);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(query);
    }

    public async Task<StoredQueryDto> UpdateAsync(string id, ChangeStoredQueryRequest request, CancellationToken ct)
    {
        var query = await LoadAsync(id, ct);
        query.EnsureUserQuery();
        if (request.Document is not null) await CheckDocumentAsync(request.Document, ct);
        query.Change(request.Name, request.Document, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(query);
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        var query = await LoadAsync(id, ct);
        query.EnsureUserQuery();
        queries.Remove(query);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>A new user query with the document of any stored query, system queries included.</summary>
    public async Task<StoredQueryDto> DuplicateAsync(string id, DuplicateStoredQueryRequest request, CancellationToken ct)
    {
        var copy = (await LoadAsync(id, ct)).Duplicate(request.Name, clock.GetUtcNow());
        queries.Add(copy);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(copy);
    }

    /// <summary>Rejects a document that cannot be stored, with each problem and its line and column under <c>document</c>.</summary>
    private async Task CheckDocumentAsync(string? document, CancellationToken ct)
    {
        var problems = await validator.ValidateAsync(document, ct);
        if (problems.Count == 0) return;
        throw new DomainValidationException(new Dictionary<string, string[]>
        {
            ["document"] = problems.Select(p => p.Line is { } line ? $"{p.Message} (line {line}, column {p.Column})" : p.Message).ToArray(),
        });
    }

    private async Task<StoredQuery> LoadAsync(string id, CancellationToken ct) =>
        await queries.GetAsync(id, ct) ?? throw new NotFoundException("Stored query", id);

    private static StoredQueryKind Kind(StoredQuery q) => q.IsSystem ? StoredQueryKind.System : StoredQueryKind.User;

    private static StoredQueryDto ToDto(StoredQuery q) => new(q.Id, q.Name, Kind(q), q.Document, q.UpdatedAt);
}
