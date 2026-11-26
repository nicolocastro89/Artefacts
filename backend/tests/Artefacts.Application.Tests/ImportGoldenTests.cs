using System.Runtime.CompilerServices;
using System.Text.Json;
using Artefacts.Application.Imports;

namespace Artefacts.Application.Tests;

/// <summary>
/// Characterisation of the whole row pipeline (map the cells, evaluate the row) over a corpus of workbook rows written
/// the way curators write them. The output of every row is compared with a checked-in snapshot, so a refactor of the
/// parsing (definitions, registries) proves it changes nothing, and an intended change shows up as a readable diff.
/// To accept intended changes, run with the environment variable <c>UPDATE_GOLDEN=1</c> and review the diff of
/// <c>ImportGolden/rows.golden.json</c>.
/// </summary>
public class ImportGoldenTests
{
    private static readonly Dictionary<int, string> Headings = new()
    {
        [1] = "Gruppo e numero", [2] = "Nome File", [3] = "Immagine", [4] = "Zona di Produzione\nData\nDinastia",
        [5] = "Provenienza", [6] = "Formato Immagine", [7] = "Mostre", [8] = "William",
    };

    private static readonly JsonSerializerOptions Pretty = new(ImportJson.Options) { WriteIndented = true };

    /// <summary>Named rows: the cells by column, and the group heading above the row.</summary>
    private static readonly (string Name, string? Group, Dictionary<int, string> Cells)[] Corpus =
    [
        ("plain-title", "01", new() { [1] = "01.", [2] = "01. Coppa Celadon\ndiam 17,6 x h 7" }),
        ("multi-line-title", "01", new() { [1] = "01.", [2] = "03. Piatto bianco e blu\ncon circoli \ndiam 34,6\nDA MONTARE" }),
        ("letter-suffix", "02", new() { [1] = "02.", [2] = "02a Frammento " }),
        ("no-number", "01", new() { [1] = "01.", [2] = "nessun numero\nsolo testo" }),
        ("times-dimensions", "01", new() { [1] = "01.", [2] = "01. abito sciamanico collo \na tre lobi\n126 x 210\n" }),
        ("unlabelled-cm", "01", new() { [1] = "01.", [2] = "01. Doppia pagina da corano\n27,9 x 36 cm\nDA MONTARE\n" }),
        ("holding-column", "01", new() { [1] = "01.\nBeta2\nRossetti\nFine Arts srl \ncella 16", [2] = "04. Ciotola" }),
        ("catalogue-number", "01", new() { [1] = "01.\nW. 391\n", [2] = "05. Brocca" }),
        ("location-code", "01", new() { [1] = "01.\nAB\nMI F3", [2] = "06. Vaso" }),
        ("origin-date-dynasty", "01", new() { [1] = "01.", [2] = "07. Tazza", [4] = "Cina\nX-XII (907-1125) \nLiao" }),
        ("origin-century", "01", new() { [1] = "01.", [2] = "08. Tazza", [4] = "Medio oriente o \nnord Africa\nIX secolo \nAbbaside" }),
        ("date-over-two-lines", "01", new() { [1] = "01.", [2] = "09. Piatto", [4] = "Egitto\nseconda metà\nXVI\nottomana" }),
        ("date-with-bracket", "01", new() { [1] = "01.", [2] = "10. Piatto", [4] = "Egitto\n19 Dhu'l hijja 797\n(4 Ottobre 1395)\nMamelucco" }),
        ("date-lookalike", "01", new() { [1] = "01.", [2] = "11. Piatto", [4] = "Qinghai Dulan VIII\nVII - VIII\nTang" }),
        ("unknown-date", "01", new() { [1] = "01.", [2] = "12. Corano", [4] = "metà XV\nOttomana?\n\nA TIMURID QUR'AN JUZ'\nAH 871/FEBRUARY 1467" }),
        ("century-only", "01", new() { [1] = "01.", [2] = "13. Corano", [4] = "XIII" }),
        ("place-in-brackets", "01", new() { [1] = "01.", [2] = "14. Corano", [4] = "Cina nord-orientale\n(Qinghai, Xinjiang)\nVI secolo ?" }),
        ("acquisition-auction", "01", new() { [1] = "01.", [2] = "15. Coppa", [5] = "Sotheby's London\n12 06 1990\nlot 170 £ 7.865" }),
        ("acquisition-note", "01", new() { [1] = "01.", [2] = "16. Coppa", [5] = "Christie's 2 Maggio 2019\nlot 285\nacquistato fuori asta" }),
        ("acquisition-private", "01", new() { [1] = "01.", [2] = "17. Coppa", [5] = "Mario Bianchi\n18 09 2007 fatt 02 e 03 2007\n£ 99.000\nCarbon dating AD 770-1000" }),
        ("acquisition-francs", "01", new() { [1] = "01.", [2] = "18. Coppa", [5] = "Paris, Drouot-Richelieu\nJean Dupont, Paris\n21 02 1992\nlot 112  Fr. 62.000" }),
        ("acquisition-decimals", "01", new() { [1] = "01.", [2] = "19. Coppa", [5] = "Christie's London\n4 aprile 2006, lot 3\n£ 8,645.00" }),
        ("acquisition-dashes", "01", new() { [1] = "01.", [2] = "20. Coppa", [5] = "ADER, Paris\n27-11-2018\nlot.147" }),
        ("exhibitions", "01", new() { [1] = "01.", [2] = "21. Coppa", [7] = "IL MONTEFELTRO E L'ORIENTE, \nPalazzo Ducale, Urbino\nGiugno - Ottobre 2018\n\nARTE OTTOMANA\nGenova" }),
        ("catalogue-entry", "01", new() { [1] = "01.", [2] = "22. Coppa", [8] = "A PARCEL GILT SILVER BOWL \nOTTOMAN BALKANS, 16TH CENTURY" }),
        ("labelled-dimensions", "01", new() { [1] = "01.", [2] = "23. Vaso\nh 30 x l 45 cm" }),
        ("unclear-dimensions", "01", new() { [1] = "01.", [2] = "24. Vaso\nh 30 x aperto 45 cm" }),
        ("empty-row", "01", new() { [1] = "01." }),
        ("group-from-heading-only", "03", new() { [2] = "25. Coppa" }),
    ];

