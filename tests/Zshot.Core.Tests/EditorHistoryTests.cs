using Zshot.Core.Editor;
using Xunit;

namespace Zshot.Core.Tests;

public class EditorHistoryTests
{
    [Fact]
    public void Undo_add_removes_element()
    {
        var doc = new EditorDocument();
        var history = new EditorHistory();
        var rect = new RectangleElement { Bounds = new EditorRect(10, 10, 40, 20) };

        history.Execute(doc, new AddElementCommand(rect));
        Assert.Single(doc.Elements);
        Assert.True(history.Undo(doc));
        Assert.Empty(doc.Elements);
    }

    [Fact]
    public void Redo_add_restores_element()
    {
        var doc = new EditorDocument();
        var history = new EditorHistory();
        var rect = new RectangleElement { Bounds = new EditorRect(0, 0, 10, 10) };

        history.Execute(doc, new AddElementCommand(rect));
        history.Undo(doc);
        Assert.True(history.Redo(doc));
        Assert.Same(rect, Assert.Single(doc.Elements));
    }

    [Fact]
    public void Undo_move_restores_bounds()
    {
        var doc = new EditorDocument();
        var history = new EditorHistory();
        var rect = new RectangleElement { Bounds = new EditorRect(5, 6, 10, 10) };
        history.Execute(doc, new AddElementCommand(rect));
        history.Execute(doc, new MoveElementCommand(rect, 8, -3));

        Assert.Equal(13, rect.Bounds.X);
        Assert.Equal(3, rect.Bounds.Y);
        history.Undo(doc);
        Assert.Equal(5, rect.Bounds.X);
        Assert.Equal(6, rect.Bounds.Y);
    }

    [Fact]
    public void Rectangle_hit_test_uses_bounds()
    {
        var rect = new RectangleElement { Bounds = new EditorRect(10, 10, 20, 20) };
        Assert.True(rect.HitTest(new EditorPoint(15, 15)));
        Assert.False(rect.HitTest(new EditorPoint(0, 0)));
    }

    [Fact]
    public void Ellipse_hit_test_rejects_corner_of_bounding_box()
    {
        var ellipse = new EllipseElement { Bounds = new EditorRect(0, 0, 100, 100) };
        Assert.True(ellipse.HitTest(new EditorPoint(50, 50)));
        Assert.False(ellipse.HitTest(new EditorPoint(1, 1)));
    }

    [Fact]
    public void History_caps_depth()
    {
        var doc = new EditorDocument();
        var history = new EditorHistory(maxDepth: 2);
        history.Execute(doc, new AddElementCommand(new RectangleElement { Bounds = new EditorRect(0, 0, 1, 1) }));
        history.Execute(doc, new AddElementCommand(new RectangleElement { Bounds = new EditorRect(1, 1, 1, 1) }));
        history.Execute(doc, new AddElementCommand(new RectangleElement { Bounds = new EditorRect(2, 2, 1, 1) }));

        Assert.Equal(3, doc.Elements.Count);
        Assert.True(history.Undo(doc));
        Assert.True(history.Undo(doc));
        Assert.False(history.Undo(doc));
        Assert.Single(doc.Elements);
    }
}
