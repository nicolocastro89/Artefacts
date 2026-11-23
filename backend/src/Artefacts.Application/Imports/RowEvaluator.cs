using Artefacts.Application.Contracts;
using Artefacts.Domain.Imports;

namespace Artefacts.Application.Imports;

/// <summary>Why a row cannot be imported: the field, the text as written, and the reason.</summary>
public sealed record RowProblem(string Field, string? Text, string Reason);

/// <summary>A picture staged from the workbook for one row.</summary>
public sealed record StagedPicture(int Index, string Name, string StorageKey, string ContentType, long Size, Domain.Media.ImageFormat? Format)
{
    public bool Supported => Format is not null;
}

/// <summary>A record row as staged when the workbook was read.</summary>
public sealed record StagedRow(
    int SheetRow,
    string? GroupNumber,
    string? GroupLabel,
    IReadOnlyDictionary<int, string> Cells,
    IReadOnlyList<StagedPicture> Pictures,
    IReadOnlyList<string> Warnings);

/// <summary>A place text resolved for import: the linked region, or a region still to be created.</summary>
public sealed record ResolvedPlace(Guid? RegionId, bool PendingCreate, PlaceAction? Decided);

/// <summary>
/// A list text resolved for import: the linked entry or party, or one still to be created at commit (which counts as
/// decided). Neither means the text is undecided.
/// </summary>
public sealed record ResolvedEntry(Guid? EntryId, bool PendingCreate)
{
    public static readonly ResolvedEntry Undecided = new(null, false);
    public bool IsDecided => EntryId is not null || PendingCreate;
}

/// <summary>A text of a row that is curated against a list, with the field it came from.</summary>
public sealed record ListText(ImportList List, ImportField Field, string Text);

/// <summary>Everything the evaluation needs besides the rows.</summary>
public sealed record EvaluationContext(
    ImportMapping Mapping,
    IReadOnlyDictionary<int, string> Headings,
    Guid? ObjectTypeId,
    IReadOnlySet<string> ExistingInventoryNumbers,
    Func<string, ResolvedPlace> Place,
    IReadOnlyDictionary<string, DateResolution> DateResolutions,
    string? SheetName = null,
    Func<ImportList, string, ResolvedEntry>? ListValue = null,
    IReadOnlyList<ParserUnit>? Units = null,
    IReadOnlyDictionary<string, DimensionResolution>? DimensionResolutions = null);

/// <summary>A row as it will be imported.</summary>
public sealed record EvaluatedRow(
    StagedRow Row,
    string? InventoryNumber,
    ImportRowStatus Status,
    ArtefactWriteRequest? Request,
    MappedRecord Mapped,
    IReadOnlyList<RowProblem> Problems,
    IReadOnlyList<string> Warnings,
    string? PlaceText,
    string? DateText,
    bool DateUnresolved,
    IReadOnlyList<ListText> ListTexts,
    string? DimensionsText = null,
    bool DimensionsUnclear = false,
    DimensionResolution? DimensionsResolution = null);

/// <summary>
/// Turns staged rows, the mapping and the decisions into would-be artefact requests, in sheet order: the first row
/// with an inventory number wins and later repeats are problems naming it. Pure, so the review, the commit and the
/// tests agree; domain validation of each request is added by the caller.
/// </summary>
public static class RowEvaluator
{
    public static IReadOnlyList<EvaluatedRow> Evaluate(IReadOnlyList<StagedRow> rows, EvaluationContext context)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<EvaluatedRow>(rows.Count);
        foreach (var row in rows.OrderBy(r => r.SheetRow))
        {
            var evaluated = Evaluate(row, context);
            if (evaluated.InventoryNumber is { } number)
            {
                if (seen.TryGetValue(number, out var first))
                {
                    evaluated = evaluated with
                    {
                        Status = ImportRowStatus.Problem,
                        Problems = [.. evaluated.Problems, new RowProblem("inventoryNumber", number, $"Row {first} has the same inventory number.")],
                    };
                }
                else
                {
                    seen[number] = row.SheetRow;
                }
            }

            result.Add(evaluated);
        }

