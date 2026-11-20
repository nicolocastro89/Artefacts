using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Imports;
using Artefacts.Infrastructure.Imports;

namespace Artefacts.Api.IntegrationTests;

public class WorkbookReaderTests
{
    private static async Task<(WorkbookSheet Sheet, SheetLayout Layout, Dictionary<int, byte[]> Pictures)> ReadAsync(byte[] workbook, string? sheet = null)
    {
        var pictures = new Dictionary<int, byte[]>();
        var read = await new OpenXmlWorkbookReader().ReadAsync(new MemoryStream(workbook), sheet, async (p, s) =>
        {
            using var copy = new MemoryStream();
            await s.CopyToAsync(copy);
            pictures[p.Index] = copy.ToArray();
        }, CancellationToken.None);
        return (read, SheetLayoutReader.Classify(read.Rows, read.Pictures), pictures);
    }

    [Fact]
    public async Task Group_heading_sets_the_group_of_the_records_below_it()
    {
        var workbook = new WorkbookBuilder()
            .Row("Gruppo e numero", "Nome File")
            .Row("01. CINA")
            .Row("01.", "01. Coppa Celadon\ndiam 17,6 x h 7")
            .Row("01.", "02. Piatto Celadon")
            .Row("02. TURCHIA")
            .Row("02.", "01. Piatto Iznik")
            .Build();

        var (_, layout, _) = await ReadAsync(workbook);

        Assert.Equal("Nome File", layout.Headings[2]);
        Assert.Equal(2, layout.GroupHeadings);
        Assert.Equal([("01", "CINA"), ("01", "CINA"), ("02", "TURCHIA")], layout.Records.Select(r => (r.GroupNumber, r.GroupLabel)));
        Assert.Equal("01. Coppa Celadon\ndiam 17,6 x h 7", layout.Records[0].Cells[2]);
        Assert.Equal([3, 4, 6], layout.Records.Select(r => r.SheetRow));
    }

    [Fact]
    public async Task Two_pictures_on_a_row_are_attached_left_first()
    {
        var left = TestData.Jpeg(500);
        var right = TestData.Tiff(600);
        var workbook = new WorkbookBuilder()
            .Row("Numero", "Nome File", "Immagine")
            .Row("01.", "01. Coppa")
            // Written right first: reading order comes from the anchors, not from the drawing order.
            .Picture(2, 3, right, "image/tiff", columnOffset: 3_000_000)
            .Picture(2, 3, left, columnOffset: 100)
            .Build();

        var (sheet, layout, pictures) = await ReadAsync(workbook);

        var record = Assert.Single(layout.Records);
        Assert.Equal(2, record.Pictures.Count);
        Assert.Equal(left, pictures[record.Pictures[0]]);
        Assert.Equal(right, pictures[record.Pictures[1]]);
        Assert.Equal("image/tiff", sheet.Pictures.Single(p => p.Index == record.Pictures[1]).ContentType);
    }

    [Fact]
    public async Task Picture_on_a_row_that_is_not_a_record_goes_to_the_record_above_with_a_warning()
    {
        var workbook = new WorkbookBuilder()
            .Row("Numero", "Nome File", "Immagine")
            .Row("01. CINA")
            .Row("01.", "01. Coppa")
            .Row("02. TURCHIA")
            .Row("02.", "01. Piatto")
            .Picture(4, 3, TestData.Jpeg(300), oneCellAnchor: true)
            .Build();

        var (_, layout, _) = await ReadAsync(workbook);

        Assert.Single(layout.Records[0].Pictures);
        Assert.Contains("row 4", Assert.Single(layout.Records[0].Warnings));
        Assert.Empty(layout.Records[1].Pictures);
    }

    [Fact]
    public async Task Skips_empty_sheets_and_reads_a_named_sheet()
    {
        var workbook = new WorkbookBuilder("Foglio1")
            .Sheet("TESSUTI").Row("Colonna", "Nome File").Row("01.", "01. Abito")
            .Sheet("Template").Row("Colonna9")
            .Build();

        var (sheet, _, _) = await ReadAsync(workbook);
        Assert.Equal("TESSUTI", sheet.Name);
        Assert.Equal(["Foglio1", "TESSUTI", "Template"], sheet.SheetNames);

        var (template, _, _) = await ReadAsync(workbook, "Template");
        Assert.Single(template.Rows);
    }

    [Fact]
    public async Task A_file_that_is_not_a_workbook_is_reported()
    {
        await Assert.ThrowsAsync<WorkbookReadException>(() => ReadAsync("a,b\n1,2"u8.ToArray()));
    }
}
