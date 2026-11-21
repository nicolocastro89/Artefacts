namespace Artefacts.Application.Contracts;

public enum StoredQueryKind
{
    System = 0,
    User = 1,
}

/// <summary>A stored query in the list: system queries ship with the application, user queries are saved by users.</summary>
public sealed record StoredQuerySummary(string Id, string Name, StoredQueryKind Kind, DateTimeOffset UpdatedAt);

public sealed record StoredQueryDto(string Id, string Name, StoredQueryKind Kind, string Document, DateTimeOffset UpdatedAt);

public sealed record CreateStoredQueryRequest(string? Name, string? Document);

/// <summary>Renames the query and/or replaces its document; a value left out is kept.</summary>
public sealed record ChangeStoredQueryRequest(string? Name = null, string? Document = null);

public sealed record DuplicateStoredQueryRequest(string? Name);
