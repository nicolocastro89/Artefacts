using Artefacts.Application.Abstractions;
using Artefacts.Domain.Parties;
using Artefacts.Infrastructure.Read;
using GreenDonut;
using Microsoft.EntityFrameworkCore;

namespace Artefacts.Infrastructure.Graph;

/// <summary>An identifier with the name the database resolved for it (null when the record has no name).</summary>
internal sealed record NameRow(Guid Id, string? Name);

/// <summary>A party as read for a reference: identifier, name and kind.</summary>
internal sealed record PartyRow(Guid Id, string Name, string Kind);

/// <summary>
/// Resolves the names of many linked records at once, in the request's languages, with one query per batch. Each
/// subclass names the SQL function (<c>Read/Views/localized_names.sql</c>) that resolves its record kind.
/// </summary>
public abstract class NamesDataLoader(
    IDbContextFactory<ReadDbContext> dbFactory,
    ILanguagePreferences preferences,
    IBatchScheduler batchScheduler,
    DataLoaderOptions options) : BatchDataLoader<Guid, string?>(batchScheduler, options)
{
    // Read when the loader is created, inside the request, rather than when a batch runs.
    private readonly string[] _languages = [.. preferences.Current];

    /// <summary>The SQL for one batch: columns <c>id</c> and <c>name</c> for the identifiers in <c>{0}</c>, languages in <c>{1}</c>.</summary>
    protected abstract FormattableString Sql(Guid[] ids, string[] languages);

    protected override async Task<IReadOnlyDictionary<Guid, string?>> LoadBatchAsync(
        IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Database.SqlQuery<NameRow>(Sql([.. keys], _languages)).ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Id, r => r.Name);
    }

    /// <summary>The record as a named reference, or null when there is no identifier or the record has no name.</summary>
    public async Task<NamedRef?> RefAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id is not { } key) return null;
        var name = await LoadAsync(key, cancellationToken);
        return name is null ? null : new NamedRef(key, name);
    }
}

public sealed class RegionNamesDataLoader(
    IDbContextFactory<ReadDbContext> dbFactory, ILanguagePreferences preferences, IBatchScheduler batchScheduler, DataLoaderOptions options)
    : NamesDataLoader(dbFactory, preferences, batchScheduler, options)
{
    protected override FormattableString Sql(Guid[] ids, string[] languages) =>
        $"SELECT i AS id, region_name(i, {languages}) AS name FROM unnest({ids}) AS i";
}

public sealed class ObjectTypeNamesDataLoader(
    IDbContextFactory<ReadDbContext> dbFactory, ILanguagePreferences preferences, IBatchScheduler batchScheduler, DataLoaderOptions options)
    : NamesDataLoader(dbFactory, preferences, batchScheduler, options)
{
    protected override FormattableString Sql(Guid[] ids, string[] languages) =>
        $"SELECT i AS id, object_type_name(i, {languages}) AS name FROM unnest({ids}) AS i";
}

/// <summary>Names of dynasties, collection groups and storage locations.</summary>
public sealed class EntryNamesDataLoader(
    IDbContextFactory<ReadDbContext> dbFactory, ILanguagePreferences preferences, IBatchScheduler batchScheduler, DataLoaderOptions options)
    : NamesDataLoader(dbFactory, preferences, batchScheduler, options)
{
    protected override FormattableString Sql(Guid[] ids, string[] languages) =>
        $"SELECT i AS id, reference_entry_name(i, {languages}) AS name FROM unnest({ids}) AS i";
}

/// <summary>Resolves the parties (acquisition sources and holders) of many artefacts at once.</summary>
public sealed class PartiesDataLoader(
    IDbContextFactory<ReadDbContext> dbFactory, IBatchScheduler batchScheduler, DataLoaderOptions options)
    : BatchDataLoader<Guid, PartyRef>(batchScheduler, options)
{
    protected override async Task<IReadOnlyDictionary<Guid, PartyRef>> LoadBatchAsync(
        IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ids = keys.ToArray();
        var rows = await db.Database
            .SqlQuery<PartyRow>($"SELECT p.id AS id, p.name AS name, p.kind AS kind FROM parties p WHERE p.id = ANY({ids})")
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Id, r => new PartyRef(r.Id, r.Name, Enum.Parse<PartyKind>(r.Kind)));
    }

    /// <summary>The party, or null when there is no identifier.</summary>
    public async Task<PartyRef?> RefAsync(Guid? id, CancellationToken cancellationToken) =>
        id is { } key ? await LoadAsync(key, cancellationToken) : null;
}
