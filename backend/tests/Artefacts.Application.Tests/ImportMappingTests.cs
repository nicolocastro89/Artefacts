using System.Text.RegularExpressions;
using Artefacts.Application.Imports;
using Artefacts.Domain.Catalogue;

namespace Artefacts.Application.Tests;

public class ImportMappingTests
{
    private static readonly Dictionary<int, string> Headings = new()
    {
        [1] = "Gruppo e numero", [2] = "Nome File", [3] = "Immagine", [4] = "Zona di Produzione\nData\nDinastia",
        [5] = "Provenienza", [6] = "Formato Immagine", [7] = "Mostre", [8] = "William",
    };

    private static ImportMapping Suggested(params Dictionary<int, string>[] rows) =>
        MappingSuggestion.Suggest(Headings,
            rows.Select((cells, i) => new SheetRecord(i + 2, "01", "CINA", cells, [], [])).ToList(), "CERAMICHE");

    private static MappedRecord Map(Dictionary<int, string> cells) => MappingEngine.Map(cells, Suggested(cells), Headings);

    [Fact]
    public void Suggests_line_rules_from_the_headings()
    {
        var mapping = Suggested(new Dictionary<int, string> { [1] = "01.", [2] = "01. Coppa" });

        Assert.Equal(ImportField.Holder, mapping.Columns.Single(c => c.Column == 1).Rest);
        Assert.Equal(ImportField.Description, mapping.Columns.Single(c => c.Column == 2).Rest);
        Assert.Equal(ImportField.Dynasty, mapping.Columns.Single(c => c.Column == 4).Rest);
        Assert.Equal(ImportField.AcquisitionNote, mapping.Columns.Single(c => c.Column == 5).Rest);
        Assert.Equal(ImportField.Exhibitions, mapping.Columns.Single(c => c.Column == 7).Target);
        Assert.Equal(ImportField.CatalogueEntry, mapping.Columns.Single(c => c.Column == 8).Target);
        Assert.Equal("en", mapping.Columns.Single(c => c.Column == 8).Language);
        Assert.True(mapping.Columns.Single(c => c.Column == 6).Ignored);
        Assert.Equal("{sheet}-{group:2}-{item:2}", mapping.InventoryNumber.Pattern);
        Assert.Equal(ImportField.CatalogueNumber, mapping.InventoryNumber.Prefer);
    }

    [Fact]
    public void Multi_line_title_cell()
    {
        var mapped = Map(new() { [2] = "01. Coppa Celadon\ndiam 17,6 x h 7" });

        Assert.Equal("Coppa Celadon", mapped.First(ImportField.Title)!.Text);
        Assert.Equal("01", mapped.First(ImportField.ItemNumber)!.Text);
        Assert.Equal("diam 17,6 x h 7", mapped.First(ImportField.Dimensions)!.Text);
    }

    [Fact]
    public void Title_continues_over_several_lines_and_remarks_go_to_the_rest()
    {
        var mapped = Map(new() { [2] = "03. Piatto bianco e blu\ncon circoli \ndiam 34,6\nDA MONTARE" });

        Assert.Equal(["Piatto bianco e blu", "con circoli"], mapped.Of(ImportField.Title).Select(v => v.Text));
        Assert.Equal("DA MONTARE", mapped.First(ImportField.Description)!.Text);
    }

    [Fact]
    public void Acquisition_cell_split_by_content()
    {
        var mapped = Map(new() { [5] = "Sotheby's London\n12 06 1990\nlot 170 £ 7.865" });

        Assert.Equal("Sotheby's London", mapped.First(ImportField.AcquisitionSource)!.Text);
        Assert.Equal(new DateOnly(1990, 6, 12), mapped.First(ImportField.AcquisitionDate)!.Parsed);
        Assert.Equal("170", mapped.First(ImportField.AcquisitionLot)!.Text);
        Assert.Equal(new ParsedMoney(7865, "GBP"), mapped.First(ImportField.AcquisitionPrice)!.Parsed);
    }

