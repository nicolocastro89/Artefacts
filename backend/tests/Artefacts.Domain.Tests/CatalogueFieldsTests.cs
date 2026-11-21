using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Common;
using Artefacts.Domain.Finance;

namespace Artefacts.Domain.Tests;

public class CatalogueFieldsTests
{
    private static Artefact NewArtefact() => Artefact.Create("CER-01-02", "Piatto Celadon");

    [Fact]
    public void Acquisition_keeps_lot_and_note_trimmed()
    {
        var acquisition = Acquisition.Create(AcquisitionMethod.Purchase, new DateOnly(1990, 6, 12), PartyId.New(),
            Money.Create(350_000_000m, "ITL"), lot: " 170 ", note: " acquistato fuori asta ");
        Assert.Equal("170", acquisition.Lot);
        Assert.Equal("acquistato fuori asta", acquisition.Note);
        Assert.Equal("ITL", acquisition.Price!.Currency);
    }

    [Fact]
    public void Acquisition_blank_lot_and_note_become_null()
    {
        var acquisition = Acquisition.Create(AcquisitionMethod.Gift, null, null, null, lot: "  ", note: "");
        Assert.Null(acquisition.Lot);
        Assert.Null(acquisition.Note);
    }

    [Fact]
    public void Place_of_production_keeps_uncertain_text_as_written()
    {
        var place = PlaceOfProduction.Create(" Anatolia o Asia Centrale? ", null);
        Assert.Equal("Anatolia o Asia Centrale?", place!.Text);
        Assert.Null(place.RegionId);
    }

    [Fact]
    public void Place_of_production_without_text_or_region_is_null()
    {
        Assert.Null(PlaceOfProduction.Create("  ", null));
    }

    [Fact]
    public void Place_of_production_can_link_a_region_without_text()
    {
        var region = RegionId.New();
        var place = PlaceOfProduction.Create(null, region);
        Assert.Equal(region, place!.RegionId);
        Assert.Null(place.Text);
    }

    [Fact]
    public void Classification_origin_and_holding_link_their_records()
    {
        var a = NewArtefact();
        var type = ObjectTypeId.New();
        ReferenceEntryId group = ReferenceEntryId.New(), dynasty = ReferenceEntryId.New(), location = ReferenceEntryId.New();
        var holder = PartyId.New();
        a.Classify(type, group);
        a.SetOrigin(PlaceOfProduction.Create("Cina", null), dynasty);
        a.SetHolding(holder, location, Money.Create(200_000m, "GBP"));
        a.SetCatalogueEntry("A JUNYAO DISH\nJIN DYNASTY (1115-1234)\n");

        Assert.Equal(type, a.ObjectTypeId);
        Assert.Equal(group, a.CollectionGroupId);
        Assert.Equal(dynasty, a.DynastyId);
        Assert.Equal(holder, a.HolderId);
        Assert.Equal(location, a.StorageLocationId);
        Assert.Equal(200_000m, a.Valuation!.Amount);
        Assert.Equal("A JUNYAO DISH\nJIN DYNASTY (1115-1234)", a.CatalogueEntryEn);
    }

    [Fact]
    public void Exhibitions_keep_order_and_drop_blank_entries()
    {
        var a = NewArtefact();
        a.ReplaceExhibitions(["Arte Ottomana 1450-1600, Genova, 1999", "  ", "Exhibition of Persian Art, New York, 1940"]);

        Assert.Equal(["Arte Ottomana 1450-1600, Genova, 1999", "Exhibition of Persian Art, New York, 1940"],
            a.Exhibitions.Select(e => e.Text));
        Assert.Equal([0, 1], a.Exhibitions.Select(e => e.Position));
    }

    [Fact]
    public void Replacing_exhibitions_removes_the_old_ones()
    {
        var a = NewArtefact();
        a.ReplaceExhibitions(["First"]);
        a.ReplaceExhibitions(["Second"]);
        Assert.Equal(["Second"], a.Exhibitions.Select(e => e.Text));
    }

    [Fact]
    public void Overlong_exhibition_is_rejected_with_its_index()
    {
        var a = NewArtefact();
        var ex = Assert.Throws<DomainValidationException>(() => a.ReplaceExhibitions(["ok", new string('x', Exhibition.MaxTextLength + 1)]));
        Assert.Contains("exhibitions[1]", ex.Errors.Keys);
    }
}
