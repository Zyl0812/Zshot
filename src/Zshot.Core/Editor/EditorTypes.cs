namespace Zshot.Core.Editor;

public readonly record struct EditorPoint(double X, double Y);

public readonly record struct EditorRect(double X, double Y, double Width, double Height)
{
    public bool Contains(EditorPoint point)
        => point.X >= X && point.Y >= Y && point.X <= X + Width && point.Y <= Y + Height;

    public EditorRect Offset(double dx, double dy) => this with { X = X + dx, Y = Y + dy };
}
