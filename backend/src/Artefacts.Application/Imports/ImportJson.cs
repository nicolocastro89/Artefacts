using System.Text.Json;
using System.Text.Json.Serialization;

namespace Artefacts.Application.Imports;

/// <summary>JSON settings for mappings, decisions and staged rows stored in the database (the API's conventions).</summary>
public static class ImportJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException($"Empty {typeof(T).Name} JSON.");
}
