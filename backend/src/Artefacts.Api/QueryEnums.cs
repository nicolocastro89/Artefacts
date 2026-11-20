using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Artefacts.Api;

/// <summary>
/// Marks a string query parameter that carries an enum value. Minimal APIs bind enums with the
/// case-sensitive <c>Enum.TryParse</c>, but the API contract uses camelCase names (as in JSON), so such
/// parameters are bound as strings and parsed with <see cref="QueryEnum.TryParse{T}"/>. The OpenAPI
/// document still describes them with the enum's schema (see <see cref="QueryEnumOperationTransformer"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
internal sealed class QueryEnumAttribute(Type enumType) : Attribute
{
    public Type EnumType { get; } = enumType;
}

internal static class QueryEnum
{
    /// <summary>Parses a defined enum member by name, ignoring case. Null or empty yields <paramref name="fallback"/>.</summary>
    public static bool TryParse<T>(string? value, T fallback, out T result) where T : struct, Enum
    {
        result = fallback;
        if (string.IsNullOrWhiteSpace(value)) return true;
        // Names only: reject numeric strings, which Enum.TryParse would otherwise accept.
        if (char.IsDigit(value[0]) || value[0] == '-') return false;
        return Enum.TryParse(value, ignoreCase: true, out result) && Enum.IsDefined(result);
    }

    public static string[] AllowedValues<T>() where T : struct, Enum =>
        Enum.GetNames<T>().Select(JsonNamingPolicy.CamelCase.ConvertName).ToArray();
}

/// <summary>Points the schema of each <see cref="QueryEnumAttribute"/> parameter at the enum's component schema.</summary>
internal sealed class QueryEnumOperationTransformer : IOpenApiOperationTransformer
{
    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        if (operation.Parameters is null || context.Document is null) return;

        foreach (var description in context.Description.ParameterDescriptions)
        {
            var enumType = EnumTypeOf(description.ParameterDescriptor);
            if (enumType is null) continue;
            if (operation.Parameters.FirstOrDefault(p => p.Name == description.Name) is not OpenApiParameter parameter) continue;

            var name = enumType.Name;
            if (context.Document.Components?.Schemas?.ContainsKey(name) != true)
                context.Document.AddComponent(name, await context.GetOrCreateSchemaAsync(enumType, cancellationToken: ct));
            parameter.Schema = new OpenApiSchemaReference(name, context.Document);
        }
    }

    private static Type? EnumTypeOf(Microsoft.AspNetCore.Mvc.Abstractions.ParameterDescriptor? descriptor) => descriptor switch
    {
        // [AsParameters] members surface as properties; plain handler parameters as ParameterInfo.
        IPropertyInfoParameterDescriptor p => p.PropertyInfo.GetCustomAttribute<QueryEnumAttribute>()?.EnumType,
        IParameterInfoParameterDescriptor p => p.ParameterInfo.GetCustomAttribute<QueryEnumAttribute>()?.EnumType,
        _ => null,
    };
}
