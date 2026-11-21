using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using HotChocolate.Execution;
using HotChocolate.Language;

namespace Artefacts.Infrastructure.Graph;

/// <summary>
/// Works out, from a document and the schema, the type of each value the query returns (by path) and of each variable.
/// The rules are those of <c>frontend/scripts/generate-field-types.mjs</c> for REST responses; the shared cases are in
/// <c>backend/openapi/field-type-rules.json</c>.
/// </summary>
public sealed class StoredQueryDescriber(IRequestExecutorProvider executors) : IStoredQueryDescriber
{
    private SchemaIndex? _schema;

    public async Task<QueryDescriptionDto> DescribeAsync(string document, CancellationToken cancellationToken)
    {
        var schema = _schema ??= SchemaIndex.From((await executors.GetExecutorAsync(cancellationToken: cancellationToken)).Schema.ToString());
        var parsed = Utf8GraphQLParser.Parse(document);
        var operation = parsed.Definitions.OfType<OperationDefinitionNode>().Single();
        var fragments = parsed.Definitions.OfType<FragmentDefinitionNode>().ToDictionary(f => f.Name.Value);

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        new Walker(schema, fragments, fields).Selections(operation.SelectionSet, schema.QueryType, "");

        var variables = operation.VariableDefinitions.ToDictionary(
            v => v.Variable.Name.Value,
            v => new QueryVariableDto(schema.InputType(v.Type), v.Type is NonNullTypeNode && v.DefaultValue is null));
        return new QueryDescriptionDto(fields, variables);
    }

    private sealed class Walker(SchemaIndex schema, Dictionary<string, FragmentDefinitionNode> fragments, SortedDictionary<string, string> fields)
    {
        public void Selections(SelectionSetNode set, string typeName, string path)
        {
            foreach (var selection in set.Selections)
            {
                switch (selection)
                {
                    case FieldNode field when !field.Name.Value.StartsWith("__"):
                        var definition = schema.Field(typeName, field.Name.Value);
                        var key = (field.Alias ?? field.Name).Value;
                        Value(definition.Type, field.SelectionSet, path.Length == 0 ? key : $"{path}.{key}");
                        break;
                    case InlineFragmentNode inline:
                        Selections(inline.SelectionSet, inline.TypeCondition?.Name.Value ?? typeName, path);
                        break;
                    case FragmentSpreadNode spread when fragments.TryGetValue(spread.Name.Value, out var fragment):
                        Selections(fragment.SelectionSet, fragment.TypeCondition.Name.Value, path);
                        break;
                }
            }
        }

        private void Value(ITypeNode type, SelectionSetNode? selections, string path)
        {
            switch (type)
            {
                case NonNullTypeNode nonNull:
                    Value(nonNull.Type, selections, path);
                    break;
                case ListTypeNode list:
                    // As in the REST descriptions: the list, then its items under "[]" (a leaf item type replaces "list").
                    fields[path + "[]"] = "list";
                    Value(list.Type, selections, path + "[]");
                    break;
                case NamedTypeNode named:
                    if (FieldTypeRules.ObjectTypes.TryGetValue(named.Name.Value, out var special)) fields[path] = special;
                    else if (schema.IsObject(named.Name.Value) && selections is not null) Selections(selections, named.Name.Value, path);
                    else fields[path] = FieldTypeRules.Scalar(named.Name.Value);
                    break;
            }
        }
    }

    /// <summary>The object types, enums and fields of the schema, read from its SDL.</summary>
    private sealed class SchemaIndex
    {
        private readonly Dictionary<string, ObjectTypeDefinitionNode> _objects = [];
        private readonly HashSet<string> _inputs = [];
        private readonly HashSet<string> _enums = [];

        public string QueryType { get; private init; } = "Query";

        public static SchemaIndex From(string sdl)
        {
            var document = Utf8GraphQLParser.Parse(sdl);
            var index = new SchemaIndex
            {
                QueryType = document.Definitions.OfType<SchemaDefinitionNode>().SingleOrDefault()?.OperationTypes
                    .FirstOrDefault(o => o.Operation == OperationType.Query)?.Type.Name.Value ?? "Query",
            };
            foreach (var definition in document.Definitions)
            {
                switch (definition)
                {
                    case ObjectTypeDefinitionNode o: index._objects[o.Name.Value] = o; break;
                    case InputObjectTypeDefinitionNode i: index._inputs.Add(i.Name.Value); break;
                    case EnumTypeDefinitionNode e: index._enums.Add(e.Name.Value); break;
                }
            }

            return index;
        }

        public bool IsObject(string name) => _objects.ContainsKey(name);

        public FieldDefinitionNode Field(string type, string field) =>
            _objects[type].Fields.First(f => f.Name.Value == field);

        /// <summary>The type of a variable: enums are text, input objects a map, lists a list.</summary>
        public string InputType(ITypeNode type) => type switch
        {
            NonNullTypeNode nonNull => InputType(nonNull.Type),
            ListTypeNode => "list",
            NamedTypeNode named when _inputs.Contains(named.Name.Value) => "map",
            NamedTypeNode named when _enums.Contains(named.Name.Value) => "text",
            NamedTypeNode named => FieldTypeRules.Scalar(named.Name.Value),
            _ => "text",
        };
    }
}

/// <summary>The value types of result data, by GraphQL type.</summary>
public static class FieldTypeRules
{
    /// <summary>Object types that are one value of their own (not walked into).</summary>
    public static readonly IReadOnlyDictionary<string, string> ObjectTypes = new Dictionary<string, string>
    {
        ["ImageUrls"] = "image",
        ["HistoricalDate"] = "historical-date",
        ["Money"] = "money",
    };

    /// <summary>The type of a scalar; an enum, or a scalar not listed, is text.</summary>
    public static string Scalar(string graphQlType) => graphQlType switch
    {
        "UUID" => "reference",
        "Int" or "Long" or "Float" or "Decimal" or "Short" or "Byte" => "number",
        "LocalDate" or "Date" or "DateTime" or "LocalDateTime" or "DateTimeOffset" => "date",
        "Boolean" => "boolean",
        _ => "text",
    };
}