        return result;
    }

    public static EvaluatedRow Evaluate(StagedRow row, EvaluationContext context)
    {
        var mapping = context.Mapping;
        var mapped = MappingEngine.Map(row.Cells, mapping, context.Headings);
        var problems = new List<RowProblem>();
        var warnings = new List<string>(row.Warnings);
        warnings.AddRange(mapped.Dropped.Select(d => $"Not imported (the column has no field for remaining lines): {d}"));

        string? Joined(ImportField field)
        {
            var values = mapped.Of(field).Select(v => v.Text).Where(t => t.Length > 0).ToList();
            if (values.Count == 0) return null;
            var separator = MappingEngine.LongText.Contains(field) ? "\n" : field == ImportField.Dimensions ? "; " : " ";
            return string.Join(separator, values);
        }

        // ---------- identity ----------
        var title = Joined(ImportField.Title);
        if (title is null) problems.Add(new RowProblem("title", null, "The row has no title."));

        // The row's own group number (column A) is more specific than the heading above it, which can disagree.
        var group = Joined(ImportField.GroupNumber) ?? row.GroupNumber;
        var inventoryNumber = Joined(ImportField.InventoryNumber)
                              ?? InventoryNumbers.Build(mapping.InventoryNumber,
                                  new InventoryNumberValues(group, mapped.First(ImportField.ItemNumber)?.Text, row.SheetRow, Joined,
                                      context.SheetName is { } sheet ? MappingSuggestion.Prefix(sheet) : null));
        if (inventoryNumber is null)
            problems.Add(new RowProblem("inventoryNumber", null,
                $"The inventory number pattern \"{mapping.InventoryNumber.Pattern}\" needs a value this row does not have."));

        // ---------- dating ----------
        HistoricalDateInput? creationDate = null;
        var dateValue = mapped.First(ImportField.CreationDate);
        var dateUnresolved = false;
        if (dateValue is not null)
        {
            var parsed = dateValue.Parsed as ParsedDateText;
            if (parsed?.Interpretation is not null)
            {
                creationDate = new HistoricalDateInput(dateValue.Text);
            }
            else if (context.DateResolutions.TryGetValue(DateKey(dateValue.Text), out var resolution))
            {
                creationDate = new HistoricalDateInput(dateValue.Text, resolution.EarliestYear, resolution.LatestYear);
            }
            else
            {
                dateUnresolved = true;
                problems.Add(new RowProblem("creationDate", dateValue.Text,
                    "The date is not understood: give it a year range in the dates step, or correct it in the workbook."));
            }
        }

        // ---------- measurements ----------
        // Read from the dimensions text when it is clear; otherwise the decision taken in the dimensions step applies.
        var dimensionsText = Joined(ImportField.Dimensions);
        var measured = new Dictionary<MeasureKind, MeasurementInput>();
        var dimensionsUnclear = false;
        DimensionResolution? dimensionsResolution = null;
        if (dimensionsText is not null)
        {
            if (DimensionsParser.Parse(dimensionsText, context.Units ?? [], mapping.Dimensions) is { } parsed)
            {
                foreach (var (kind, input) in parsed.Measurements) measured[kind] = input;
            }
            else
            {
                dimensionsUnclear = true;
                if (context.DimensionResolutions?.GetValueOrDefault(DateKey(dimensionsText)) is { } resolution && IsUsable(resolution, context.Units))
                {
                    dimensionsResolution = resolution;
                    if (!resolution.KeepTextOnly)
                        foreach (var (kind, input) in resolution.Measurements) measured[kind] = input;
                }
                else
                {
                    problems.Add(new RowProblem("dimensions", dimensionsText,
                        "The measurements could not be read: enter them, or keep the text only, in the dimensions step."));
                }
            }
        }

        // ---------- origin ----------
        var placeText = Joined(ImportField.PlaceOfProduction);
        PlaceOfProductionInput? place = null;
        if (placeText is not null)
        {
            var resolved = context.Place(placeText);
            place = new PlaceOfProductionInput(placeText, resolved.RegionId);
        }

        // ---------- list values and parties ----------
        // Every text must be linked to an entry or marked for creation; empty cells need no decision.
        var listTexts = new List<ListText>();
        Guid? Entry(ImportList list, ImportField field, string? text, string problemField, string noun)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            listTexts.Add(new ListText(list, field, text));
            var resolved = context.ListValue?.Invoke(list, text) ?? ResolvedEntry.Undecided;
            if (!resolved.IsDecided)
                problems.Add(new RowProblem(problemField, text, $"Link \"{text}\" to a {noun} or create one in the lists step."));
            return resolved.EntryId;
        }

        // A mapped column is more specific than the group heading above the row.
        var collectionGroupId = Entry(ImportList.CollectionGroup, ImportField.CollectionGroup,
            Joined(ImportField.CollectionGroup) ?? row.GroupLabel, "collectionGroup", "collection group");
        var dynastyId = Entry(ImportList.Dynasty, ImportField.Dynasty, Joined(ImportField.Dynasty), "dynasty", "dynasty");
        var storageLocationId = Entry(ImportList.StorageLocation, ImportField.StorageLocation, Joined(ImportField.StorageLocation),
            "storageLocation", "storage location");
        var holderId = Entry(ImportList.Party, ImportField.Holder, Joined(ImportField.Holder), "holder", "party");

        // ---------- acquisition ----------
        var notes = mapped.Of(ImportField.AcquisitionNote).Select(v => v.Text).ToList();
        DateOnly? acquisitionDate = null;
        if (mapped.First(ImportField.AcquisitionDate) is { } dateOfAcquisition)
        {
            if (dateOfAcquisition.Parsed is DateOnly d) acquisitionDate = d;
            else
            {
                notes.Add(dateOfAcquisition.Text);
                warnings.Add($"\"{dateOfAcquisition.Text}\" is not a calendar date; it was kept in the acquisition note.");
            }
        }

        MoneyDto? price = null;
        if (mapped.First(ImportField.AcquisitionPrice) is { } priceValue)
        {
            if (priceValue.Parsed is ParsedMoney money)
            {
                price = new MoneyDto(money.Amount, money.Currency);
                // A separator convention was needed: keep the price as written next to the reading.
                if (priceValue.Guessed) notes.Add(priceValue.Text);
            }
            else
            {
                problems.Add(new RowProblem("acquisition.price", priceValue.Text, "No amount with a currency could be read from this text."));
            }
        }

        var source = Joined(ImportField.AcquisitionSource);
        var sourceId = Entry(ImportList.Party, ImportField.AcquisitionSource, source, "acquisition.source", "party");
        var lot = mapped.First(ImportField.AcquisitionLot)?.Text;
        var note = notes.Count > 0 ? string.Join("\n", notes) : null;
        var acquisition = source is not null || acquisitionDate is not null || lot is not null || price is not null || note is not null
            ? new AcquisitionInput(mapping.AcquisitionMethod, acquisitionDate, sourceId, price, lot, note)
            : null;

        // ---------- valuation ----------
        MoneyDto? valuation = null;
        if (mapped.First(ImportField.Valuation) is { } valuationValue)
        {
            if (valuationValue.Parsed is ParsedMoney money) valuation = new MoneyDto(money.Amount, money.Currency);
            else problems.Add(new RowProblem("valuation", valuationValue.Text, "No amount with a currency could be read from this text."));
        }

        var description = Joined(ImportField.Description);

        // Each catalogue entry column has the language of its rule (English when the rule gives none); columns in the
        // same language are joined.
        var columnLanguages = MappingEngine.Resolve(mapping, context.Headings)
            .GroupBy(r => r.Column).ToDictionary(g => g.Key, g => g.First().Rule.Language ?? "en");
        var catalogueEntries = mapped.Of(ImportField.CatalogueEntry).Where(v => v.Text.Length > 0)
            .GroupBy(v => columnLanguages.GetValueOrDefault(v.Column, "en"))
            .ToDictionary(g => g.Key, g => (string?)string.Join("\n", g.Select(v => v.Text)));
        foreach (var picture in row.Pictures.Where(p => !p.Supported))
            warnings.Add($"Picture \"{picture.Name}\" ({picture.ContentType}) is not in a format the catalogue accepts (JPEG, PNG, TIFF, WebP); the row is imported without it.");

        var request = new ArtefactWriteRequest(
            inventoryNumber,
            title,
            Joined(ImportField.Maker),
            creationDate,
            Joined(ImportField.Medium),
            Joined(ImportField.Dimensions),
            description is null ? null : new Dictionary<string, string?> { [mapping.Language] = description },
            acquisition,
            null,
            context.ObjectTypeId,
            collectionGroupId,
            place,
            dynastyId,
            mapped.Of(ImportField.Exhibitions).Select(v => v.Text).ToList(),
            catalogueEntries.Count == 0 ? null : catalogueEntries,
            holderId,
            storageLocationId,
            valuation,
            measured.GetValueOrDefault(MeasureKind.Width),
            measured.GetValueOrDefault(MeasureKind.Height),
            measured.GetValueOrDefault(MeasureKind.Length),
            measured.GetValueOrDefault(MeasureKind.Diameter));

        var status = problems.Count > 0
            ? ImportRowStatus.Problem
            : inventoryNumber is not null && context.ExistingInventoryNumbers.Contains(inventoryNumber)
                ? ImportRowStatus.Skipped
                : ImportRowStatus.Ready;

        return new EvaluatedRow(row, inventoryNumber, status, request, mapped, problems, warnings, placeText,
            dateValue?.Text, dateUnresolved, listTexts, dimensionsText, dimensionsUnclear, dimensionsResolution);
    }

    /// <summary>A resolution whose units have since been deleted no longer counts as a decision.</summary>
    private static bool IsUsable(DimensionResolution resolution, IReadOnlyList<ParserUnit>? units) =>
        resolution.KeepTextOnly ||
        (resolution.Measurements.Any() &&
         (units is null || resolution.Measurements.All(m => m.Input.UnitId is { } id && units.Any(u => u.Id == id))));

    /// <summary>Date resolutions apply to every row with the same text, ignoring case and spacing.</summary>
    public static string DateKey(string text) => string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
