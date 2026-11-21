namespace Artefacts.Infrastructure.Read;

/// <summary>
/// SQL functions (Read/Views/localized_names.sql) that resolve or match names of linked records, usable in LINQ
/// projections, ordering and grouping so that lists sort and group by the reader's language across pages.
/// </summary>
public static class LocalizedSql
{
    public static string? RegionName(Guid? regionId, string[] languages) =>
        throw new NotSupportedException("Translated to SQL by EF Core.");

    /// <summary>The name of a dynasty, collection group or storage location.</summary>
    public static string? ReferenceEntryName(Guid? entryId, string[] languages) =>
        throw new NotSupportedException("Translated to SQL by EF Core.");

    /// <summary>Whether a name of the list entry, in any language, matches an ILIKE pattern escaped with <c>\</c>.</summary>
    public static bool ReferenceEntryMatches(Guid? entryId, string pattern) =>
        throw new NotSupportedException("Translated to SQL by EF Core.");

    public static string? PartyName(Guid? partyId) =>
        throw new NotSupportedException("Translated to SQL by EF Core.");

    public static string? ObjectTypeName(Guid? objectTypeId, string[] languages) =>
        throw new NotSupportedException("Translated to SQL by EF Core.");
}
