using Starshot.Core.Editor;
using Starshot.Core.Overlay;
using Xunit;

namespace Starshot.Core.Tests;

public class OverlayGeometryTests
{
    private static readonly EditorRect Selection = new(100, 80, 200, 120);

    [Fact]
    public void HitTest_inside_is_move()
    {
        var hit = SelectionInteraction.HitTest(Selection, new EditorPoint(150, 140), handleSize: 8);
        Assert.Equal(SelectionHitKind.Inside, hit);
    }

    [Fact]
    public void HitTest_outside_is_none()
    {
        var hit = SelectionInteraction.HitTest(Selection, new EditorPoint(20, 20), handleSize: 8);
        Assert.Equal(SelectionHitKind.None, hit);
    }

    [Fact]
    public void HitTest_corners_win_over_edges()
    {
        Assert.Equal(SelectionHitKind.NorthWest, SelectionInteraction.HitTest(Selection, new EditorPoint(100, 80), 8));
        Assert.Equal(SelectionHitKind.NorthEast, SelectionInteraction.HitTest(Selection, new EditorPoint(300, 80), 8));
        Assert.Equal(SelectionHitKind.SouthWest, SelectionInteraction.HitTest(Selection, new EditorPoint(100, 200), 8));
        Assert.Equal(SelectionHitKind.SouthEast, SelectionInteraction.HitTest(Selection, new EditorPoint(300, 200), 8));
    }

    [Fact]
    public void HitTest_edge_midpoints()
    {
        Assert.Equal(SelectionHitKind.North, SelectionInteraction.HitTest(Selection, new EditorPoint(200, 80), 8));
        Assert.Equal(SelectionHitKind.South, SelectionInteraction.HitTest(Selection, new EditorPoint(200, 200), 8));
        Assert.Equal(SelectionHitKind.West, SelectionInteraction.HitTest(Selection, new EditorPoint(100, 140), 8));
        Assert.Equal(SelectionHitKind.East, SelectionInteraction.HitTest(Selection, new EditorPoint(300, 140), 8));
    }

    [Fact]
    public void Move_clamps_to_bounds()
    {
        var moved = SelectionInteraction.Move(Selection, dx: -500, dy: 10, bounds: new EditorRect(0, 0, 400, 300));
        Assert.Equal(0, moved.X);
        Assert.Equal(90, moved.Y);
        Assert.Equal(200, moved.Width);
        Assert.Equal(120, moved.Height);
    }

    [Fact]
    public void Resize_southeast_grows_from_fixed_origin()
    {
        var resized = SelectionInteraction.Resize(
            Selection,
            SelectionHitKind.SouthEast,
            new EditorPoint(340, 230),
            bounds: new EditorRect(0, 0, 800, 600),
            minSize: 5);
        Assert.Equal(100, resized.X);
        Assert.Equal(80, resized.Y);
        Assert.Equal(240, resized.Width);
        Assert.Equal(150, resized.Height);
    }

    [Fact]
    public void Resize_northwest_keeps_bottom_right()
    {
        var resized = SelectionInteraction.Resize(
            Selection,
            SelectionHitKind.NorthWest,
            new EditorPoint(80, 60),
            bounds: new EditorRect(0, 0, 800, 600),
            minSize: 5);
        Assert.Equal(80, resized.X);
        Assert.Equal(60, resized.Y);
        Assert.Equal(220, resized.Width);
        Assert.Equal(140, resized.Height);
    }

    [Fact]
    public void Toolbar_prefers_below_selection()
    {
        var pos = ToolbarAnchor.Place(
            selection: new EditorRect(100, 80, 200, 120),
            toolbarWidth: 200,
            toolbarHeight: 40,
            monitor: new EditorRect(0, 0, 1920, 1080),
            gap: 8);
        Assert.Equal(100, pos.X);
        Assert.Equal(208, pos.Y);
    }

    [Fact]
    public void Toolbar_flips_above_when_below_does_not_fit()
    {
        var pos = ToolbarAnchor.Place(
            selection: new EditorRect(100, 1000, 200, 60),
            toolbarWidth: 400,
            toolbarHeight: 40,
            monitor: new EditorRect(0, 0, 1920, 1080),
            gap: 8);
        Assert.Equal(1000 - 8 - 40, pos.Y);
    }

    [Fact]
    public void Toolbar_clamps_into_monitor()
    {
        var pos = ToolbarAnchor.Place(
            selection: new EditorRect(10, 10, 40, 20),
            toolbarWidth: 400,
            toolbarHeight: 40,
            monitor: new EditorRect(0, 0, 500, 80),
            gap: 8);
        Assert.True(pos.X >= 0);
        Assert.True(pos.X + 400 <= 500);
        Assert.True(pos.Y >= 0);
        Assert.True(pos.Y + 40 <= 80);
    }
}
