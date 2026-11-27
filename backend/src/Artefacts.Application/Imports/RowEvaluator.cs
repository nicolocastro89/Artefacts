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
    IReadOnlyDictionary<string, DimensionResolution>? DimensionResolutions = null,
    LookupTables? Tables = null,
    DetectorRegistry? Detectors = null);

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

    public static EvaluatedRow Evaluate(StagedRow row, EvaluationContext context, FieldRegistry? fields = null)
    {
        fields ??= FieldRegistry.BuiltIn;
        var mapped = MappingEngine.Map(row.Cells, context.Mapping, context.Headings, detectors: context.Detectors, fields: fields, tables: context.Tables);
        var warnings = new List<string>(row.Warnings);
        warnings.AddRange(mapped.Dropped.Select(d => $"Not imported (the column has no field for remaining lines): {d}"));

        var draft = new RowDraft(row, context, mapped, fields, warnings);
        foreach (var field in fields.All) field.Apply?.Invoke(draft);

        foreach (var picture in row.Pictures.Where(p => !p.Supported))
            warnings.Add($"Picture \"{picture.Name}\" ({picture.ContentType}) is not in a format the catalogue accepts (JPEG, PNG, TIFF, WebP); the row is imported without it.");

        var inventoryNumber = draft.InventoryNumber;
        var status = draft.Problems.Count > 0
            ? ImportRowStatus.Problem
            : inventoryNumber is not null && context.ExistingInventoryNumbers.Contains(inventoryNumber)
                ? ImportRowStatus.Skipped
                : ImportRowStatus.Ready;

        return new EvaluatedRow(row, inventoryNumber, status, draft.ToRequest(), mapped, draft.Problems, warnings, draft.PlaceText,
            draft.DateText, draft.DateUnresolved, draft.ListTexts, draft.DimensionsText, draft.DimensionsUnclear, draft.DimensionsResolution);
    }

    /// <summary>Date resolutions apply to every row with the same text, ignoring case and spacing.</summary>
    public static string DateKey(string text) => string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
