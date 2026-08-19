using Starshot.Core.Editor;

namespace Starshot.Core.Overlay;

public enum SelectionHitKind
{
    None,
    Inside,
    North,
    South,
    East,
    West,
    NorthWest,
    NorthEast,
    SouthWest,
    SouthEast,
}

public static class SelectionInteraction
{
    public static SelectionHitKind HitTest(EditorRect selection, EditorPoint point, double handleSize)
    {
        double half = Math.Max(2, handleSize) / 2;
        if (Contains(Handle(selection.X, selection.Y, half), point)) return SelectionHitKind.NorthWest;
        if (Contains(Handle(selection.X + selection.Width, selection.Y, half), point)) return SelectionHitKind.NorthEast;
        if (Contains(Handle(selection.X, selection.Y + selection.Height, half), point)) return SelectionHitKind.SouthWest;
        if (Contains(Handle(selection.X + selection.Width, selection.Y + selection.Height, half), point)) return SelectionHitKind.SouthEast;
        if (Contains(Handle(selection.X + selection.Width / 2, selection.Y, half), point)) return SelectionHitKind.North;
        if (Contains(Handle(selection.X + selection.Width / 2, selection.Y + selection.Height, half), point)) return SelectionHitKind.South;
        if (Contains(Handle(selection.X, selection.Y + selection.Height / 2, half), point)) return SelectionHitKind.West;
        if (Contains(Handle(selection.X + selection.Width, selection.Y + selection.Height / 2, half), point)) return SelectionHitKind.East;
        if (selection.Contains(point)) return SelectionHitKind.Inside;
        return SelectionHitKind.None;
    }

    public static EditorRect Move(EditorRect selection, double dx, double dy, EditorRect bounds)
    {
        double x = Math.Clamp(selection.X + dx, bounds.X, bounds.X + bounds.Width - selection.Width);
        double y = Math.Clamp(selection.Y + dy, bounds.Y, bounds.Y + bounds.Height - selection.Height);
        return selection with { X = x, Y = y };
    }

    public static EditorRect Resize(EditorRect selection, SelectionHitKind hit, EditorPoint pointer, EditorRect bounds, double minSize)
    {
        double left = selection.X;
        double top = selection.Y;
        double right = selection.X + selection.Width;
        double bottom = selection.Y + selection.Height;
        double min = Math.Max(1, minSize);

        if (hit is SelectionHitKind.West or SelectionHitKind.NorthWest or SelectionHitKind.SouthWest)
        {
            left = Math.Clamp(pointer.X, bounds.X, right - min);
        }
        if (hit is SelectionHitKind.East or SelectionHitKind.NorthEast or SelectionHitKind.SouthEast)
        {
            right = Math.Clamp(pointer.X, left + min, bounds.X + bounds.Width);
        }
        if (hit is SelectionHitKind.North or SelectionHitKind.NorthWest or SelectionHitKind.NorthEast)
        {
            top = Math.Clamp(pointer.Y, bounds.Y, bottom - min);
        }
        if (hit is SelectionHitKind.South or SelectionHitKind.SouthWest or SelectionHitKind.SouthEast)
        {
            bottom = Math.Clamp(pointer.Y, top + min, bounds.Y + bounds.Height);
        }

        return new EditorRect(left, top, right - left, bottom - top);
    }

    private static EditorRect Handle(double cx, double cy, double half)
        => new(cx - half, cy - half, half * 2, half * 2);

    private static bool Contains(EditorRect rect, EditorPoint point) => rect.Contains(point);
}
