using Artefacts.Application.Contracts;

namespace Artefacts.Application.Imports;

/// <summary>How many values a field takes from one row.</summary>
public enum FieldCardinality
{
    /// <summary>One value; further values found by line rules go to the column's rest field instead.</summary>
    Single = 0,

    /// <summary>Every value found is kept and the values are joined.</summary>
    Multi = 1,
}

/// <summary>How the values of a multi-valued field are joined into one text.</summary>
public enum JoinMode
{
    Space = 0,
    LineBreak = 1,
    Semicolon = 2,
}

/// <summary>How a column mapped as a whole to a field is cut into values.</summary>
public enum CellShape
{
    /// <summary>The cell's lines joined with spaces into one value.</summary>
    Lines = 0,

    /// <summary>The cell as written, line breaks kept, as one value.</summary>
    Text = 1,

    /// <summary>One value per paragraph (blank lines separate paragraphs), its lines joined with commas.</summary>
    Paragraphs = 2,
}

/// <summary>
/// One catalogue field that workbook text can be mapped to: what it is called, how its values are collected from a
/// cell (<see cref="Shape"/>, <see cref="Parse"/>), how several values are combined (<see cref="Cardinality"/>,
/// <see cref="Join"/>), the managed list its text is curated against, and how it is applied to the artefact being
/// built (<see cref="Apply"/>). The identifier is stored in mappings and templates and must stay stable.
/// </summary>
public sealed record FieldDefinition(
    ImportField Id,
    string Label,
    FieldCardinality Cardinality = FieldCardinality.Multi,
    JoinMode Join = JoinMode.Space,
    CellShape Shape = CellShape.Lines,
    Func<string, DetectorRegistry, DetectionContext, Detection?>? Parse = null,
    ListSpec? List = null,
    Action<RowDraft>? Apply = null)
{
    /// <summary>The identifier as stored in mappings (camel case).</summary>
    public string Key => char.ToLowerInvariant(Id.ToString()[0]) + Id.ToString()[1..];

    public string Separator => Join switch { JoinMode.LineBreak => "\n", JoinMode.Semicolon => "; ", _ => " " };
}

/// <summary>The managed list a field's text is curated against, with the wording of the problem when it is not.</summary>
public sealed record ListSpec(ImportList List, string ProblemField, string Noun);

/// <summary>
/// The fields a mapping can refer to, by identifier. <see cref="All"/> is in the order the fields are applied to a
/// row (which is the order problems are reported in); <see cref="Ordered"/> is the order the mapping step offers them.
/// </summary>
public sealed class FieldRegistry
{
    private readonly Dictionary<ImportField, FieldDefinition> _byId;

    public FieldRegistry(IEnumerable<FieldDefinition> fields)
    {
        All = fields.ToList();
        _byId = All.ToDictionary(f => f.Id);
        Ordered = All.OrderBy(f => (int)f.Id).ToList();
    }

    public IReadOnlyList<FieldDefinition> All { get; }

    public IReadOnlyList<FieldDefinition> Ordered { get; }

    public FieldDefinition Get(ImportField field) => _byId[field];

    public bool IsSingleValued(ImportField field) => Get(field).Cardinality == FieldCardinality.Single;

