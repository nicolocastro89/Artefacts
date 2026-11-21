namespace Artefacts.Infrastructure.Graph;

/// <summary>A query shipped with the application: its identifier (the file name), its name and its document.</summary>
public sealed record SystemQuery(string Id, string Name, string Document);

/// <summary>
/// The system queries embedded from <c>Graph/Queries/*.graphql</c>. Each file starts with a <c># name: …</c> line;
/// the file name without extension is the query's identifier.
/// </summary>
public static class SystemQueries
{
    private const string Prefix = "Queries.";

    public static IReadOnlyList<SystemQuery> All { get; } = Load();

    private static List<SystemQuery> Load()
    {
        var assembly = typeof(SystemQueries).Assembly;
        var list = new List<SystemQuery>();
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix) && n.EndsWith(".graphql")).Order())
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            var document = reader.ReadToEnd().Replace("\r\n", "\n");
            var id = resource[Prefix.Length..^".graphql".Length];
            var firstLine = document.Split('\n', 2)[0];
            var name = firstLine.StartsWith("# name:") ? firstLine["# name:".Length..].Trim() : id;
            list.Add(new SystemQuery(id, name, document));
        }

        return list;
    }
}
