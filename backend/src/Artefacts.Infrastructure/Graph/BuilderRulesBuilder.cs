using System.Text.RegularExpressions;
using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using HotChocolate.Language;
using Artefacts.Infrastructure.Read;
using Microsoft.Extensions.Options;

namespace Artefacts.Infrastructure.Graph;

/// <summary>
/// Reads, from the running schema and the field catalog, what a query builder may offer: the roots and their arguments,
/// the fields of every object type, the fields a root can be filtered and ordered on with the operators of each, the enums
/// and the limits. Nothing is kept by hand, so the rules follow the schema.
/// </summary>
internal sealed partial class BuilderRulesBuilder(SchemaIndexProvider schemas, IFieldCatalog catalog, IOptions<GraphOptions> options) : IBuilderRulesProvider
{
    public async Task<BuilderRulesDto> GetAsync(CancellationToken cancellationToken)
    {
        var schema = await schemas.GetAsync(cancellationToken);
        var labels = ColumnLabels();

        var roots = schema.Roots.Select(root => Root(schema, root, labels)).ToList();
        var types = schema.Objects.Where(o => o.Key != schema.QueryType)
            .OrderBy(o => o.Key, StringComparer.Ordinal)
            .ToDictionary(o => o.Key, o => new BuilderTypeDto([.. o.Value.Fields.Select(f => Field(schema, f))]));
        var enums = schema.Enums.OrderBy(e => e.Key, StringComparer.Ordinal)
            .ToDictionary(e => e.Key, e => (IReadOnlyList<string>)[.. e.Value.Values.Select(v => v.Name.Value)]);

        var limits = options.Value;
        return new BuilderRulesDto(roots, types, enums,
            new BuilderLimitsDto(ArtefactListSteps.MaxPageSize, limits.MaxDepth, limits.MaxFieldCost, limits.MaxTypeCost));
    }

    private static BuilderRootDto Root(SchemaIndex schema, FieldDefinitionNode root, Dictionary<string, string> labels)
    {
        var arguments = root.Arguments.Select(a => Argument(schema, a)).ToList();
        var where = root.Arguments.FirstOrDefault(a => a.Name.Value == "where");
        var order = root.Arguments.FirstOrDefault(a => a.Name.Value == "order");
        return new BuilderRootDto(
            root.Name.Value, SchemaIndex.NamedType(root.Type), SchemaIndex.IsList(root.Type), arguments,
            where is null ? null : FilterFields(schema, SchemaIndex.NamedType(where.Type), labels),
            order is null ? null : SortFields(schema, SchemaIndex.NamedType(order.Type), labels));
    }

    private static BuilderArgumentDto Argument(SchemaIndex schema, InputValueDefinitionNode argument)
    {
        var type = SchemaIndex.NamedType(argument.Type);
        var kind = schema.Inputs.ContainsKey(type) ? "input" : schema.Enums.ContainsKey(type) ? "enum" : "scalar";
        return new BuilderArgumentDto(
            argument.Name.Value, type, kind, SchemaIndex.IsList(argument.Type), argument.Type is NonNullTypeNode && argument.DefaultValue is null,
            argument.DefaultValue?.ToString());
    }

    private static BuilderFieldDto Field(SchemaIndex schema, FieldDefinitionNode field)
    {
        var type = SchemaIndex.NamedType(field.Type);
        var special = FieldTypeRules.ObjectTypes.TryGetValue(type, out var value) ? value : null;
        var isObject = schema.IsObject(type);
        var kind = isObject ? "object" : schema.Enums.ContainsKey(type) ? "enum" : "scalar";
        var valueType = isObject ? special : FieldTypeRules.Scalar(type);
        var sensitivity = field.Directives.FirstOrDefault(d => d.Name.Value == "sensitive")?.Arguments
            .FirstOrDefault(a => a.Name.Value == "tag")?.Value is StringValueNode tag ? tag.Value : null;
        return new BuilderFieldDto(field.Name.Value, type, kind, SchemaIndex.IsList(field.Type), field.Type is NonNullTypeNode, valueType, sensitivity);
    }

    /// <summary>The fields of a filter input (not its <c>and</c> and <c>or</c>), each with the operators of its operator input.</summary>
    private static List<BuilderFilterFieldDto>? FilterFields(SchemaIndex schema, string filterInput, Dictionary<string, string> labels)
    {
        if (!schema.Inputs.TryGetValue(filterInput, out var filter)) return null;
        var fields = new List<BuilderFilterFieldDto>();
        foreach (var field in filter.Fields.Where(f => f.Name.Value is not ("and" or "or")))
        {
            if (!schema.Inputs.TryGetValue(SchemaIndex.NamedType(field.Type), out var operators)) continue;
            var scalar = operators.Fields.Where(f => f.Name.Value == "eq").Select(f => SchemaIndex.NamedType(f.Type)).FirstOrDefault() ?? "String";
            fields.Add(new BuilderFilterFieldDto(
                field.Name.Value, Label(field.Name.Value, labels), FieldTypeRules.Scalar(scalar), scalar,
                [.. operators.Fields.Where(o => o.Name.Value is not ("and" or "or")).Select(o => new BuilderOperatorDto(o.Name.Value, SchemaIndex.IsList(o.Type)))]));
        }

        return fields;
    }

    private static List<BuilderSortFieldDto>? SortFields(SchemaIndex schema, string sortInput, Dictionary<string, string> labels) =>
        schema.Inputs.TryGetValue(sortInput, out var sort)
            ? [.. sort.Fields.Select(f => new BuilderSortFieldDto(f.Name.Value, Label(f.Name.Value, labels)))]
            : null;

    /// <summary>
    /// A column's label from the field catalog; a catalog field of several columns (a date has a display text and two
    /// years) names each column by its own words so they can be told apart.
    /// </summary>
    private Dictionary<string, string> ColumnLabels()
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in catalog.Get().Entities.SelectMany(e => e.Fields))
        {
            foreach (var column in field.Columns)
                labels[column] = field.Columns.Count == 1 ? field.Label : $"{field.Label} ({Distinguishing(field.Label, Humanise(Camel(column)))})";
        }

        return labels;
    }

    /// <summary>The words of a column's name that the catalog label does not already say: "Creation date" + "Creation date earliest year" → "earliest year".</summary>
    private static string Distinguishing(string label, string column) =>
        column.StartsWith(label, StringComparison.OrdinalIgnoreCase) && column.Length > label.Length ? column[label.Length..].Trim().ToLowerInvariant() : column;

    private static string Label(string name, Dictionary<string, string> labels) =>
        labels.TryGetValue(Snake(name), out var label) ? label : Humanise(name);

    /// <summary><c>creationDateEarliestYear</c> → <c>creation_date_earliest_year</c>.</summary>
    private static string Snake(string name) => CamelBoundary().Replace(name, "$1_$2").ToLowerInvariant();

    /// <summary><c>creation_date_earliest_year</c> → <c>creationDateEarliestYear</c>.</summary>
    private static string Camel(string column) =>
        string.Concat(column.Split('_').Select((part, i) => i == 0 || part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));

    /// <summary><c>creationDateEarliestYear</c> → <c>creation date earliest year</c>, first letter upper case.</summary>
    private static string Humanise(string name)
    {
        var words = CamelBoundary().Replace(name, "$1 $2").ToLowerInvariant();
        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
    }

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex CamelBoundary();
}
