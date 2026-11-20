using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Artefacts.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Builds small synthetic <c>.xlsx</c> workbooks in code, shaped like the collection's real workbooks:
/// a headings row, group heading rows, multi-line cells (shared strings, as Excel writes them) and pictures
/// anchored to cells. Real workbooks are never used by tests.
/// </summary>
public sealed class WorkbookBuilder
{
    private readonly List<SheetSpec> _sheets = [];
    private SheetSpec Current => _sheets[^1];

    public WorkbookBuilder(string sheetName = "CERAMICHE") => Sheet(sheetName);

    /// <summary>Starts another sheet; following rows and pictures go to it.</summary>
    public WorkbookBuilder Sheet(string name)
    {
        _sheets.Add(new SheetSpec(name));
        return this;
    }

    /// <summary>Appends a row; cells are column A, B, … and null leaves a cell empty.</summary>
    public WorkbookBuilder Row(params string?[] cells)
    {
        Current.Rows.Add(cells);
        return this;
    }

    public WorkbookBuilder EmptyRow() => Row();

    /// <summary>The 1-based sheet row number the next <see cref="Row"/> call will write.</summary>
    public int NextRow => Current.Rows.Count + 1;

    /// <summary>
    /// Anchors a picture at a 1-based <paramref name="row"/> and <paramref name="column"/>, with an offset in EMUs
    /// inside the cell. <paramref name="contentType"/> is the image part's type (e.g. image/vnd.ms-photo for JPEG XR).
    /// </summary>
    public WorkbookBuilder Picture(int row, int column, byte[] content, string contentType = "image/jpeg",
        long columnOffset = 0, bool oneCellAnchor = false)
    {
        Current.Pictures.Add(new PictureSpec(row, column, content, contentType, columnOffset, oneCellAnchor));
        return this;
    }

    public byte[] Build()
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook(new Sheets());
            var shared = workbookPart.AddNewPart<SharedStringTablePart>();
            shared.SharedStringTable = new SharedStringTable();
            var strings = new Dictionary<string, int>();

            uint sheetId = 1;
            foreach (var spec in _sheets)
            {
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var data = new SheetData();
                for (var r = 0; r < spec.Rows.Count; r++)
                {
                    var row = new Row { RowIndex = (uint)(r + 1) };
                    for (var c = 0; c < spec.Rows[r].Length; c++)
                    {
                        var text = spec.Rows[r][c];
                        if (text is null) continue;
                        if (!strings.TryGetValue(text, out var index))
                        {
                            index = strings.Count;
                            strings[text] = index;
                            shared.SharedStringTable.AppendChild(new SharedStringItem(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
                        }

                        row.AppendChild(new Cell
                        {
                            CellReference = ColumnName(c + 1) + (r + 1),
                            DataType = CellValues.SharedString,
                            CellValue = new CellValue(index.ToString()),
                        });
                    }

                    data.AppendChild(row);
                }

                worksheetPart.Worksheet = new Worksheet(data);
                if (spec.Pictures.Count > 0)
                {
                    var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
                    WriteDrawing(drawingsPart, spec.Pictures);
                    worksheetPart.Worksheet.AppendChild(new Drawing { Id = worksheetPart.GetIdOfPart(drawingsPart) });
                }

                workbookPart.Workbook.Sheets!.AppendChild(new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = sheetId++,
                    Name = spec.Name,
                });
            }
        }

        return stream.ToArray();
    }

    private static void WriteDrawing(DrawingsPart part, List<PictureSpec> pictures)
    {
        var xml = new StringBuilder();
        xml.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        xml.Append("""<xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""");
        for (var i = 0; i < pictures.Count; i++)
        {
            var p = pictures[i];
            var image = part.AddNewPart<ImagePart>(p.ContentType, $"rIdImg{i + 1}");
            using (var s = new MemoryStream(p.Content)) image.FeedData(s);

            var from = $"<xdr:from><xdr:col>{p.Column - 1}</xdr:col><xdr:colOff>{p.ColumnOffset}</xdr:colOff><xdr:row>{p.Row - 1}</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from>";
            var pic = $"""<xdr:pic><xdr:nvPicPr><xdr:cNvPr id="{i + 2}" name="Immagine {i + 1}"/><xdr:cNvPicPr/></xdr:nvPicPr><xdr:blipFill><a:blip r:embed="rIdImg{i + 1}"/><a:stretch/></xdr:blipFill><xdr:spPr><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></xdr:spPr></xdr:pic><xdr:clientData/>""";
            if (p.OneCellAnchor)
                xml.Append($"""<xdr:oneCellAnchor>{from}<xdr:ext cx="914400" cy="914400"/>{pic}</xdr:oneCellAnchor>""");
            else
                xml.Append($"""<xdr:twoCellAnchor editAs="oneCell">{from}<xdr:to><xdr:col>{p.Column - 1}</xdr:col><xdr:colOff>{p.ColumnOffset + 914400}</xdr:colOff><xdr:row>{p.Row - 1}</xdr:row><xdr:rowOff>914400</xdr:rowOff></xdr:to>{pic}</xdr:twoCellAnchor>""");
        }

        xml.Append("</xdr:wsDr>");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml.ToString()));
        part.FeedData(stream);
    }

    public static string ColumnName(int column)
    {
        var name = "";
        for (var n = column; n > 0; n = (n - 1) / 26) name = (char)('A' + (n - 1) % 26) + name;
        return name;
    }

    /// <summary>A tiny JPEG XR (HD Photo) header: enough for format sniffing, which is all the catalogue does.</summary>
    public static byte[] JpegXr() => [0x49, 0x49, 0xBC, 0x01, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    private sealed record SheetSpec(string Name)
    {
        public List<string?[]> Rows { get; } = [];
        public List<PictureSpec> Pictures { get; } = [];
    }

    private sealed record PictureSpec(int Row, int Column, byte[] Content, string ContentType, long ColumnOffset, bool OneCellAnchor);
}
