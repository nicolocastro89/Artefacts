using System.IO.Packaging;
using System.Text.RegularExpressions;
using Artefacts.Application.Imports;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace Artefacts.Infrastructure.Imports;

/// <summary>
/// Reads a sheet with the OpenXml SDK: rows with the forward-only <see cref="OpenXmlReader"/>, shared strings once,
/// and the pictures of the sheet's drawing part with their cell anchors.
/// </summary>
public sealed partial class OpenXmlWorkbookReader : IWorkbookReader
{
    public async Task<WorkbookSheet> ReadAsync(Stream workbook, string? sheet, Func<WorkbookPicture, Stream, Task> onPicture, CancellationToken ct)
    {
        SpreadsheetDocument document;
        try
        {
            document = SpreadsheetDocument.Open(workbook, false);
        }
        catch (Exception e) when (e is OpenXmlPackageException or FileFormatException or InvalidDataException or IOException)
        {
            throw new WorkbookReadException("The file is not a readable .xlsx workbook.", e);
        }

        using (document)
        {
            try
            {
                var part = document.WorkbookPart ?? throw new WorkbookReadException("The workbook has no sheets.");
                var sheets = part.Workbook?.Sheets?.Elements<Sheet>().ToList() ?? [];
                if (sheets.Count == 0) throw new WorkbookReadException("The workbook has no sheets.");
                var names = sheets.Select(s => s.Name?.Value ?? "").ToList();
                var shared = SharedStrings(part);

                Sheet chosen;
                List<WorkbookRow> rows;
                if (sheet is not null)
                {
                    chosen = sheets.FirstOrDefault(s => s.Name?.Value == sheet)
                             ?? throw new WorkbookReadException($"The workbook has no sheet named \"{sheet}\".");
                    rows = ReadRows(Worksheet(part, chosen), shared, ct);
                }
                else
                {
                    (chosen, rows) = (sheets[0], ReadRows(Worksheet(part, sheets[0]), shared, ct));
                    foreach (var candidate in sheets)
                    {
                        var candidateRows = candidate == sheets[0] ? rows : ReadRows(Worksheet(part, candidate), shared, ct);
                        if (candidateRows.Count < 2) continue;
                        (chosen, rows) = (candidate, candidateRows);
                        break;
                    }
                }

                var pictures = await ReadPicturesAsync(Worksheet(part, chosen), onPicture, ct);
                return new WorkbookSheet(chosen.Name?.Value ?? "", names, rows, pictures);
            }
            catch (Exception e) when (e is OpenXmlPackageException or InvalidDataException or System.Xml.XmlException)
            {
                throw new WorkbookReadException("The workbook is damaged and could not be read.", e);
            }
        }
    }

    private static WorksheetPart Worksheet(WorkbookPart part, Sheet sheet) =>
        sheet.Id?.Value is { } id && part.GetPartById(id) is WorksheetPart worksheet
            ? worksheet
            : throw new WorkbookReadException($"The sheet \"{sheet.Name?.Value}\" is not a worksheet.");

    private static List<string> SharedStrings(WorkbookPart part)
    {
        var result = new List<string>();
        if (part.SharedStringTablePart is not { } table) return result;
        using var reader = OpenXmlReader.Create(table);
        while (reader.Read())
        {
            if (reader.ElementType != typeof(SharedStringItem) || !reader.IsStartElement) continue;
            result.Add(TextOf((SharedStringItem)reader.LoadCurrentElement()!));
        }

        return result;
    }

    private static List<WorkbookRow> ReadRows(WorksheetPart part, List<string> shared, CancellationToken ct)
    {
        var rows = new List<WorkbookRow>();
        using var reader = OpenXmlReader.Create(part);
        var next = 1;
        while (reader.Read())
        {
            if (reader.ElementType != typeof(Row) || !reader.IsStartElement) continue;
            ct.ThrowIfCancellationRequested();
            var row = (Row)reader.LoadCurrentElement()!;
            var number = row.RowIndex?.Value is { } index ? (int)index : next;
            next = number + 1;

            var cells = new Dictionary<int, string>();
            var column = 0;
            foreach (var cell in row.Elements<Cell>())
            {
                column = cell.CellReference?.Value is { } reference ? ColumnOf(reference) : column + 1;
                var text = ValueOf(cell, shared);
                if (!string.IsNullOrWhiteSpace(text)) cells[column] = text;
            }

            if (cells.Count > 0) rows.Add(new WorkbookRow(number, cells));
        }

        return rows;
    }

    private static async Task<List<WorkbookPicture>> ReadPicturesAsync(
        WorksheetPart part, Func<WorkbookPicture, Stream, Task> onPicture, CancellationToken ct)
    {
        var pictures = new List<WorkbookPicture>();
        if (part.DrawingsPart is not { WorksheetDrawing: { } drawing } drawings) return pictures;

        foreach (var anchor in drawing.ChildElements)
        {
            var from = anchor switch
            {
                Xdr.TwoCellAnchor two => two.FromMarker,
                Xdr.OneCellAnchor one => one.FromMarker,
                _ => null,
            };
            if (from is null) continue;

            foreach (var picture in anchor.Descendants<Xdr.Picture>())
            {
                var embed = picture.BlipFill?.Blip?.Embed?.Value;
                if (embed is null || drawings.GetPartById(embed) is not ImagePart image) continue;
                ct.ThrowIfCancellationRequested();

                var info = new WorkbookPicture(
                    pictures.Count + 1,
                    int.Parse(from.RowId?.Text ?? "0") + 1,
                    int.Parse(from.ColumnId?.Text ?? "0") + 1,
                    long.TryParse(from.ColumnOffset?.Text, out var colOffset) ? colOffset : 0,
                    long.TryParse(from.RowOffset?.Text, out var rowOffset) ? rowOffset : 0,
                    image.ContentType,
                    picture.NonVisualPictureProperties?.NonVisualDrawingProperties?.Name?.Value ?? $"Picture {pictures.Count + 1}");
                await using (var content = image.GetStream(FileMode.Open, FileAccess.Read))
                {
                    await onPicture(info, content);
                }

                pictures.Add(info);
            }
        }

        return pictures;
    }

    private static string ValueOf(Cell cell, List<string> shared)
    {
        var raw = cell.CellValue?.Text;
        var type = cell.DataType?.Value;
        if (type == CellValues.SharedString)
            return int.TryParse(raw, out var i) && i >= 0 && i < shared.Count ? shared[i] : "";
        if (type == CellValues.InlineString)
            return cell.InlineString is { } inline ? TextOf(inline) : "";
        if (type == CellValues.Boolean)
            return raw == "1" ? "TRUE" : raw == "0" ? "FALSE" : raw ?? "";
        return Unescape(raw ?? "");
    }

    /// <summary>The visible text of a rich or plain string item, without phonetic runs.</summary>
    private static string TextOf(OpenXmlElement item) =>
        Unescape(string.Concat(item.Descendants<Text>().Where(t => t.Parent is not PhoneticRun).Select(t => t.Text)));

    /// <summary>Excel escapes control characters as <c>_xHHHH_</c>; line breaks become "\n".</summary>
    private static string Unescape(string text) =>
        EscapeRegex().Replace(text, m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString())
            .Replace("\r\n", "\n").Replace('\r', '\n');

    private static int ColumnOf(string reference)
    {
        var column = 0;
        foreach (var ch in reference)
        {
            if (ch is < 'A' or > 'Z') break;
            column = column * 26 + (ch - 'A' + 1);
        }

        return column;
    }

    [GeneratedRegex("_x([0-9A-Fa-f]{4})_")]
    private static partial Regex EscapeRegex();
}
