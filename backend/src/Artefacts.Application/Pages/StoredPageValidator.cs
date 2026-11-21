using System.Text.Json;
using Artefacts.Application.Abstractions;

namespace Artefacts.Application.Pages;

/// <summary>
/// Checks that a page definition has the shape the frontend renders: a root node of containers, layouts, repeats and
/// widgets, with data sources of a known kind, and stored queries that exist. It does not check widget identifiers,
/// binding slots or binding types; the renderer and the page editor report those.
/// </summary>
public sealed class StoredPageValidator(IStoredQueryRepository queries)
{
    private const int MaxDepth = 40;

    private static readonly string[] NodeTypes = ["widget", "repeat", "container", "layout"];
    private static readonly string[] Layouts = ["stack", "row", "split", "grid", "section", "rack", "steps"];
    private static readonly string[] DataSourceKinds = ["endpoint", "query", "static"];

    /// <summary>The problems of a definition, each with its path (<c>root.children[0].dataSource.queryId</c>); empty when it is valid.</summary>
    public async Task<IReadOnlyList<string>> ValidateAsync(JsonElement? definition, CancellationToken ct)
    {
        var problems = new List<string>();
        var queryIds = new List<(string Path, string Id)>();

        if (definition is not { ValueKind: JsonValueKind.Object } page)
            return ["definition: A page definition is a JSON object with a root node."];
        if (!page.TryGetProperty("root", out var root)) return ["root: Is required."];
        Node(root, "root", 0, problems, queryIds);

        foreach (var (path, id) in queryIds)
            if (await queries.GetAsync(id, ct) is null) problems.Add($"{path}: The stored query '{id}' does not exist.");
        return problems;
    }

    private static void Node(JsonElement node, string path, int depth, List<string> problems, List<(string, string)> queryIds)
    {
        if (depth > MaxDepth) { problems.Add($"{path}: Is nested too deeply."); return; }
        if (node.ValueKind != JsonValueKind.Object) { problems.Add($"{path}: Must be an object."); return; }
        Text(node, "id", path, problems, required: true);

        if (Text(node, "type", path, problems, required: true) is not { } type) return;
        if (!NodeTypes.Contains(type)) { problems.Add($"{path}.type: Unknown node type '{type}'."); return; }

        switch (type)
        {
            case "widget":
                Text(node, "widgetId", path, problems, required: true);
                Object(node, "bindings", path, problems, required: true, valuesAreText: true);
                Object(node, "config", path, problems, required: false);
                break;
            case "repeat":
                Text(node, "itemsPath", path, problems, required: true);
                Text(node, "emptyText", path, problems, required: false);
                OneOf(node, "layout", Layouts, path, problems, required: false);
                Children(node, path, depth, problems, queryIds);
                break;
            case "container":
                DataSource(node, path, problems, queryIds);
                Children(node, path, depth, problems, queryIds);
                break;
            case "layout":
                OneOf(node, "layout", Layouts, path, problems, required: true);
                Text(node, "title", path, problems, required: false);
                Children(node, path, depth, problems, queryIds);
                break;
        }
    }

    private static void Children(JsonElement node, string path, int depth, List<string> problems, List<(string, string)> queryIds)
    {
        if (!node.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
        {
            problems.Add($"{path}.children: Must be a list of nodes.");
            return;
        }
        var index = 0;
        foreach (var child in children.EnumerateArray()) Node(child, $"{path}.children[{index++}]", depth + 1, problems, queryIds);
    }

    private static void DataSource(JsonElement node, string path, List<string> problems, List<(string, string)> queryIds)
    {
        var at = $"{path}.dataSource";
        if (!node.TryGetProperty("dataSource", out var source) || source.ValueKind != JsonValueKind.Object)
        {
            problems.Add($"{at}: A container needs a data source.");
            return;
        }
        if (OneOf(source, "kind", DataSourceKinds, at, problems, required: true) is not { } kind) return;

        switch (kind)
        {
            case "endpoint":
                Text(source, "operationId", at, problems, required: true);
                Object(source, "params", at, problems, required: false);
                break;
            case "query":
                if (Text(source, "queryId", at, problems, required: true) is { } queryId) queryIds.Add(($"{at}.queryId", queryId));
                Object(source, "variables", at, problems, required: false);
                Text(source, "require", at, problems, required: false);
                break;
            case "static":
                if (!source.TryGetProperty("data", out _)) problems.Add($"{at}.data: Is required.");
                break;
        }
    }

    private static string? Text(JsonElement node, string name, string path, List<string> problems, bool required)
    {
        if (!node.TryGetProperty(name, out var value))
        {
            if (required) problems.Add($"{path}.{name}: Is required.");
            return null;
        }
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            if (required || value.ValueKind != JsonValueKind.Null) problems.Add($"{path}.{name}: Must be text.");
            return null;
        }
        return value.GetString();
    }

    private static string? OneOf(JsonElement node, string name, string[] allowed, string path, List<string> problems, bool required)
    {
        var value = Text(node, name, path, problems, required);
        if (value is null || allowed.Contains(value)) return value;
        problems.Add($"{path}.{name}: '{value}' is not one of {string.Join(", ", allowed)}.");
        return null;
    }

    private static void Object(JsonElement node, string name, string path, List<string> problems, bool required, bool valuesAreText = false)
    {
        if (!node.TryGetProperty(name, out var value))
        {
            if (required) problems.Add($"{path}.{name}: Is required.");
            return;
        }
        if (value.ValueKind != JsonValueKind.Object) { problems.Add($"{path}.{name}: Must be an object."); return; }
        if (!valuesAreText) return;
        foreach (var property in value.EnumerateObject())
            if (property.Value.ValueKind != JsonValueKind.String) problems.Add($"{path}.{name}.{property.Name}: Must be text.");
    }
}
