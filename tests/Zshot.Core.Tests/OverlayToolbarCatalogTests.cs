using Zshot.Core.Overlay;
using Xunit;

namespace Zshot.Core.Tests;

public class OverlayToolbarCatalogTests
{
    [Fact]
    public void Default_hidden_are_ellipse_text_number_redo()
    {
        var hidden = OverlayToolbarCatalog.ParseHidden(null);

        Assert.False(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Ellipse, hidden));
        Assert.False(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Text, hidden));
        Assert.False(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Number, hidden));
        Assert.False(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Redo, hidden));

        Assert.True(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Color, hidden));
        Assert.True(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Select, hidden));
        Assert.True(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Rect, hidden));
        Assert.True(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Undo, hidden));
        Assert.True(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Save, hidden));
    }

    [Fact]
    public void Copy_is_never_visible()
    {
        Assert.False(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Copy, OverlayToolbarCatalog.ParseHidden(null)));
        Assert.False(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Copy, OverlayToolbarCatalog.ParseHidden("")));
    }

    [Fact]
    public void Complete_and_cancel_are_always_visible()
    {
        var hidden = OverlayToolbarCatalog.ParseHidden("complete,cancel,save");
        Assert.True(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Complete, hidden));
        Assert.True(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Cancel, hidden));
        Assert.False(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Save, hidden));
    }

    [Fact]
    public void Empty_string_shows_all_customizable_items()
    {
        var hidden = OverlayToolbarCatalog.ParseHidden("");
        foreach (var id in OverlayToolbarCatalog.Customizable)
        {
            Assert.True(OverlayToolbarCatalog.IsVisible(id, hidden));
        }
    }

    [Fact]
    public void Unknown_ids_are_ignored()
    {
        var hidden = OverlayToolbarCatalog.ParseHidden("nope,ellipse");
        Assert.DoesNotContain("nope", hidden);
        Assert.False(OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Ellipse, hidden));
    }

    [Fact]
    public void Serialize_keeps_catalog_order()
    {
        var stored = OverlayToolbarCatalog.SerializeHidden(["redo", "ellipse", "text", "number"]);
        Assert.Equal("ellipse,text,number,redo", stored);
    }

    [Fact]
    public void Default_hidden_value_matches_serialize()
    {
        Assert.Equal(
            OverlayToolbarCatalog.SerializeHidden(OverlayToolbarCatalog.DefaultHidden),
            OverlayToolbarCatalog.DefaultHiddenValue);
    }

    [Fact]
    public void Copy_is_not_customizable()
    {
        Assert.DoesNotContain(OverlayToolbarCatalog.Copy, OverlayToolbarCatalog.Customizable);
        Assert.DoesNotContain(OverlayToolbarCatalog.Complete, OverlayToolbarCatalog.Customizable);
        Assert.DoesNotContain(OverlayToolbarCatalog.Cancel, OverlayToolbarCatalog.Customizable);
    }
}
