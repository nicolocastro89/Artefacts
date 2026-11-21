using System.Text.RegularExpressions;
using Artefacts.Api.IntegrationTests.Infrastructure;

namespace Artefacts.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public partial class GraphSchemaTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>The fields the schema tags <c>@sensitive</c>, as <c>Type.field = tag</c>.</summary>
    private static List<string> SensitiveFields(string sdl)
    {
        var fields = new List<string>();
        var type = "";
        foreach (var line in sdl.Split('\n'))
        {
            if (TypeLine().Match(line) is { Success: true } t) type = t.Groups[1].Value;
            if (SensitiveLine().Match(line) is { Success: true } f) fields.Add($"{type}.{f.Groups[1].Value} = {f.Groups[2].Value}");
        }

        return fields;
    }

    [GeneratedRegex(@"^(?:type|interface|input) (\w+)")]
    private static partial Regex TypeLine();

    [GeneratedRegex(@"^\s+(\w+)(?:\(.*\))?: .*@sensitive\(tag: ""(\w+)""\)")]
    private static partial Regex SensitiveLine();

    [Fact]
    public async Task Published_schema_tags_the_financial_fields()
    {
        var sdl = await _client.GetStringAsync("/graphql?sdl");

        Assert.Equal(["Acquisition.price = financial", "Artefact.valuation = financial"], SensitiveFields(sdl).Order());
    }

    [Fact]
    public async Task Schema_file_is_exported_at_build_and_is_the_published_schema()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "openapi", "schema.graphql"))) dir = dir.Parent;
        Assert.NotNull(dir);

        var exported = await File.ReadAllTextAsync(Path.Combine(dir.FullName, "openapi", "schema.graphql"));
        var published = await _client.GetStringAsync("/graphql?sdl");

        Assert.Equal(published.ReplaceLineEndings("\n").Trim(), exported.ReplaceLineEndings("\n").Trim());
        Assert.Equal(["Acquisition.price = financial", "Artefact.valuation = financial"], SensitiveFields(exported).Order());
    }
}