    [Fact]
    public void Rows_of_the_corpus_are_mapped_and_evaluated_as_recorded()
    {
        var rows = Corpus.Select((c, i) => new SheetRecord(i + 2, c.Group, "CINA", c.Cells, [], [])).ToList();
        var mapping = MappingSuggestion.Suggest(Headings, rows, "CERAMICHE") with
        {
            Dimensions = new DimensionsOptions(DimensionsParserTests.Cm, [MeasureKind.Height, MeasureKind.Width]),
        };
        var context = new EvaluationContext(mapping, Headings, null, new HashSet<string>(), _ => new ResolvedPlace(null, false, null),
            new Dictionary<string, DateResolution>(), SheetName: "CERAMICHE",
            ListValue: (list, text) => new ResolvedEntry(EntryFor(list, text), false), Units: DimensionsParserTests.Units);

        var evaluated = Corpus.Select((c, i) => (c.Name, Row: RowEvaluator.Evaluate(
            new StagedRow(i + 2, c.Group, "CINA", c.Cells, [], []), context)));
        var actual = JsonSerializer.Serialize(evaluated.ToDictionary(e => e.Name, e => Summarise(e.Row)), Pretty) + "\n";

        Assert.Equal(Golden("rows.golden.json", actual), actual.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Suggested_mapping_for_the_corpus_is_as_recorded()
    {
        var rows = Corpus.Select((c, i) => new SheetRecord(i + 2, c.Group, "CINA", c.Cells, [], [])).ToList();
        var actual = JsonSerializer.Serialize(MappingSuggestion.Suggest(Headings, rows, "CERAMICHE"), Pretty) + "\n";

        Assert.Equal(Golden("suggested-mapping.golden.json", actual), actual.ReplaceLineEndings("\n"));
    }

    /// <summary>
    /// A mapping and decisions saved by an earlier version still load, and write back unchanged. Refactors keep the field
    /// and detector names and the document shape that stored jobs and templates hold.
    /// </summary>
    [Fact]
    public void Stored_mapping_and_decisions_load_and_round_trip()
    {
        var mappingJson = File.ReadAllText(Path("stored-mapping.json"));
        var decisionsJson = File.ReadAllText(Path("stored-decisions.json"));

        var mapping = ImportJson.Read<ImportMapping>(mappingJson);
        var decisions = ImportJson.Read<ImportDecisions>(decisionsJson);

        Assert.Equal(Normalise(mappingJson), Normalise(ImportJson.Write(mapping)));
        Assert.Equal(Normalise(decisionsJson), Normalise(ImportJson.Write(decisions)));
        // Every field and detector the fixture names is a real one: renaming either would orphan stored mappings.
        Assert.Contains(mapping.Columns.SelectMany(c => c.Lines ?? []), l => l.Detector == DetectorIds.Money && l.Target == ImportField.AcquisitionPrice);
        Assert.Contains(mapping.Columns, c => c.Target == ImportField.CatalogueEntry && c.Language == "en");
        Assert.NotEmpty(decisions.Places);
        Assert.NotEmpty(decisions.Dates);
        Assert.NotEmpty(decisions.Lists);
        Assert.NotEmpty(decisions.Dimensions!);
    }

    // ---------- helpers ----------

    /// <summary>What a refactor must not change about a row, without the staged input and with stable identifiers.</summary>
    private static object Summarise(EvaluatedRow row) => new
    {
        row.InventoryNumber,
        Status = row.Status.ToString(),
        Mapped = row.Mapped.Values.Select(v => new { Field = v.Field.ToString(), v.Text, v.Column, v.Line, v.Parsed, v.Guessed }),
        row.Mapped.Dropped,
        row.Mapped.Discarded,
        row.Problems,
        row.Warnings,
        row.PlaceText,
        row.DateText,
        row.DateUnresolved,
        ListTexts = row.ListTexts.Select(t => $"{t.List}/{t.Field}: {t.Text}"),
        row.DimensionsText,
        row.DimensionsUnclear,
        row.Request,
    };

    private static Guid EntryFor(ImportList list, string text) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes($"{list}:{NameMatcher.Normalize(text)}")));

    private static string Normalise(string json) =>
        JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement, Pretty).ReplaceLineEndings("\n");

    private static string Path(string file, [CallerFilePath] string source = "") =>
        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(source)!, "ImportGolden", file);

    /// <summary>The recorded text of <paramref name="file"/>; with UPDATE_GOLDEN=1 the file is first rewritten from <paramref name="actual"/>.</summary>
    private static string Golden(string file, string actual, [CallerFilePath] string source = "")
    {
        var path = Path(file, source);
        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual.ReplaceLineEndings("\n"));
        }

        return File.ReadAllText(path).ReplaceLineEndings("\n");
    }
}
