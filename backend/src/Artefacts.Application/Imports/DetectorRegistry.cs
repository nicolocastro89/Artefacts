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
public sealed record DetectionContext(IReadOnlyList<string> LocationPatterns)
{
    public static readonly DetectionContext Default = new(Detectors.DefaultLocationPatterns);

    public static DetectionContext For(ImportMapping mapping) => new(mapping.LocationPatterns ?? Detectors.DefaultLocationPatterns);
}

/// <summary>
/// Recognises one kind of line of a cell: "an amount", "a lot", "an item number and title". Detectors are pure, so
/// preview, review and import agree. A line rule names its detector by <see cref="Id"/>.
/// </summary>
public interface ILineDetector
{
    /// <summary>The stable identifier stored in mappings.</summary>
    string Id { get; }

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
        new BuiltInDetector(DetectorIds.ItemTitle, (line, _) => Imports.Detectors.ItemTitle(line)),
        new BuiltInDetector(DetectorIds.GroupNumber, (line, _) => Imports.Detectors.GroupNumber(line)),
        new BuiltInDetector(DetectorIds.Dimensions, (line, _) => Imports.Detectors.Dimensions(line)),
        new BuiltInDetector(DetectorIds.HistoricalDate, (line, _) => Imports.Detectors.HistoricalDate(line)),
        new BuiltInDetector(DetectorIds.CalendarDate, (line, _) => Imports.Detectors.CalendarDate(line)),
        new BuiltInDetector(DetectorIds.Lot, (line, _) => Imports.Detectors.Lot(line)),
        new BuiltInDetector(DetectorIds.Money, (line, _) => Imports.Detectors.Money(line)),
        new BuiltInDetector(DetectorIds.CatalogueNumber, (line, _) => Imports.Detectors.CatalogueNumber(line)),
        new BuiltInDetector(DetectorIds.LocationCode, (line, context) => Imports.Detectors.LocationCode(line, context.LocationPatterns)),
    ]);

    public IReadOnlyList<ILineDetector> Detectors { get; }

    public IReadOnlyList<string> Ids => Detectors.Select(d => d.Id).ToList();

    public bool Contains(string id) => _byId.ContainsKey(id);

    /// <summary>Runs the detector with this identifier; an unknown identifier recognises nothing.</summary>
    public Detection? Detect(string id, string line, DetectionContext? context = null) =>
        _byId.TryGetValue(id, out var detector) ? detector.Detect(line, context ?? DetectionContext.Default) : null;
}

/// <summary>A detector whose rules are written in code.</summary>
public sealed class BuiltInDetector(string id, Func<string, DetectionContext, Detection?> detect) : ILineDetector
{
    public string Id => id;

    public Detection? Detect(string line, DetectionContext context) => detect(line, context);
}
