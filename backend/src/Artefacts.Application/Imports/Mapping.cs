using System.Text.Json.Serialization;
using Artefacts.Domain.Catalogue;

namespace Artefacts.Application.Imports;

/// <summary>A catalogue field that workbook text can be mapped to.</summary>
public enum ImportField
{
    Title = 0,
    ItemNumber = 1,
    GroupNumber = 2,
    CatalogueNumber = 3,
    InventoryNumber = 4,
    Dimensions = 5,
    Medium = 6,
    Maker = 7,
    Description = 8,
    PlaceOfProduction = 9,
    CreationDate = 10,
    Dynasty = 11,
    AcquisitionSource = 12,
    AcquisitionDate = 13,
    AcquisitionLot = 14,
    AcquisitionPrice = 15,
    AcquisitionNote = 16,
    Exhibitions = 17,
    CatalogueEntryEn = 18,
    Holder = 19,
    StorageLocation = 20,
    Valuation = 21,
}

/// <summary>
/// One line rule of a column: lines recognised by <see cref="Detector"/>, or the line at <see cref="Position"/>
/// (0-based among the cell's non-empty lines), go to <see cref="Target"/>. With <see cref="Continues"/>, unmatched
/// lines that directly follow go to the same field (a title or a place written over several lines).
/// </summary>
public sealed record LineRule(ImportField Target, LineDetector? Detector = null, int? Position = null, bool Continues = false);

/// <summary>
/// How one column is mapped, identified by its heading text with its 1-based index as a fallback. Either the whole
/// cell goes to <see cref="Target"/>, or its lines are assigned by <see cref="Lines"/> with every other line going to
/// <see cref="Rest"/>. A column with neither is ignored.
/// </summary>
public sealed record ColumnRule(int Column, string? Heading, ImportField? Target = null, IReadOnlyList<LineRule>? Lines = null, ImportField? Rest = null)
{
    [JsonIgnore] public bool UsesLines => Lines is { Count: > 0 };
    [JsonIgnore] public bool Ignored => Target is null && !UsesLines;
}

/// <summary>
/// The inventory number pattern: literal text with <c>{group}</c>, <c>{item}</c>, <c>{row}</c> and
/// <c>{field:name}</c> tokens, each with optional zero padding (<c>{group:2}</c>), and a field to use instead when the
/// row has a value for it (such as a catalogue number).
/// </summary>
public sealed record InventoryNumberPattern(string Pattern, ImportField? Prefer = null);

/// <summary>How the rows of a workbook become artefacts. Stored on the job and in templates.</summary>
public sealed record ImportMapping(
    IReadOnlyList<ColumnRule> Columns,
    InventoryNumberPattern InventoryNumber,
    string Language = "it",
    AcquisitionMethod AcquisitionMethod = AcquisitionMethod.Purchase,
    IReadOnlyList<string>? LocationPatterns = null);

public enum PlaceAction
{
    /// <summary>Link the place text to an existing region.</summary>
    Link = 0,

    /// <summary>Create a region (when the import is committed) and link it.</summary>
    Create = 1,

    /// <summary>Import the place text without a region.</summary>
    Keep = 2,
}

/// <summary>
/// What to do with one distinct place text. <see cref="Text"/> is the text as first written; decisions match on its
/// normalised form. For <see cref="PlaceAction.Create"/>, <see cref="RegionId"/> is filled once the region exists.
/// </summary>
public sealed record PlaceDecision(
    string Text,
    PlaceAction Action,
    Guid? RegionId = null,
    IReadOnlyDictionary<string, string?>? Names = null,
    Guid? ParentId = null);

/// <summary>A year range given for a date text the rules do not understand.</summary>
public sealed record DateResolution(string Text, int EarliestYear, int LatestYear);

/// <summary>Place and date decisions, stored on the job and copied into templates.</summary>
public sealed record ImportDecisions(IReadOnlyList<PlaceDecision> Places, IReadOnlyList<DateResolution> Dates)
{
    public static readonly ImportDecisions Empty = new([], []);
}

/// <summary>A value assigned to a field from one line (or part of a line) of a cell.</summary>
public sealed record MappedValue(ImportField Field, string Text, int Column, int Line, object? Parsed = null, bool Guessed = false);

/// <summary>Everything a record row's cells were mapped to, plus lines that went nowhere.</summary>
public sealed record MappedRecord(IReadOnlyList<MappedValue> Values, IReadOnlyList<string> Dropped)
{
    public IEnumerable<MappedValue> Of(ImportField field) => Values.Where(v => v.Field == field);
    public MappedValue? First(ImportField field) => Values.FirstOrDefault(v => v.Field == field);
}
