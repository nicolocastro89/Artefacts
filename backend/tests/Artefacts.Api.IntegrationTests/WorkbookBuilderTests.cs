using Artefacts.Api.IntegrationTests.Infrastructure;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace Artefacts.Api.IntegrationTests;

/// <summary>The synthetic workbooks other tests rely on contain what the builder was told to write.</summary>
public class WorkbookBuilderTests
{
    [Fact]
    public void Writes_rows_multi_line_cells_and_anchored_pictures()
    {
        var jpeg = TestData.Jpeg(1000);
        var bytes = new WorkbookBuilder("CERAMICHE")
            .Row("Gruppo e numero", "Nome File", "Immagine")
            .Row("01. CINA")
            .Row("01.", "01. Coppa Celadon\ndiam 17,6 x h 7")
            .Picture(3, 3, jpeg)
            .Picture(3, 3, WorkbookBuilder.JpegXr(), "image/vnd.ms-photo", columnOffset: 2_000_000, oneCellAnchor: true)
            .Sheet("Template")
            .Row("Colonna9")
            .Build();

        using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        var workbook = document.WorkbookPart!;
        var sheets = workbook.Workbook!.Sheets!.Elements<Sheet>().ToList();
        Assert.Equal(["CERAMICHE", "Template"], sheets.Select(s => s.Name!.Value));

        var part = (WorksheetPart)workbook.GetPartById(sheets[0].Id!);
        var shared = workbook.SharedStringTablePart!.SharedStringTable!.Elements<SharedStringItem>().Select(i => i.InnerText).ToList();
        var cells = part.Worksheet!.Descendants<Cell>().ToDictionary(c => c.CellReference!.Value!, c => shared[int.Parse(c.CellValue!.Text)]);
        Assert.Equal("01. CINA", cells["A2"]);
        Assert.Equal("01. Coppa Celadon\ndiam 17,6 x h 7", cells["B3"]);

        var drawingsPart = part.DrawingsPart!;
        var drawing = drawingsPart.WorksheetDrawing!;
        var anchors = drawing.Elements<Xdr.TwoCellAnchor>().Cast<DocumentFormat.OpenXml.OpenXmlCompositeElement>()
            .Concat(drawing.Elements<Xdr.OneCellAnchor>()).ToList();
        Assert.Equal(2, anchors.Count);
        var images = drawingsPart.ImageParts.ToList();
        Assert.Contains(images, i => i.ContentType == "image/vnd.ms-photo");
        using var first = drawingsPart.GetPartById("rIdImg1").GetStream();
        using var copy = new MemoryStream();
        first.CopyTo(copy);
        Assert.Equal(jpeg, copy.ToArray());
    }
}
