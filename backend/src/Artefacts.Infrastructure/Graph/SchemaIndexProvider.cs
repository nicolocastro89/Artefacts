using HotChocolate.Execution;

namespace Artefacts.Infrastructure.Graph;

/// <summary>The index of the running schema, built once from its SDL.</summary>
internal sealed class SchemaIndexProvider(IRequestExecutorProvider executors)
{
    private SchemaIndex? _schema;

    public async Task<SchemaIndex> GetAsync(CancellationToken cancellationToken) =>
        _schema ??= SchemaIndex.From((await executors.GetExecutorAsync(cancellationToken: cancellationToken)).Schema.ToString());
}
