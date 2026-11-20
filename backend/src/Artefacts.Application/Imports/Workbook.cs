using System.Text.RegularExpressions;

namespace Artefacts.Application.Imports;

/// <summary>A non-empty sheet row: its 1-based row number and the text of each non-empty cell by 1-based column.</summary>
public sealed record WorkbookRow(int SheetRow, IReadOnlyDictionary<int, string> Cells);

/// <summary>A picture embedded in the sheet, anchored at a 1-based row and column (offsets in EMUs inside the cell).</summary>
public sealed record WorkbookPicture(int Index, int Row, int Column, long ColumnOffset, long RowOffset, string ContentType, string Name);

/// <summary>What was read from one sheet of a workbook.</summary>
public sealed record WorkbookSheet(string Name, IReadOnlyList<string> SheetNames, IReadOnlyList<WorkbookRow> Rows, IReadOnlyList<WorkbookPicture> Pictures);

/// <summary>The workbook could not be read (not an .xlsx package, no such sheet, damaged parts).</summary>
public sealed class WorkbookReadException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Reads one sheet of an <c>.xlsx</c> workbook in a single forward pass.</summary>
public interface IWorkbookReader
{
    /// <summary>
    /// Reads <paramref name="sheet"/>, or the first sheet with at least one row below its headings, from a seekable
    /// stream. Each picture's content is handed to <paramref name="onPicture"/> while the workbook is open.
    /// </summary>
    /// <exception cref="WorkbookReadException"/>
    Task<WorkbookSheet> ReadAsync(Stream workbook, string? sheet, Func<WorkbookPicture, Stream, Task> onPicture, CancellationToken ct);
}

/// <summary>A record row of a sheet, with the group heading above it and the pictures anchored on it.</summary>
public sealed record SheetRecord(
    int SheetRow,
    string? GroupNumber,
    string? GroupLabel,
    IReadOnlyDictionary<int, string> Cells,
    IReadOnlyList<int> Pictures,
    IReadOnlyList<string> Warnings);

public sealed record SheetLayout(
    int HeadingRow,
    IReadOnlyDictionary<int, string> Headings,
    IReadOnlyList<SheetRecord> Records,
    int GroupHeadings,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Classifies the rows of a sheet: the first non-empty row holds the headings; a row whose only value is in the first
/// column and reads "NN. Text" is a group heading; every other row is a record. Pictures belong to the record row
/// they are anchored on, in reading order, or else to the nearest record row above with a warning.
/// </summary>
public static partial class SheetLayoutReader
{
    public static SheetLayout Classify(IReadOnlyList<WorkbookRow> rows, IReadOnlyList<WorkbookPicture> pictures)
    {
        if (rows.Count == 0) return new SheetLayout(0, new Dictionary<int, string>(), [], 0, ["The sheet is empty."]);

        var headings = rows[0].Cells.ToDictionary(c => c.Key, c => Clean(c.Value));
        var records = new List<(WorkbookRow Row, string? Number, string? Label)>();
        string? number = null, label = null;
        var groupHeadings = 0;
        foreach (var row in rows.Skip(1))
        {
            if (IsGroupHeading(row, out var n, out var l))
            {
                (number, label) = (n, l);
                groupHeadings++;
                continue;
            }

            records.Add((row, number, label));
        }

        var warnings = new List<string>();
        var picturesByRecord = records.ToDictionary(r => r.Row.SheetRow, _ => new List<int>());
        var rowWarnings = records.ToDictionary(r => r.Row.SheetRow, _ => new List<string>());
        foreach (var picture in pictures.OrderBy(p => p.Row).ThenBy(p => p.Column).ThenBy(p => p.ColumnOffset).ThenBy(p => p.RowOffset))
        {
            if (picturesByRecord.TryGetValue(picture.Row, out var own))
            {
                own.Add(picture.Index);
                continue;
            }

            var above = records.LastOrDefault(r => r.Row.SheetRow < picture.Row);
            if (above.Row is null)
            {
                warnings.Add($"Picture \"{picture.Name}\" on row {picture.Row} is above every record row and was not attached.");
                continue;
            }

            picturesByRecord[above.Row.SheetRow].Add(picture.Index);
            rowWarnings[above.Row.SheetRow].Add(
                $"Picture \"{picture.Name}\" is anchored on row {picture.Row}, which is not a record row; it was attached to row {above.Row.SheetRow}.");
        }

        return new SheetLayout(
            rows[0].SheetRow,
            headings,
            records.Select(r => new SheetRecord(r.Row.SheetRow, r.Number, r.Label, r.Row.Cells,
                picturesByRecord[r.Row.SheetRow], rowWarnings[r.Row.SheetRow])).ToList(),
            groupHeadings,
            warnings);
    }

    private static bool IsGroupHeading(WorkbookRow row, out string number, out string label)
    {
        number = label = "";
        if (row.Cells.Count != 1 || !row.Cells.TryGetValue(1, out var text)) return false;
        var m = GroupHeadingRegex().Match(text);
        if (!m.Success) return false;
        number = m.Groups["n"].Value;
        label = Clean(m.Groups["label"].Value);
        return true;
    }

    /// <summary>Collapses whitespace (including line breaks) to single spaces.</summary>
    public static string Clean(string text) => WhitespaceRegex().Replace(text, " ").Trim();

    [GeneratedRegex(@"^\s*(?<n>\d+)\.\s*(?<label>\S.*)$", RegexOptions.Singleline)]
    private static partial Regex GroupHeadingRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