    /// <summary>The fields that ship with the application.</summary>
    public static FieldRegistry BuiltIn { get; } = new(
    [
        new(ImportField.Title, "Title", Apply: d =>
        {
            d.Title = d.Joined(ImportField.Title);
            if (d.Title is null) d.Problems.Add(new RowProblem("title", null, "The row has no title."));
        }),
        new(ImportField.InventoryNumber, "Inventory number", FieldCardinality.Single, Apply: d =>
        {
            // The row's own group number (column A) is more specific than the heading above it, which can disagree.
            var group = d.Joined(ImportField.GroupNumber) ?? d.Row.GroupNumber;
            d.InventoryNumber = d.Joined(ImportField.InventoryNumber)
                                ?? InventoryNumbers.Build(d.Mapping.InventoryNumber,
                                    new InventoryNumberValues(group, d.Mapped.First(ImportField.ItemNumber)?.Text, d.Row.SheetRow, d.Joined,
                                        d.Context.SheetName is { } sheet ? MappingSuggestion.Prefix(sheet) : null));
            if (d.InventoryNumber is null)
                d.Problems.Add(new RowProblem("inventoryNumber", null,
                    $"The inventory number pattern \"{d.Mapping.InventoryNumber.Pattern}\" needs a value this row does not have."));
        }),
        new(ImportField.CreationDate, "Date", FieldCardinality.Single,
            Parse: (text, detectors, context) => detectors.Detect(DetectorIds.HistoricalDate, text, context) ?? new Detection(text, new ParsedDateText(text, null)),
            Apply: ApplyCreationDate),
        new(ImportField.Dimensions, "Dimensions", Join: JoinMode.Semicolon, Apply: ApplyDimensions),
        new(ImportField.PlaceOfProduction, "Place of production", Apply: d =>
        {
            d.PlaceText = d.Joined(ImportField.PlaceOfProduction);
            if (d.PlaceText is null) return;
            d.Place = new PlaceOfProductionInput(d.PlaceText, d.Context.Place(d.PlaceText).RegionId);
        }),
        new(ImportField.CollectionGroup, "Collection group", List: new ListSpec(ImportList.CollectionGroup, "collectionGroup", "collection group"),
            Apply: d => d.CollectionGroupId = d.Entry(ImportField.CollectionGroup,
                // A mapped column is more specific than the group heading above the row.
                d.Joined(ImportField.CollectionGroup) ?? d.Row.GroupLabel)),
        new(ImportField.Dynasty, "Dynasty", List: new ListSpec(ImportList.Dynasty, "dynasty", "dynasty"),
            Apply: d => d.DynastyId = d.Entry(ImportField.Dynasty, d.Joined(ImportField.Dynasty))),
        new(ImportField.StorageLocation, "Storage location", List: new ListSpec(ImportList.StorageLocation, "storageLocation", "storage location"),
            Apply: d => d.StorageLocationId = d.Entry(ImportField.StorageLocation, d.Joined(ImportField.StorageLocation))),
        new(ImportField.Holder, "Holder", List: new ListSpec(ImportList.Party, "holder", "party"),
            Apply: d => d.HolderId = d.Entry(ImportField.Holder, d.Joined(ImportField.Holder))),
        new(ImportField.AcquisitionNote, "Acquisition note", Join: JoinMode.LineBreak, Shape: CellShape.Text,
            Apply: d => d.AcquisitionNotes.AddRange(d.Mapped.Of(ImportField.AcquisitionNote).Select(v => v.Text))),
        new(ImportField.AcquisitionDate, "Acquisition date", FieldCardinality.Single,
            Parse: (text, detectors, context) => detectors.Detect(DetectorIds.CalendarDate, text, context), Apply: ApplyAcquisitionDate),
        new(ImportField.AcquisitionPrice, "Price", FieldCardinality.Single,
            Parse: (text, detectors, context) => detectors.Detect(DetectorIds.Money, text, context), Apply: ApplyAcquisitionPrice),
        new(ImportField.AcquisitionSource, "Acquired from", List: new ListSpec(ImportList.Party, "acquisition.source", "party"), Apply: d =>
        {
            d.AcquisitionSourceText = d.Joined(ImportField.AcquisitionSource);
            d.AcquisitionSourceId = d.Entry(ImportField.AcquisitionSource, d.AcquisitionSourceText);
        }),
        new(ImportField.AcquisitionLot, "Lot", FieldCardinality.Single, Apply: d => d.AcquisitionLot = d.Mapped.First(ImportField.AcquisitionLot)?.Text),
        new(ImportField.Valuation, "Valuation", FieldCardinality.Single,
            Parse: (text, detectors, context) => detectors.Detect(DetectorIds.Money, text, context), Apply: d =>
            {
                if (d.Mapped.First(ImportField.Valuation) is not { } value) return;
                if (value.Parsed is ParsedMoney money) d.Valuation = new MoneyDto(money.Amount, money.Currency);
                else d.Problems.Add(new RowProblem("valuation", value.Text, "No amount with a currency could be read from this text."));
            }),
        new(ImportField.Description, "Description", Join: JoinMode.LineBreak, Shape: CellShape.Text,
            Apply: d => d.Description = d.Joined(ImportField.Description)),
        new(ImportField.Maker, "Maker", Apply: d => d.Maker = d.Joined(ImportField.Maker)),
        new(ImportField.Medium, "Medium", Apply: d => d.Medium = d.Joined(ImportField.Medium)),
        new(ImportField.Exhibitions, "Exhibitions", Shape: CellShape.Paragraphs,
            Apply: d => d.Exhibitions.AddRange(d.Mapped.Of(ImportField.Exhibitions).Select(v => v.Text))),
        new(ImportField.CatalogueEntry, "Catalogue entry", Join: JoinMode.LineBreak, Shape: CellShape.Text, Apply: ApplyCatalogueEntries),

        // Read by the inventory number pattern and the review, not applied to the artefact on their own.
        new(ImportField.ItemNumber, "Item number", FieldCardinality.Single),
        new(ImportField.GroupNumber, "Group number", FieldCardinality.Single),
        new(ImportField.CatalogueNumber, "Catalogue number", FieldCardinality.Single,
            Parse: (text, detectors, context) => detectors.Detect(DetectorIds.CatalogueNumber, text, context)),
    ]);

