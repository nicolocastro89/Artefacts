using System.Text.Json;
using Artefacts.Application.Common;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Dating;

namespace Artefacts.Application.Imports;

/// <summary>
/// One decision about a text the import cannot settle by rule: what to do (<see cref="Action"/>) with every row that
/// has the text, and the details of that (<see cref="Payload"/>, whose shape belongs to the kind of resolution).
/// </summary>
public sealed record ImportResolution(string Text, string Action, JsonElement? Payload = null);

/// <summary>A distinct text a kind of resolution still concerns, with the rows using it and the decision in force.</summary>
internal sealed record PendingResolution(string Text, IReadOnlyList<int> SheetRows, ImportResolution? Resolution);

/// <summary>
/// A kind of text an import asks the user to settle (a date the rules do not understand, dimensions that could not be
/// read clearly). Each kind finds its pending texts in an evaluation, validates the decisions it is sent, and stores
/// them in the job's <see cref="ImportDecisions"/>, whose stored shape does not change.
/// </summary>
internal interface IResolutionKind
{
    string Id { get; }

    string Label { get; }

    /// <summary>Whether finding the pending texts needs the rows checked against the catalogue as well.</summary>
    bool ValidatesRows { get; }

    IReadOnlyList<PendingResolution> Pending(ImportHandler.Evaluation evaluation, ImportDecisions decisions);

    /// <summary>Reports every problem with the decisions, by <c>resolutions[i]...</c>.</summary>
    void Validate(IReadOnlyList<ImportResolution> resolutions, IReadOnlySet<Guid> unitIds, ValidationCollector v);

    /// <summary>The decisions with these laid over them: per text the new decision wins, and decisions for other texts are kept.</summary>
    ImportDecisions Apply(ImportDecisions decisions, IReadOnlyList<ImportResolution> resolutions);
}

/// <summary>The identifiers of the kinds of resolution, as used in the API.</summary>
public static class ResolutionKindIds
{
    public const string Dates = "dates";
    public const string Dimensions = "dimensions";
}

/// <summary>The kinds of resolution, by identifier.</summary>
internal static class ResolutionKinds
{

    public static IReadOnlyList<IResolutionKind> All { get; } = [new DateResolutionKind(), new DimensionResolutionKind()];

    public static IResolutionKind? Find(string id) => All.FirstOrDefault(k => k.Id == id);

    internal static T? Read<T>(ImportResolution resolution) where T : class =>
        resolution.Payload is { ValueKind: JsonValueKind.Object } p ? p.Deserialize<T>(ImportJson.Options) : null;

    internal static JsonElement ToPayload<T>(T value) => JsonSerializer.SerializeToElement(value, ImportJson.Options);

    private static string JsonName(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    internal static string FieldName(MeasureKind kind) => JsonName(kind.ToString());
}

/// <summary>A year range for a date text the historical date rules cannot read. Action <c>range</c>, payload <see cref="DateRangePayload"/>.</summary>
internal sealed class DateResolutionKind : IResolutionKind
{
    public const string Range = "range";

    public string Id => ResolutionKindIds.Dates;

    public string Label => "Dates";

    public bool ValidatesRows => true;

    public IReadOnlyList<PendingResolution> Pending(ImportHandler.Evaluation evaluation, ImportDecisions decisions)
    {
        var saved = decisions.Dates.ToDictionary(d => RowEvaluator.DateKey(d.Text));
        return evaluation.Rows
            .Where(r => r.DateText is not null)
            .Where(r => r.DateUnresolved || r.Mapped.First(ImportField.CreationDate)?.Parsed is ParsedDateText { Interpretation: null })
            .GroupBy(r => RowEvaluator.DateKey(r.DateText!))
            .Select(g => new PendingResolution(g.First().DateText!, g.Select(r => r.Row.SheetRow).ToList(), saved.TryGetValue(g.Key, out var d) ? ToResolution(d) : null))
            .ToList();
    }

    public void Validate(IReadOnlyList<ImportResolution> resolutions, IReadOnlySet<Guid> unitIds, ValidationCollector v)
    {
        for (var i = 0; i < resolutions.Count; i++)
        {
            var key = $"resolutions[{i}]";
            if (string.IsNullOrWhiteSpace(resolutions[i].Text)) v.Add($"{key}.text", "The date text is required.");
            if (resolutions[i].Action != Range || ResolutionKinds.Read<DateRangePayload>(resolutions[i]) is not { EarliestYear: { } earliest, LatestYear: { } latest })
            {
                v.Add(key, "Give the earliest and latest year.");
                continue;
            }

            if (earliest is < HistoricalDateParser.MinYear or > HistoricalDateParser.MaxYear)
                v.Add($"{key}.payload.earliestYear", $"Years must be between {HistoricalDateParser.MinYear} and {HistoricalDateParser.MaxYear}.");
            if (latest is < HistoricalDateParser.MinYear or > HistoricalDateParser.MaxYear)
                v.Add($"{key}.payload.latestYear", $"Years must be between {HistoricalDateParser.MinYear} and {HistoricalDateParser.MaxYear}.");
            if (earliest > latest) v.Add($"{key}.payload.latestYear", "The latest year must not be before the earliest year.");
        }
    }

