namespace Artefacts.Domain.Imports;

/// <summary>
/// The entries a user added to (or changed in) one of the built-in lookup tables (aggregate root): words and what they
/// mean, as a JSON object of word to value. The built-in entries are code and are not stored; there is one row per
/// table that has user entries, identified by the table's identifier. Whether the entries fit the table is checked by
/// whoever saves them.
/// </summary>
public sealed class ImportLookupTable
{
    public const int MaxTableIdLength = 64;

    /// <summary>The identifier of the built-in table these entries belong to (<c>months</c>, <c>measureLabels</c>, ...).</summary>
    public string TableId { get; private set; } = default!;

    /// <summary>The entries as JSON text: an object of word to value.</summary>
    public string Entries { get; private set; } = default!;

    public DateTimeOffset UpdatedAt { get; private set; }

    // For EF Core.
    private ImportLookupTable()
    {
    }

    public static ImportLookupTable Create(string tableId, string entries, DateTimeOffset now) =>
        new() { TableId = tableId, Entries = entries, UpdatedAt = now };

    public void Replace(string entries, DateTimeOffset now)
    {
        Entries = entries;
        UpdatedAt = now;
    }
}
