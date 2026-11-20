using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Artefacts.Infrastructure.Imports;

/// <summary>
/// Writes the import report as a workbook: one line per problem or warning of a row (or one line for a row without
/// either), with the sheet row, status, inventory number, link to the artefact, field, original text and reason.
/// </summary>
public sealed class XlsxImportReportWriter : IImportReportWriter
{
    private static readonly string[] Headings = ["Sheet row", "Status", "Inventory number", "Artefact", "Field", "Original text", "Reason"];

    public void Write(ImportReportDto report, Stream output)
    {
        using var buffer = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(buffer, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            workbook.Workbook = new Workbook(new Sheets());
            var sheetPart = workbook.AddNewPart<WorksheetPart>();
            var data = new SheetData();
            uint rowIndex = 1;
            data.AppendChild(Row(rowIndex++, Headings));

            foreach (var row in report.Rows)
            {
                var status = row.Status.ToString();
                var artefact = row.ArtefactId is { } id ? $"/artefacts/{id}" : "";
                var lines = row.Problems.Select(p => (p.Field, p.Text ?? "", p.Reason))
                    .Concat(row.Warnings.Select(w => ("warning", "", w)))
                    .ToList();
                if (lines.Count == 0) lines.Add(("", "", ""));
                foreach (var (field, text, reason) in lines)
                    data.AppendChild(Row(rowIndex++, [row.SheetRow.ToString(), status, row.InventoryNumber ?? "", artefact, field, text, reason]));
            }

            sheetPart.Worksheet = new Worksheet(
                new Columns(
                    new Column { Min = 1, Max = 2, Width = 12, CustomWidth = true },
                    new Column { Min = 3, Max = 4, Width = 24, CustomWidth = true },
                    new Column { Min = 5, Max = 5, Width = 18, CustomWidth = true },
                    new Column { Min = 6, Max = 7, Width = 60, CustomWidth = true }),
                data);
            workbook.Workbook.Sheets!.AppendChild(new Sheet
            {
                Id = workbook.GetIdOfPart(sheetPart),
                SheetId = 1,
                Name = "Report",
            });
        }

        buffer.Position = 0;
        buffer.CopyTo(output);
    }

    private static Row Row(uint index, IReadOnlyList<string> values)
    {
        var row = new Row { RowIndex = index };
        for (var i = 0; i < values.Count; i++)
        {
            row.AppendChild(new Cell
            {
                CellReference = $"{(char)('A' + i)}{index}",
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(values[i]) { Space = SpaceProcessingModeValues.Preserve }),
            });
        }

        return row;
    }
}
