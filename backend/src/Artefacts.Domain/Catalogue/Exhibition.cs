using Artefacts.Domain.Common;

namespace Artefacts.Domain.Catalogue;

/// <summary>One exhibition an artefact was shown in, as free text (venue, title, year as the curator wrote them).</summary>
public sealed class Exhibition
{
    public const int MaxTextLength = 1000;

    public string Text { get; private set; } = default!;
    public int Position { get; private set; }

    // For EF Core.
    private Exhibition()
    {
    }

    /// <summary>Returns null for blank entries, which are dropped.</summary>
    internal static Exhibition? Create(string? text, int position, int index)
    {
        var clean = Guard.Optional(text, $"exhibitions[{index}]", MaxTextLength);
        return clean is null ? null : new Exhibition { Text = clean, Position = position };
    }
}
