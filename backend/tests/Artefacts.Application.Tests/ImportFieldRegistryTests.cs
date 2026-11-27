using Artefacts.Application.Imports;

namespace Artefacts.Application.Tests;

public class ImportFieldRegistryTests
{
    [Fact]
    public void Every_field_has_one_definition()
    {
        var defined = FieldRegistry.BuiltIn.All.Select(f => f.Id).ToList();

        Assert.Equal(Enum.GetValues<ImportField>().Order(), defined.Order());
        Assert.Equal(defined.Count, defined.Distinct().Count());
    }

    /// <summary>The identifiers mappings and templates stored: renaming one orphans them.</summary>
    [Fact]
    public void Fields_keep_their_stored_identifiers() =>
        Assert.Equal(
            ["title", "itemNumber", "groupNumber", "catalogueNumber", "inventoryNumber", "dimensions", "medium", "maker", "description",
                "placeOfProduction", "creationDate", "dynasty", "acquisitionSource", "acquisitionDate", "acquisitionLot", "acquisitionPrice",
                "acquisitionNote", "exhibitions", "catalogueEntry", "holder", "storageLocation", "valuation", "collectionGroup"],
            FieldRegistry.BuiltIn.Ordered.Select(f => f.Key));

    [Fact]
    public void Single_valued_fields_are_the_ones_a_row_holds_once() =>
        Assert.Equal(
            [ImportField.ItemNumber, ImportField.GroupNumber, ImportField.CatalogueNumber, ImportField.InventoryNumber, ImportField.CreationDate,
                ImportField.AcquisitionDate, ImportField.AcquisitionLot, ImportField.AcquisitionPrice, ImportField.Valuation],
            FieldRegistry.BuiltIn.Ordered.Where(f => f.Cardinality == FieldCardinality.Single).Select(f => f.Id));

    [Fact]
    public void Long_text_fields_join_with_line_breaks_and_dimensions_with_semicolons()
    {
        var registry = FieldRegistry.BuiltIn;

        Assert.All([ImportField.Description, ImportField.AcquisitionNote, ImportField.CatalogueEntry],
            f => Assert.Equal("\n", registry.Get(f).Separator));
        Assert.Equal("; ", registry.Get(ImportField.Dimensions).Separator);
        Assert.Equal(" ", registry.Get(ImportField.Title).Separator);
    }

    [Fact]
    public void Curated_fields_name_their_list() =>
        Assert.Equal(
            [(ImportField.Dynasty, ImportList.Dynasty), (ImportField.AcquisitionSource, ImportList.Party), (ImportField.Holder, ImportList.Party),
                (ImportField.StorageLocation, ImportList.StorageLocation), (ImportField.CollectionGroup, ImportList.CollectionGroup)],
            FieldRegistry.BuiltIn.Ordered.Where(f => f.List is not null).Select(f => (f.Id, f.List!.List)));
}