    [Fact]
    public void Unmatched_acquisition_lines_go_to_the_note()
    {
        var mapped = Map(new() { [5] = "Christie's 2 Maggio 2019\nlot 285\nacquistato fuori asta" });

        Assert.Equal("Christie's", mapped.First(ImportField.AcquisitionSource)!.Text);
        Assert.Equal("acquistato fuori asta", mapped.First(ImportField.AcquisitionNote)!.Text);
    }

    [Fact]
    public void Origin_cell_gives_place_date_and_dynasty()
    {
        var mapped = Map(new() { [4] = "Medio oriente o \nnord Africa\nIX secolo \nAbbaside" });

        Assert.Equal(["Medio oriente o", "nord Africa"], mapped.Of(ImportField.PlaceOfProduction).Select(v => v.Text));
        Assert.Equal("IX secolo", mapped.First(ImportField.CreationDate)!.Text);
        Assert.Equal("Abbaside", mapped.First(ImportField.Dynasty)!.Text);
    }

    [Fact]
    public void A_date_over_two_lines_is_joined()
    {
        var split = Map(new() { [4] = "Egitto\nseconda metà\nXVI\nottomana" });
        Assert.Equal("seconda metà XVI", split.First(ImportField.CreationDate)!.Text);
        Assert.Equal("ottomana", split.First(ImportField.Dynasty)!.Text);

        var bracketed = Map(new() { [4] = "Egitto\n19 Dhu'l hijja 797\n(4 Ottobre 1395)\nMamelucco" });
        Assert.Equal("19 Dhu'l hijja 797 (4 Ottobre 1395)", bracketed.First(ImportField.CreationDate)!.Text);
        Assert.Equal("Mamelucco", bracketed.First(ImportField.Dynasty)!.Text);
    }

    [Fact]
    public void A_line_that_only_looks_like_a_date_gives_way_to_a_real_date()
    {
        var mapped = Map(new() { [4] = "Qinghai Dulan VIII\nVII - VIII\nTang" });

        Assert.Equal("Qinghai Dulan VIII", mapped.First(ImportField.PlaceOfProduction)!.Text);
        Assert.Equal("VII - VIII", mapped.First(ImportField.CreationDate)!.Text);
    }

    [Fact]
    public void Holding_column_gives_group_location_and_holder()
    {
        var mapped = Map(new() { [1] = "01.\nBeta2\nRossetti\nFine Arts srl \ncella 16" });

        Assert.Equal("01", mapped.First(ImportField.GroupNumber)!.Text);
        Assert.Equal(["Beta2"], mapped.Of(ImportField.StorageLocation).Select(v => v.Text).Take(1));
        Assert.Equal(["Rossetti", "Fine Arts srl"], mapped.Of(ImportField.Holder).Select(v => v.Text));
    }

    [Fact]
    public void Exhibitions_are_one_entry_per_paragraph()
    {
        var mapped = Map(new() { [7] = "IL MONTEFELTRO E L'ORIENTE, \nPalazzo Ducale, Urbino\nGiugno - Ottobre 2018\n\nARTE OTTOMANA\nGenova" });

        Assert.Equal(["IL MONTEFELTRO E L'ORIENTE, Palazzo Ducale, Urbino, Giugno - Ottobre 2018", "ARTE OTTOMANA, Genova"],
            mapped.Of(ImportField.Exhibitions).Select(v => v.Text));
    }

    [Fact]
    public void A_column_without_a_rest_field_reports_its_unmatched_lines()
    {
        var mapping = new ImportMapping(
            [new ColumnRule(5, "Provenienza", Lines: [new LineRule(ImportField.AcquisitionLot, DetectorIds.Lot)])],
            new InventoryNumberPattern("X-{row}"));

        var mapped = MappingEngine.Map(new Dictionary<int, string> { [5] = "lot 3\nacquistato fuori asta" }, mapping, Headings);

        Assert.Contains("acquistato fuori asta", Assert.Single(mapped.Dropped));
    }

