using Artefacts.Domain.Common;
using Artefacts.Domain.Dating;

namespace Artefacts.Domain.Catalogue;

/// <summary>
/// One step in an artefact's provenance: a region (from the reference list) over a historical period.
/// The period follows the same rules as any <see cref="HistoricalDate"/> (it is a year range with a precision).
/// </summary>
public sealed class ProvenanceEntry
{
    public const int MaxNoteLength = 2000;

    public RegionId RegionId { get; private set; }
    public HistoricalDate Period { get; private set; } = default!;
    public string? Note { get; private set; }
    public int Position { get; private set; }

    // For EF Core.
    private ProvenanceEntry()
    {
    }

    public static ProvenanceEntry Create(RegionId regionId, HistoricalDate period, string? note) =>
        new()
        {
            RegionId = regionId,
            Period = period ?? throw new DomainValidationException("period", "Is required."),
            Note = Guard.Optional(note, "note", MaxNoteLength),
        };

    internal ProvenanceEntry AtPosition(int position) =>
        new() { RegionId = RegionId, Period = Period, Note = Note, Position = position };
}