    private static void ApplyCreationDate(RowDraft d)
    {
        if (d.Mapped.First(ImportField.CreationDate) is not { } value) return;
        d.DateText = value.Text;
        var parsed = value.Parsed as ParsedDateText;
        if (parsed?.Interpretation is not null)
        {
            d.CreationDate = new HistoricalDateInput(value.Text);
        }
        else if (d.Context.DateResolutions.TryGetValue(RowEvaluator.DateKey(value.Text), out var resolution))
        {
            d.CreationDate = new HistoricalDateInput(value.Text, resolution.EarliestYear, resolution.LatestYear);
        }
        else
        {
            d.DateUnresolved = true;
            d.Problems.Add(new RowProblem("creationDate", value.Text,
                "The date is not understood: give it a year range in the dates step, or correct it in the workbook."));
        }
    }

    /// <summary>Measurements are read from the dimensions text when it is clear; otherwise the decision taken in the dimensions step applies.</summary>
    private static void ApplyDimensions(RowDraft d)
    {
        d.DimensionsText = d.Joined(ImportField.Dimensions);
        if (d.DimensionsText is null) return;
        var context = d.Context;
        if (DimensionsParser.Parse(d.DimensionsText, context.Units ?? [], d.Mapping.Dimensions, context.Tables) is { } parsed)
        {
            foreach (var (kind, input) in parsed.Measurements) d.Measured[kind] = input;
            return;
        }

        d.DimensionsUnclear = true;
        if (context.DimensionResolutions?.GetValueOrDefault(RowEvaluator.DateKey(d.DimensionsText)) is { } resolution && IsUsable(resolution, context.Units))
        {
            d.DimensionsResolution = resolution;
            if (!resolution.KeepTextOnly)
                foreach (var (kind, input) in resolution.Measurements) d.Measured[kind] = input;
        }
        else
        {
            d.Problems.Add(new RowProblem("dimensions", d.DimensionsText,
                "The measurements could not be read: enter them, or keep the text only, in the dimensions step."));
        }
    }

    /// <summary>A resolution whose units have since been deleted no longer counts as a decision.</summary>
    private static bool IsUsable(DimensionResolution resolution, IReadOnlyList<ParserUnit>? units) =>
        resolution.KeepTextOnly ||
        (resolution.Measurements.Any() &&
         (units is null || resolution.Measurements.All(m => m.Input.UnitId is { } id && units.Any(u => u.Id == id))));

    private static void ApplyAcquisitionDate(RowDraft d)
    {
        if (d.Mapped.First(ImportField.AcquisitionDate) is not { } value) return;
        if (value.Parsed is DateOnly date)
        {
            d.AcquisitionDate = date;
        }
        else
        {
            d.AcquisitionNotes.Add(value.Text);
            d.Warnings.Add($"\"{value.Text}\" is not a calendar date; it was kept in the acquisition note.");
        }
    }

    private static void ApplyAcquisitionPrice(RowDraft d)
    {
        if (d.Mapped.First(ImportField.AcquisitionPrice) is not { } value) return;
        if (value.Parsed is ParsedMoney money)
        {
            d.AcquisitionPrice = new MoneyDto(money.Amount, money.Currency);
            // A separator convention was needed: keep the price as written next to the reading.
            if (value.Guessed) d.AcquisitionNotes.Add(value.Text);
        }
        else
        {
            d.Problems.Add(new RowProblem("acquisition.price", value.Text, "No amount with a currency could be read from this text."));
        }
    }

    /// <summary>Each catalogue entry column has the language of its rule (English when the rule gives none); columns in the same language are joined.</summary>
    private static void ApplyCatalogueEntries(RowDraft d)
    {
        var languages = MappingEngine.Resolve(d.Mapping, d.Context.Headings)
            .GroupBy(r => r.Column).ToDictionary(g => g.Key, g => g.First().Rule.Language ?? "en");
        foreach (var language in d.Mapped.Of(ImportField.CatalogueEntry).Where(v => v.Text.Length > 0)
                     .GroupBy(v => languages.GetValueOrDefault(v.Column, "en")))
            d.CatalogueEntries[language.Key] = string.Join("\n", language.Select(v => v.Text));
    }
}

