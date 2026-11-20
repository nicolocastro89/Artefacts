namespace Artefacts.Infrastructure.Read;

/// <summary>
/// SQL functions (Read/Views/localized_names.sql) that resolve a name in a preference order, usable in LINQ
/// projections, ordering and grouping so that lists sort and group by the reader's language across pages.
/// </summary>
public static class LocalizedSql
{
    public static string? RegionName(Guid? regionId, string[] languages) =>
        throw new NotSupportedException("Translated to SQL by EF Core.");

    public static string? ObjectTypeName(Guid? objectTypeId, string[] languages) =>
        throw new NotSupportedException("Translated to SQL by EF Core.");
}