    public ImportDecisions Apply(ImportDecisions decisions, IReadOnlyList<ImportResolution> resolutions)
    {
        var byKey = decisions.Dates.ToDictionary(d => RowEvaluator.DateKey(d.Text));
        foreach (var r in resolutions)
        {
            var range = ResolutionKinds.Read<DateRangePayload>(r)!;
            byKey[RowEvaluator.DateKey(r.Text)] = new DateResolution(r.Text.Trim(), range.EarliestYear!.Value, range.LatestYear!.Value);
        }

        return decisions with { Dates = byKey.Values.ToList() };
    }

    private static ImportResolution ToResolution(DateResolution d) =>
        new(d.Text, Range, ResolutionKinds.ToPayload(new DateRangePayload(d.EarliestYear, d.LatestYear)));
}

public sealed record DateRangePayload(int? EarliestYear, int? LatestYear);

/// <summary>
/// Dimensions whose measurements could not be read clearly. Action <c>measure</c> with a <see cref="DimensionsPayload"/>
/// of the measurements every row with the text gets, or <c>keepText</c> to import the text without measurements.
/// </summary>
internal sealed class DimensionResolutionKind : IResolutionKind
{
    public const string Measure = "measure";
    public const string KeepText = "keepText";

    public string Id => ResolutionKindIds.Dimensions;

    public string Label => "Dimensions";

    public bool ValidatesRows => false;

    public IReadOnlyList<PendingResolution> Pending(ImportHandler.Evaluation evaluation, ImportDecisions decisions)
    {
        var saved = (decisions.Dimensions ?? []).ToDictionary(d => RowEvaluator.DateKey(d.Text));
        return evaluation.Rows
            .Where(r => r.DimensionsUnclear && r.DimensionsText is not null)
            .GroupBy(r => RowEvaluator.DateKey(r.DimensionsText!))
            .Select(g => new PendingResolution(g.First().DimensionsText!, g.Select(r => r.Row.SheetRow).ToList(),
                g.First().DimensionsResolution is { } inRow ? ToResolution(inRow) : saved.TryGetValue(g.Key, out var d) ? ToResolution(d) : null))
            .ToList();
    }

    public void Validate(IReadOnlyList<ImportResolution> resolutions, IReadOnlySet<Guid> unitIds, ValidationCollector v)
    {
        for (var i = 0; i < resolutions.Count; i++)
        {
            var key = $"resolutions[{i}]";
            if (string.IsNullOrWhiteSpace(resolutions[i].Text)) v.Add($"{key}.text", "The dimensions text is required.");
            if (resolutions[i].Action == KeepText) continue;
            if (resolutions[i].Action != Measure)
            {
                v.Add($"{key}.action", $"Must be {Measure} or {KeepText}.");
                continue;
            }

            var measurements = ResolutionKinds.Read<DimensionsPayload>(resolutions[i])?.ToResolution("x").Measurements.ToList() ?? [];
            if (measurements.Count == 0) v.Add(key, "Enter at least one measurement, or keep the text only.");
            foreach (var (kind, input) in measurements)
            {
                var field = $"{key}.payload.{ResolutionKinds.FieldName(kind)}";
                v.Try(field, () => Domain.Measures.Measurement.Create(input.Value, input.UnitId));
                if (input.UnitId is { } unitId && !unitIds.Contains(unitId)) v.Add($"{field}.unitId", "The unit does not exist.");
            }
        }
    }

    public ImportDecisions Apply(ImportDecisions decisions, IReadOnlyList<ImportResolution> resolutions)
    {
        var byKey = (decisions.Dimensions ?? []).ToDictionary(d => RowEvaluator.DateKey(d.Text));
        foreach (var r in resolutions)
            byKey[RowEvaluator.DateKey(r.Text)] = r.Action == KeepText
                ? new DimensionResolution(r.Text.Trim(), KeepTextOnly: true)
                : ResolutionKinds.Read<DimensionsPayload>(r)!.ToResolution(r.Text.Trim());
        return decisions with { Dimensions = byKey.Values.ToList() };
    }

    private static ImportResolution ToResolution(DimensionResolution d) =>
        d.KeepTextOnly
            ? new ImportResolution(d.Text, KeepText)
            : new ImportResolution(d.Text, Measure, ResolutionKinds.ToPayload(new DimensionsPayload(d.Width, d.Height, d.Length, d.Diameter)));
}

public sealed record DimensionsPayload(MeasurementInput? Width = null, MeasurementInput? Height = null, MeasurementInput? Length = null, MeasurementInput? Diameter = null)
{
    internal DimensionResolution ToResolution(string text) => new(text, false, Width, Height, Length, Diameter);
}
