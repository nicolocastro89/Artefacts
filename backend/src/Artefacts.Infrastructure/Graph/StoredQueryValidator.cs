using Artefacts.Application.Abstractions;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Language;
using HotChocolate.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Artefacts.Infrastructure.Graph;

/// <summary>
/// Checks that a document can be stored: it parses, holds exactly one operation and that operation is a query, and it
/// is valid against the schema (fields, arguments, types, and the depth limit).
/// </summary>
public sealed class StoredQueryValidator(IRequestExecutorProvider executors) : IStoredQueryValidator
{
    public async Task<IReadOnlyList<QueryProblem>> ValidateAsync(string? document, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(document)) return [new QueryProblem("The document is empty.", null, null)];

        DocumentNode parsed;
        try
        {
            parsed = Utf8GraphQLParser.Parse(document);
        }
        catch (SyntaxException e)
        {
            return [new QueryProblem(e.Message, e.Line, e.Column)];
        }

        var operations = parsed.Definitions.OfType<OperationDefinitionNode>().ToList();
        if (operations.Count != 1)
            return [new QueryProblem($"The document must hold exactly one operation, not {operations.Count}.", null, null)];
        if (operations[0].Operation != OperationType.Query)
            return [new QueryProblem("The operation must be a query.", operations[0].Location?.Line, operations[0].Location?.Column)];

        var executor = await executors.GetExecutorAsync(cancellationToken: cancellationToken);
        var result = executor.Schema.Services.GetRequiredService<DocumentValidator>().Validate(executor.Schema, parsed);
        return result.HasErrors
            ? result.Errors.Select(e => new QueryProblem(e.Message, e.Locations?.FirstOrDefault().Line, e.Locations?.FirstOrDefault().Column)).ToList()
            : [];
    }
}
