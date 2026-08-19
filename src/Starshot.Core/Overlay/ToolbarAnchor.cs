using Starshot.Core.Editor;

namespace Starshot.Core.Overlay;

public static class ToolbarAnchor
{
    public static EditorPoint Place(EditorRect selection, double toolbarWidth, double toolbarHeight, EditorRect monitor, double gap)
    {
        double x = selection.X + selection.Width / 2 - toolbarWidth / 2;
        double below = selection.Y + selection.Height + gap;
        double above = selection.Y - gap - toolbarHeight;
        double y = below + toolbarHeight <= monitor.Y + monitor.Height ? below : above;
        x = Math.Clamp(x, monitor.X, monitor.X + monitor.Width - toolbarWidth);
        y = Math.Clamp(y, monitor.Y, monitor.Y + monitor.Height - toolbarHeight);
        return new EditorPoint(x, y);
    }
}
