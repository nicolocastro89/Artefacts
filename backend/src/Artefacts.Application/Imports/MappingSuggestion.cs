using System.Globalization;
using System.Text;

namespace Artefacts.Application.Imports;

/// <summary>A column as the mapping step shows it: heading, sample cells and how often each detector recognises a line.</summary>
public sealed record ColumnSummary(
    int Column,
    string Heading,
    IReadOnlyList<string> Samples,
    int FilledRows,
    IReadOnlyDictionary<LineDetector, double> HitRates);

/// <summary>Suggests a mapping from the column headings and the content of the first rows.</summary>
public static class MappingSuggestion
{
    public const int SampleRows = 20;

    public static IReadOnlyList<ColumnSummary> Summarise(IReadOnlyDictionary<int, string> headings, IReadOnlyList<SheetRecord> records)
    {
        var sample = records.Take(SampleRows).ToList();
        var columns = headings.Keys.Concat(records.SelectMany(r => r.Cells.Keys)).Distinct().Order();
        return columns.Select(column =>
        {
            var cells = sample.Select(r => r.Cells.GetValueOrDefault(column)).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).ToList();
            var lines = cells.SelectMany(c => MappingEngine.Lines(c).Select(l => l.Text)).ToList();
            var rates = Enum.GetValues<LineDetector>().ToDictionary(
                d => d,
                d => lines.Count == 0 ? 0 : Math.Round(lines.Count(l => Detectors.Detect(d, l) is not null) / (double)lines.Count, 2));
            return new ColumnSummary(column, headings.GetValueOrDefault(column) ?? "", cells.Take(3).ToList(),
                records.Count(r => r.Cells.ContainsKey(column)), rates);
        }).ToList();
    }

    public static ImportMapping Suggest(IReadOnlyDictionary<int, string> headings, IReadOnlyList<SheetRecord> records, string sheetName)
    {
        var summaries = Summarise(headings, records);
        var first = summaries.Count > 0 ? summaries.Min(s => s.Column) : 1;
        var rules = summaries.Select(s => Suggest(s, s.Column == first)).ToList();
        return new ImportMapping(
            rules,
            // {sheet} keeps a template usable for every workbook: each gets its own prefix ("CER", "TES", …).
            new InventoryNumberPattern("{sheet}-{group:2}-{item:2}", ImportField.CatalogueNumber),
            LocationPatterns: Detectors.DefaultLocationPatterns,
            // The collection measures in centimetres; numbers are never read in an assumed order.
            Dimensions: new DimensionsOptions(Domain.Measures.SeededUnits.Centimetre.Value));
    }

    /// <summary>The usual line rules for the multi-line columns of the collection's workbooks.</summary>
    public static class Rules
    {
        public static ColumnRule Holding(int column, string? heading) => new(column, heading,
            Lines: [new(ImportField.GroupNumber, LineDetector.GroupNumber), new(ImportField.CatalogueNumber, LineDetector.CatalogueNumber),
                new(ImportField.StorageLocation, LineDetector.LocationCode)],
            Rest: ImportField.Holder);

        public static ColumnRule Title(int column, string? heading) => new(column, heading,
            Lines: [new(ImportField.Title, LineDetector.ItemTitle, Continues: true), new(ImportField.Dimensions, LineDetector.Dimensions),
                // A title written without its item number.
                new(ImportField.Title, Position: 0, Continues: true)],
            Rest: ImportField.Description);

        public static ColumnRule Origin(int column, string? heading) => new(column, heading,
            Lines: [new(ImportField.CreationDate, LineDetector.HistoricalDate), new(ImportField.PlaceOfProduction, Position: 0, Continues: true)],
            Rest: ImportField.Dynasty);

        public static ColumnRule Acquisition(int column, string? heading) => new(column, heading,
            Lines: [new(ImportField.AcquisitionDate, LineDetector.CalendarDate), new(ImportField.AcquisitionLot, LineDetector.Lot),
                new(ImportField.AcquisitionPrice, LineDetector.Money), new(ImportField.AcquisitionSource, Position: 0, Continues: true)],
            Rest: ImportField.AcquisitionNote);
    }

    /// <summary>The language a catalogue entry column says it is in; English when the heading does not say.</summary>
    private static string EntryLanguage(string foldedHeading) =>
        foldedHeading.Contains("italian") ? "it" : "en";

    private static ColumnRule Suggest(ColumnSummary s, bool isFirst)
    {
        var h = Fold(s.Heading);
        bool Has(params string[] words) => words.Any(h.Contains);

        if (isFirst && (Has("gruppo", "numero", "colonna", "group", "number") || s.HitRates[LineDetector.GroupNumber] > 0.3))
            return Rules.Holding(s.Column, s.Heading);
        if (Has("nome", "titolo", "title", "oggetto")) return Rules.Title(s.Column, s.Heading);
        if (Has("zona", "produzione", "origine", "luogo")) return Rules.Origin(s.Column, s.Heading);
        if (Has("provenienza", "acquist", "acquisiz", "fornitore", "venditore")) return Rules.Acquisition(s.Column, s.Heading);
        if (Has("mostr", "esposizion", "exhibition")) return Whole(s, ImportField.Exhibitions);
        if (Has("william", "english", "inglese", "catalog")) return Whole(s, ImportField.CatalogueEntry) with { Language = EntryLanguage(h) };
        if (Has("valore", "stima", "valuation", "value")) return Whole(s, ImportField.Valuation);
        if (Has("formato", "immagine", "image", "foto")) return new ColumnRule(s.Column, s.Heading);
        if (Has("dinastia", "dynasty")) return Whole(s, ImportField.Dynasty);
        if (Has("misure", "dimension")) return Whole(s, ImportField.Dimensions);
        if (Has("data", "date", "datazione")) return Whole(s, ImportField.CreationDate);
        if (Has("materia", "tecnica", "medium", "material")) return Whole(s, ImportField.Medium);
        if (Has("autore", "artista", "maker", "artist")) return Whole(s, ImportField.Maker);
        if (Has("collocazione", "ubicazione", "location")) return Whole(s, ImportField.StorageLocation);
        if (Has("proprietario", "holder", "owner")) return Whole(s, ImportField.Holder);
        if (Has("descrizione", "description", "note")) return Whole(s, ImportField.Description);

        // Unknown heading: an empty column is ignored; otherwise the content decides, and text is never lost.
        if (s.FilledRows == 0) return new ColumnRule(s.Column, s.Heading);
        if (s.HitRates[LineDetector.Money] > 0.5) return Whole(s, ImportField.Valuation);
        if (s.HitRates[LineDetector.Dimensions] > 0.5) return Whole(s, ImportField.Dimensions);
        return Whole(s, ImportField.Description);
    }

    private static ColumnRule Whole(ColumnSummary s, ImportField field) => new(s.Column, s.Heading, field);

    /// <summary>Three capital letters from the sheet name ("CERAMICHE" → "CER").</summary>
    public static string Prefix(string sheetName)
    {
        var letters = new string(Fold(sheetName).Where(char.IsAsciiLetter).Take(3).ToArray()).ToUpperInvariant();
        return letters.Length == 0 ? "IMP" : letters;
    }

    /// <summary>Lower case without accents.</summary>
    public static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
