namespace Zshot.Core.Editor;

public abstract class EditorElement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public EditorRect Bounds { get; set; }
    public bool IsSelected { get; set; }
    public string StrokeColor { get; set; } = "#FFFF3B30";
    public string? FillColor { get; set; }
    public double StrokeWidth { get; set; } = 3;
    public int ZIndex { get; set; }

    public abstract bool HitTest(EditorPoint point);

    public virtual void MoveBy(double dx, double dy)
    {
        Bounds = Bounds.Offset(dx, dy);
    }
}

public sealed class RectangleElement : EditorElement
{
    public override bool HitTest(EditorPoint point) => Bounds.Contains(point);
}

public sealed class EllipseElement : EditorElement
{
    public override bool HitTest(EditorPoint point)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return false;
        }

        double cx = Bounds.X + Bounds.Width / 2;
        double cy = Bounds.Y + Bounds.Height / 2;
        double nx = (point.X - cx) / (Bounds.Width / 2);
        double ny = (point.Y - cy) / (Bounds.Height / 2);
        return nx * nx + ny * ny <= 1;
    }
}

public class LineElement : EditorElement
{
    public EditorPoint Start { get; set; }
    public EditorPoint End { get; set; }

    public override bool HitTest(EditorPoint point)
        => DistanceToSegment(point, Start, End) <= Math.Max(StrokeWidth, 4);

    public override void MoveBy(double dx, double dy)
    {
        Start = new EditorPoint(Start.X + dx, Start.Y + dy);
        End = new EditorPoint(End.X + dx, End.Y + dy);
        Bounds = Bounds.Offset(dx, dy);
    }

    internal static double DistanceToSegment(EditorPoint p, EditorPoint a, EditorPoint b)
    {
        double vx = b.X - a.X;
        double vy = b.Y - a.Y;
        double len2 = vx * vx + vy * vy;
        if (len2 <= double.Epsilon)
        {
            double dx = p.X - a.X;
            double dy = p.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        double t = ((p.X - a.X) * vx + (p.Y - a.Y) * vy) / len2;
        t = Math.Clamp(t, 0, 1);
        double qx = a.X + t * vx;
        double qy = a.Y + t * vy;
        double ex = p.X - qx;
        double ey = p.Y - qy;
        return Math.Sqrt(ex * ex + ey * ey);
    }
}

public sealed class ArrowElement : LineElement
{
    public double HeadSize { get; set; } = 12;
}

public sealed class PenElement : EditorElement
{
    public List<EditorPoint> Points { get; } = new();

    public override bool HitTest(EditorPoint point)
    {
        if (Points.Count == 1)
        {
            return LineElement.DistanceToSegment(point, Points[0], Points[0]) <= Math.Max(StrokeWidth, 4);
        }

        for (int i = 1; i < Points.Count; i++)
        {
            if (LineElement.DistanceToSegment(point, Points[i - 1], Points[i]) <= Math.Max(StrokeWidth, 4))
            {
                return true;
            }
        }

        return false;
    }

    public override void MoveBy(double dx, double dy)
    {
        for (int i = 0; i < Points.Count; i++)
        {
            Points[i] = new EditorPoint(Points[i].X + dx, Points[i].Y + dy);
        }

        Bounds = Bounds.Offset(dx, dy);
    }

    public void RecalculateBounds()
    {
        if (Points.Count == 0)
        {
            Bounds = default;
            return;
        }

        double minX = Points.Min(p => p.X);
        double minY = Points.Min(p => p.Y);
        double maxX = Points.Max(p => p.X);
        double maxY = Points.Max(p => p.Y);
        Bounds = new EditorRect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
    }
}

public sealed class TextElement : EditorElement
{
    public string Text { get; set; } = "";
    public double FontSize { get; set; } = 16;
    public string FontFamily { get; set; } = "Segoe UI";

    public override bool HitTest(EditorPoint point) => Bounds.Contains(point);
}

public sealed class MosaicElement : EditorElement
{
    public int BlockSize { get; set; } = 12;

    public override bool HitTest(EditorPoint point) => Bounds.Contains(point);
}

public sealed class NumberElement : EditorElement
{
    public int Number { get; set; } = 1;

    public override bool HitTest(EditorPoint point) => Bounds.Contains(point);
}
