using System.Text.Json.Nodes;

namespace Artefacts.Infrastructure.Pages;

/// <summary>A page shipped with the application: its slug (the file name), its name and its definition as JSON.</summary>
public sealed record SystemPage(string Slug, string Name, string Definition);

/// <summary>
/// The system pages embedded in the assembly (<c>Pages/Definitions/*.json</c>). Each file holds the page's
/// <c>name</c> next to its <c>root</c> node; the stored definition is the file without the name.
/// </summary>
public static class SystemPages
{
    private const string Prefix = "Pages.";

    public static IReadOnlyList<SystemPage> All { get; } = Load();

    private static List<SystemPage> Load()
    {
        var assembly = typeof(SystemPages).Assembly;
        var list = new List<SystemPage>();
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix) && n.EndsWith(".json")).Order())
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            var file = JsonNode.Parse(stream)!.AsObject();
            var slug = resource[Prefix.Length..^".json".Length];
            var name = file["name"]?.GetValue<string>() ?? slug;
            file.Remove("name");
            list.Add(new SystemPage(slug, name, file.ToJsonString()));
        }

        return list;
    }
}
