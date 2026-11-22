using HotChocolate.Language;

namespace Artefacts.Infrastructure.Graph;

/// <summary>The object types, input types, enums and fields of the schema, read from its SDL.</summary>
internal sealed class SchemaIndex
{
    private readonly Dictionary<string, ObjectTypeDefinitionNode> _objects = [];
    private readonly Dictionary<string, InputObjectTypeDefinitionNode> _inputs = [];
    private readonly Dictionary<string, EnumTypeDefinitionNode> _enums = [];

    public string QueryType { get; private init; } = "Query";

    public IReadOnlyDictionary<string, ObjectTypeDefinitionNode> Objects => _objects;
    public IReadOnlyDictionary<string, InputObjectTypeDefinitionNode> Inputs => _inputs;
    public IReadOnlyDictionary<string, EnumTypeDefinitionNode> Enums => _enums;

    /// <summary>The fields of the query type: the roots a document can start from.</summary>
    public IReadOnlyList<FieldDefinitionNode> Roots => _objects[QueryType].Fields;

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
                case InputObjectTypeDefinitionNode i: index._inputs[i.Name.Value] = i; break;
                case EnumTypeDefinitionNode e: index._enums[e.Name.Value] = e; break;
            }
        }

        return index;
    }

    public bool IsObject(string name) => _objects.ContainsKey(name);

    public FieldDefinitionNode Field(string type, string field) =>
        _objects[type].Fields.First(f => f.Name.Value == field);

    /// <summary>The name of a type with its list and non-null wrappers removed.</summary>
    public static string NamedType(ITypeNode type) => type switch
    {
        NonNullTypeNode nonNull => NamedType(nonNull.Type),
        ListTypeNode list => NamedType(list.Type),
        NamedTypeNode named => named.Name.Value,
        _ => throw new InvalidOperationException("Unknown type node."),
    };

    /// <summary>Whether a type is a list, ignoring non-null wrappers.</summary>
    public static bool IsList(ITypeNode type) => type switch
    {
        NonNullTypeNode nonNull => IsList(nonNull.Type),
        ListTypeNode => true,
        _ => false,
    };

    /// <summary>The type of a variable: enums are text, input objects a map, lists a list.</summary>
    public string InputType(ITypeNode type) => type switch
    {
        NonNullTypeNode nonNull => InputType(nonNull.Type),
        ListTypeNode => "list",
        NamedTypeNode named when _inputs.ContainsKey(named.Name.Value) => "map",
        NamedTypeNode named when _enums.ContainsKey(named.Name.Value) => "text",
        NamedTypeNode named => FieldTypeRules.Scalar(named.Name.Value),
        _ => "text",
    };
}
