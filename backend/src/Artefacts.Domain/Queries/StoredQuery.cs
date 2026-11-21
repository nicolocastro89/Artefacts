using System.Security.Cryptography;
using Artefacts.Domain.Common;
using Artefacts.Domain.Languages;

namespace Artefacts.Domain.Queries;

/// <summary>
/// A named GraphQL query document kept as data (aggregate root). System queries ship with the application, keep a
/// readable identifier and cannot be changed through the API; user queries get a generated identifier. Whether the
/// document is valid against the schema is checked by whoever saves it, which needs the schema.
/// </summary>
public sealed class StoredQuery
{
    public const int MaxNameLength = 200;
    public const string UserIdPrefix = "q-";

    /// <summary>The slug of a system query (<c>artefact-list</c>) or <c>q-</c> and a short random text of a user query.</summary>
    public string Id { get; private set; } = default!;

    public string Name { get; private set; } = default!;

    /// <summary>Lower case, whitespace collapsed: unique across stored queries.</summary>
    public string NormalizedName { get; private set; } = default!;

    public string Document { get; private set; } = default!;
    public bool IsSystem { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // For EF Core.
    private StoredQuery()
    {
    }

    public static StoredQuery CreateUser(string? name, string document, DateTimeOffset now)
    {
        var query = new StoredQuery { Id = UserIdPrefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(5)), CreatedAt = now };
        query.Set(name, document, now);
        return query;
    }

    public static StoredQuery CreateSystem(string id, string name, string document, DateTimeOffset now)
    {
        var query = new StoredQuery { Id = id, IsSystem = true, CreatedAt = now };
        query.Set(name, document, now);
        return query;
    }

    /// <summary>A new user query with this query's document.</summary>
    public StoredQuery Duplicate(string? name, DateTimeOffset now) => CreateUser(name, Document, now);

    /// <summary>Renames the query and/or replaces its document; a value left null is kept. Not for system queries.</summary>
    public void Change(string? name, string? document, DateTimeOffset now)
    {
        EnsureUserQuery();
        Set(name ?? Name, document ?? Document, now);
    }

    /// <summary>Brings a system query to the name and document of the running release.</summary>
    public void ReplaceSystem(string name, string document, DateTimeOffset now)
    {
        if (!IsSystem) throw new InvalidOperationException($"Query '{Id}' is not a system query.");
        if (name != Name || document != Document) Set(name, document, now);
    }

    /// <summary>Throws the conflict that tells a client system queries are read-only.</summary>
    public void EnsureUserQuery()
    {
        if (IsSystem) throw new ConflictException("system_query", "System queries cannot be changed or deleted; duplicate it to change a copy.");
    }

    private void Set(string? name, string document, DateTimeOffset now)
    {
        Name = Guard.Required(name, "name", MaxNameLength);
        NormalizedName = LocalizedName.Normalize(Name);
        Document = document;
        UpdatedAt = now;
    }
}