/// <summary>
/// The artefact being built from one row: each field definition's <see cref="FieldDefinition.Apply"/> fills in its part,
/// then <see cref="ToRequest"/> assembles the write request. Fields that go together (the acquisition) are assembled here.
/// </summary>
public sealed class RowDraft(StagedRow row, EvaluationContext context, MappedRecord mapped, FieldRegistry fields, List<string> warnings)
{
    public StagedRow Row { get; } = row;
    public EvaluationContext Context { get; } = context;
    public ImportMapping Mapping => Context.Mapping;
    public MappedRecord Mapped { get; } = mapped;
    public List<RowProblem> Problems { get; } = [];
    public List<string> Warnings { get; } = warnings;
    public List<ListText> ListTexts { get; } = [];

    public string? Title { get; set; }
    public string? InventoryNumber { get; set; }
    public string? Maker { get; set; }
    public string? Medium { get; set; }
    public string? Description { get; set; }
    public HistoricalDateInput? CreationDate { get; set; }
    public string? DateText { get; set; }
    public bool DateUnresolved { get; set; }
    public string? DimensionsText { get; set; }
    public bool DimensionsUnclear { get; set; }
    public DimensionResolution? DimensionsResolution { get; set; }
    public Dictionary<MeasureKind, MeasurementInput> Measured { get; } = [];
    public string? PlaceText { get; set; }
    public PlaceOfProductionInput? Place { get; set; }
    public Guid? CollectionGroupId { get; set; }
    public Guid? DynastyId { get; set; }
    public Guid? StorageLocationId { get; set; }
    public Guid? HolderId { get; set; }
    public List<string> AcquisitionNotes { get; } = [];
    public DateOnly? AcquisitionDate { get; set; }
    public MoneyDto? AcquisitionPrice { get; set; }
    public string? AcquisitionSourceText { get; set; }
    public Guid? AcquisitionSourceId { get; set; }
    public string? AcquisitionLot { get; set; }
    public MoneyDto? Valuation { get; set; }
    public List<string> Exhibitions { get; } = [];
    public Dictionary<string, string?> CatalogueEntries { get; } = [];

    /// <summary>The values mapped to a field, joined the way its definition says; null when there are none.</summary>
    public string? Joined(ImportField field)
    {
        var values = Mapped.Of(field).Select(v => v.Text).Where(t => t.Length > 0).ToList();
        return values.Count == 0 ? null : string.Join(fields.Get(field).Separator, values);
    }

    /// <summary>
    /// Curates a text against the field's list: every text must be linked to an entry or marked for creation (empty
    /// cells need no decision). Returns the linked entry.
    /// </summary>
    public Guid? Entry(ImportField field, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var spec = fields.Get(field).List!;
        text = text.Trim();
        ListTexts.Add(new ListText(spec.List, field, text));
        var resolved = Context.ListValue?.Invoke(spec.List, text) ?? ResolvedEntry.Undecided;
        if (!resolved.IsDecided)
            Problems.Add(new RowProblem(spec.ProblemField, text, $"Link \"{text}\" to a {spec.Noun} or create one in the lists step."));
        return resolved.EntryId;
    }

    public ArtefactWriteRequest ToRequest()
    {
        var note = AcquisitionNotes.Count > 0 ? string.Join("\n", AcquisitionNotes) : null;
        var acquisition = AcquisitionSourceText is not null || AcquisitionDate is not null || AcquisitionLot is not null || AcquisitionPrice is not null || note is not null
            ? new AcquisitionInput(Mapping.AcquisitionMethod, AcquisitionDate, AcquisitionSourceId, AcquisitionPrice, AcquisitionLot, note)
            : null;
        return new ArtefactWriteRequest(
            InventoryNumber,
            Title,
            Maker,
            CreationDate,
            Medium,
            DimensionsText,
            Description is null ? null : new Dictionary<string, string?> { [Mapping.Language] = Description },
            acquisition,
            null,
            Context.ObjectTypeId,
            CollectionGroupId,
            Place,
            DynastyId,
            Exhibitions,
            CatalogueEntries.Count == 0 ? null : CatalogueEntries,
            HolderId,
            StorageLocationId,
            Valuation,
            Measured.GetValueOrDefault(MeasureKind.Width),
            Measured.GetValueOrDefault(MeasureKind.Height),
            Measured.GetValueOrDefault(MeasureKind.Length),
            Measured.GetValueOrDefault(MeasureKind.Diameter));
    }
}
