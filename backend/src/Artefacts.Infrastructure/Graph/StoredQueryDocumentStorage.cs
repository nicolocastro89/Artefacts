using HotChocolate.Execution;
using HotChocolate.Language;
using HotChocolate.PersistedOperations;

namespace Artefacts.Infrastructure.Graph;

/// <summary>Finds the document of a stored query by its identifier. Registered as a singleton.</summary>
public interface IStoredQueryDocuments
{
    ValueTask<string?> FindAsync(string id, CancellationToken ct);
}

/// <summary>
/// Lets clients run a stored query by sending its identifier (<c>{ "id": "artefact-list", "variables": … }</c>).
/// Stored queries are saved through <c>/api/queries</c>, never through the GraphQL transport.
/// </summary>
internal sealed class StoredQueryDocumentStorage(IStoredQueryDocuments documents) : IOperationDocumentStorage
{
    public async ValueTask<IOperationDocument?> TryReadAsync(OperationDocumentId documentId, CancellationToken cancellationToken)
    {
        var text = await documents.FindAsync(documentId.Value, cancellationToken);
        return text is null ? null : new OperationDocumentSourceText(text);
    }

    public ValueTask SaveAsync(OperationDocumentId documentId, IOperationDocument document, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Stored queries are saved through /api/queries.");
}
