namespace Artefacts.Application.Imports;

/// <summary>
/// The identifiers of the built-in detectors, as stored in mappings and templates (camel case, so a mapping written
/// when detectors were an enum still loads). Other detectors, such as user-defined ones, bring their own identifiers.
/// </summary>
public static class DetectorIds
{
    public const string ItemTitle = "itemTitle";
    public const string GroupNumber = "groupNumber";
    public const string Dimensions = "dimensions";
    public const string HistoricalDate = "historicalDate";
    public const string CalendarDate = "calendarDate";
    public const string Lot = "lot";
    public const string Money = "money";
    public const string CatalogueNumber = "catalogueNumber";
    public const string LocationCode = "locationCode";
}

/// <summary>What a detector may need from the mapping it runs under.</summary>
public sealed record DetectionContext(IReadOnlyList<string> LocationPatterns, LookupTables? Tables = null)
{
    /// <summary>The lookup tables the detectors read (the built-in ones unless the context brings its own).</summary>
    public LookupTables Lookup => Tables ?? LookupTables.BuiltIn;

    public static readonly DetectionContext Default = new(Detectors.DefaultLocationPatterns);

    public static DetectionContext For(ImportMapping mapping, LookupTables? tables = null) =>
        new(mapping.LocationPatterns ?? Detectors.DefaultLocationPatterns, tables);
}

/// <summary>
/// Recognises one kind of line of a cell: "an amount", "a lot", "an item number and title". Detectors are pure, so
/// preview, review and import agree. A line rule names its detector by <see cref="Id"/>.
/// </summary>
public interface ILineDetector
{
    /// <summary>The stable identifier stored in mappings.</summary>
    string Id { get; }

    /// <summary>What the mapping step calls this detector.</summary>
    string Label => Id;

    /// <summary>A line it recognises, shown next to the label.</summary>
    string? Example => null;

    /// <summary>
    /// The definition that recognises the same lines, for a detector whose rules can be written as one, so it can be
    /// copied and changed; null for detectors that only exist as code.
    /// </summary>
    DetectorDefinition? Definition => null;

    /// <summary>The recognised text and what is left of the line, or null when the line is not of this kind.</summary>
    Detection? Detect(string line, DetectionContext context);
}

/// <summary>The detectors a mapping can refer to, by identifier.</summary>
public sealed class DetectorRegistry
{
    private readonly Dictionary<string, ILineDetector> _byId;

    public DetectorRegistry(IEnumerable<ILineDetector> detectors)
    {
        Detectors = detectors.ToList();
        _byId = Detectors.ToDictionary(d => d.Id, StringComparer.Ordinal);
    }

    /// <summary>The detectors that ship with the application.</summary>
    public static DetectorRegistry BuiltIn { get; } = new(
    [
        new BuiltInDetector(DetectorIds.ItemTitle, "Numbered title", "01. Coppa Celadon", (line, _) => Imports.Detectors.ItemTitle(line)),
        new BuiltInDetector(DetectorIds.GroupNumber, "Group number", "01.", (line, _) => Imports.Detectors.GroupNumber(line),
            new DetectorDefinition(DetectorKind.Regex, @"^\s*(?<value>\d{1,3})\s*\.?\s*$", Example: "01.")),
        new BuiltInDetector(DetectorIds.Dimensions, "Dimensions", "diam 17,6 x h 7", (line, _) => Imports.Detectors.Dimensions(line)),
        new BuiltInDetector(DetectorIds.HistoricalDate, "Historical date", "fine XII inizio XIII", (line, _) => Imports.Detectors.HistoricalDate(line)),
        new BuiltInDetector(DetectorIds.CalendarDate, "Calendar date", "12 06 1990", (line, context) => Imports.Detectors.CalendarDate(line, context.Lookup)),
        new BuiltInDetector(DetectorIds.Lot, "Auction lot", "lot 170", (line, _) => Imports.Detectors.Lot(line),
            new DetectorDefinition(DetectorKind.Regex, @"\b(?:lotto|lot)\b\.?\s*(?:n[.°]?\s*)?(?<value>\d+[a-zA-Z]?)\b", Example: "lot 170")),
        new BuiltInDetector(DetectorIds.Money, "Price with currency", "£ 7.865", (line, context) => Imports.Detectors.Money(line, context.Lookup)),
        new BuiltInDetector(DetectorIds.CatalogueNumber, "Catalogue number", "W.389", (line, _) => Imports.Detectors.CatalogueNumber(line),
            new DetectorDefinition(DetectorKind.Regex, @"^\s*(?<value>[A-Z]{1,3}\.\s?\d{1,5}[a-z]?)\s*$", IgnoreCase: false, Example: "W.389")),
        new BuiltInDetector(DetectorIds.LocationCode, "Location code", "MI F3", (line, context) => Imports.Detectors.LocationCode(line, context.LocationPatterns)),
    ]);

    /// <summary>This registry with more detectors (such as the user's stored ones); an identifier already taken keeps the detector it has.</summary>
    public DetectorRegistry With(IEnumerable<ILineDetector> more) =>
        new([.. Detectors, .. more.Where(d => !_byId.ContainsKey(d.Id))]);

    public IReadOnlyList<ILineDetector> Detectors { get; }

    public IReadOnlyList<string> Ids => Detectors.Select(d => d.Id).ToList();

    public bool Contains(string id) => _byId.ContainsKey(id);

    /// <summary>Runs the detector with this identifier; an unknown identifier recognises nothing.</summary>
    public Detection? Detect(string id, string line, DetectionContext? context = null) =>
        _byId.TryGetValue(id, out var detector) ? detector.Detect(line, context ?? DetectionContext.Default) : null;
}

/// <summary>A detector whose rules are written in code.</summary>
public sealed class BuiltInDetector(
    string id, string label, string example, Func<string, DetectionContext, Detection?> detect, DetectorDefinition? definition = null) : ILineDetector
{
    public DetectorDefinition? Definition => definition;

    public string Id => id;

    public string Label => label;

    public string? Example => example;

    public Detection? Detect(string line, DetectionContext context) => detect(line, context);
}
