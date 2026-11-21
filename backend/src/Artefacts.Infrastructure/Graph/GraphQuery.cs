using Artefacts.Application.Abstractions;
using Artefacts.Infrastructure.Read;
using HotChocolate;
using Microsoft.EntityFrameworkCore;

namespace Artefacts.Infrastructure.Graph;

/// <summary>The root query type: read-only access to the collection through the read views.</summary>
public sealed class GraphQuery
{
    /// <summary>The reader's first preferred language: the language names and descriptions are asked for in.</summary>
    public string Language([Service] ILanguagePreferences preferences) => preferences.Current.FirstOrDefault() ?? "";

    /// <summary>One artefact, or null when there is none with this identifier.</summary>
    public async Task<ArtefactNode?> GetArtefact(Guid id, [Service] ReadDbContext db, CancellationToken cancellationToken)
    {
        var row = await db.Artefacts.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        return row is null ? null : new ArtefactNode(row);
    }
}

/// <summary>An artefact as read from <c>v_artefact</c>.</summary>
public sealed class ArtefactNode(ArtefactRow row)
{
    internal ArtefactRow Row { get; } = row;

    public Guid Id => Row.Id;
    public string InventoryNumber => Row.InventoryNumber;
    public string Title => Row.Title;
}
