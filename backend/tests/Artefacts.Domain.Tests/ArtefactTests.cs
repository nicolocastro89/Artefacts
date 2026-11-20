using Artefacts.Domain.Catalogue;
using Artefacts.Domain.Common;
using Artefacts.Domain.Dating;

namespace Artefacts.Domain.Tests;

public class ArtefactTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Creates_with_inventory_number_and_title()
    {
        var a = Artefact.Create(" INV-2026-001 ", "Still Life with Lemons");
        Assert.Equal("INV-2026-001", a.InventoryNumber);
        Assert.Equal("Still Life with Lemons", a.Title);
    }

    [Fact]
    public void Reports_all_missing_required_fields()
    {
        var ex = Assert.Throws<DomainValidationException>(() => Artefact.Create("", null));
        Assert.Contains("inventoryNumber", ex.Errors.Keys);
        Assert.Contains("title", ex.Errors.Keys);
    }

    [Fact]
    public void Provenance_keeps_order()
    {
        var a = Artefact.Create("INV-1", "T");
        var flanders = RegionId.New();
        var paris = RegionId.New();
        a.ReplaceProvenance([
            ProvenanceEntry.Create(flanders, HistoricalDate.Create("c. 1650"), null),
            ProvenanceEntry.Create(paris, HistoricalDate.Create("1780s"), "Sold at auction"),
        ]);

        Assert.Collection(a.Provenance,
            p => Assert.Equal(flanders, p.RegionId),
            p => { Assert.Equal(paris, p.RegionId); Assert.Equal(1, p.Position); });
    }

    [Fact]
    public void First_image_becomes_primary()
    {
        var a = Artefact.Create("INV-1", "T");
        var first = MediaAssetId.New();
        a.AttachImage(first, T0);
        a.AttachImage(MediaAssetId.New(), T0.AddMinutes(1));

        Assert.Equal(first, a.PrimaryImage!.MediaAssetId);
        Assert.Single(a.Images, i => i.IsPrimary);
    }

    [Fact]
    public void Make_primary_switches_primary()
    {
        var a = Artefact.Create("INV-1", "T");
        var first = MediaAssetId.New();
        var second = MediaAssetId.New();
        a.AttachImage(first, T0);
        a.AttachImage(second, T0.AddMinutes(1));

        a.MakePrimary(second);

        Assert.Equal(second, a.PrimaryImage!.MediaAssetId);
        Assert.Single(a.Images, i => i.IsPrimary);
    }

    [Fact]
    public void Deleting_primary_promotes_earliest_remaining()
    {
        var a = Artefact.Create("INV-1", "T");
        var first = MediaAssetId.New();
        var second = MediaAssetId.New();
        var third = MediaAssetId.New();
        a.AttachImage(first, T0);
        a.AttachImage(second, T0.AddMinutes(1));
        a.AttachImage(third, T0.AddMinutes(2));
        a.MakePrimary(third);

        a.RemoveImage(third);

        Assert.Equal(first, a.PrimaryImage!.MediaAssetId);
    }

    [Fact]
    public void Deleting_last_image_leaves_no_primary()
    {
        var a = Artefact.Create("INV-1", "T");
        var only = MediaAssetId.New();
        a.AttachImage(only, T0);
        a.RemoveImage(only);
        Assert.Null(a.PrimaryImage);
        Assert.Empty(a.Images);
    }

    [Fact]
    public void Unknown_image_is_not_found()
    {
        var a = Artefact.Create("INV-1", "T");
        Assert.Throws<NotFoundException>(() => a.MakePrimary(MediaAssetId.New()));
    }

    [Fact]
    public void Caption_is_trimmed_and_optional()
    {
        var a = Artefact.Create("INV-1", "T");
        var id = MediaAssetId.New();
        a.AttachImage(id, T0);
        a.SetImageCaption(id, "  Recto, after 2021 conservation ");
        Assert.Equal("Recto, after 2021 conservation", a.Images[0].Caption);
        a.SetImageCaption(id, " ");
        Assert.Null(a.Images[0].Caption);
    }
}
