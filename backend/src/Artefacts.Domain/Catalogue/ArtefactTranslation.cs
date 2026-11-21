namespace Artefacts.Domain.Catalogue;

/// <summary>
/// An artefact's translatable texts in one language. Only the description for now; later translatable fields become
/// further properties here, so one row holds everything an artefact has in that language.
/// </summary>
public sealed class ArtefactTranslation
{
    public string Language { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? CatalogueEntry { get; private set; }

    // For EF Core.
    private ArtefactTranslation()
    {
    }

    internal static ArtefactTranslation Create(string language, string? description, string? catalogueEntry = null) =>
        new() { Language = language, Description = description, CatalogueEntry = catalogueEntry };
}