    public static TheoryData<string, string> Cells => new()
    {
        { "1", "01.\nAB\nMI F3" }, { "1", "01.\nW. 391\n" }, { "1", "01.\nRossetti\nFine Arts srl \ncella 16" },
        { "2", "01. abito sciamanico collo \na tre lobi\n126 x 210\n" }, { "2", "02a Frammento " },
        { "2", "01. Doppia pagina da corano\n27,9 x 36 cm\nDA MONTARE\n" }, { "2", "nessun numero\nsolo testo" },
        { "4", "Cina\nX-XII (907-1125) \nLiao" }, { "4", "metà XV\nOttomana?\n\nA TIMURID QUR'AN JUZ'\nAH 871/FEBRUARY 1467" },
        { "4", "XIII" }, { "4", "Cina nord-orientale\n(Qinghai, Xinjiang)\nVI secolo ?" },
        { "5", "Mario Bianchi\n18 09 2007 fatt 02 e 03 2007\n£ 99.000\nCarbon dating AD 770-1000" },
        { "5", "Paris, Drouot-Richelieu\nJean Dupont, Paris\n21 02 1992\nlot 112  Fr. 62.000" },
        { "5", "Christie's London\n4 aprile 2006, lot 3\n£ 8,645.00" }, { "5", "ADER, Paris\n27-11-2018\nlot.147" },
        { "7", "ARTE OTTOMANA 1450-1600\nPalazzo Lomellino, Genova\n\nSecond show" }, { "8", "A PARCEL GILT SILVER BOWL \nOTTOMAN BALKANS, 16TH CENTURY" },
    };

    /// <summary>
    /// Nothing is dropped: with the suggested mapping, the letters and digits of every line end up in the mapped
    /// values exactly once (lines split by a detector are counted by their parts).
    /// </summary>
    [Theory]
    [MemberData(nameof(Cells))]
    public void Every_line_ends_up_in_exactly_one_field(string column, string cell)
    {
        var col = int.Parse(column);
        var mapped = Map(new() { [col] = cell });

        Assert.Empty(mapped.Dropped);
        // A lot keeps its number only: the word "lot" marks it and is not text of its own.
        var expected = Regex.Replace(cell, @"\blot(?:to)?\b", "", RegexOptions.IgnoreCase);
        Assert.Equal(Sorted(expected), Sorted(string.Concat(mapped.Values.Select(v => v.Text))));
    }

    /// <summary>The letters and digits of a text in sorted order: a remainder split off a line changes the order only.</summary>
    private static string Sorted(string text) => string.Concat(Regex.Replace(text, @"[^\p{L}\p{N}]", "").Order());

    [Fact]
    public void A_rule_whose_heading_is_missing_does_not_take_a_column_another_rule_claims()
    {
        // Saved from a workbook with "Valore" in column 8; in this one column 8 is "William".
        var mapping = new ImportMapping(
            [new ColumnRule(9, "William", ImportField.CatalogueEntry), new ColumnRule(8, "Valore", ImportField.Valuation),
                new ColumnRule(2, "Nome File", ImportField.Title)],
            new InventoryNumberPattern("X-{row}"));
        var headings = new Dictionary<int, string> { [2] = "Nome", [8] = "William" };

        var resolved = MappingEngine.Resolve(mapping, headings);

        Assert.Equal([(8, ImportField.CatalogueEntry), (2, ImportField.Title)], resolved.Select(r => (r.Column, r.Rule.Target!.Value)));
    }

    [Fact]
    public void Template_mapping_round_trips_as_json()
    {
        var mapping = Suggested(new Dictionary<int, string> { [1] = "01." }) with { AcquisitionMethod = AcquisitionMethod.Gift };
        var json = System.Text.Json.JsonSerializer.Serialize(mapping, ImportJson.Options);
        var back = System.Text.Json.JsonSerializer.Deserialize<ImportMapping>(json, ImportJson.Options)!;

        Assert.Equal(mapping.InventoryNumber, back.InventoryNumber);
        Assert.Equal(mapping.Columns.Count, back.Columns.Count);
        Assert.Equal(mapping.Columns[3].Lines, back.Columns[3].Lines);
        Assert.Equal(AcquisitionMethod.Gift, back.AcquisitionMethod);
    }
}
