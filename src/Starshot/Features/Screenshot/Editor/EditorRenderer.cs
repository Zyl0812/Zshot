using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI;
using Starshot.Core.Editor;
using Windows.Foundation;
using Windows.UI;

namespace Starshot.Features.Screenshot.Editor;

internal static class EditorRenderer
{
    public static void Draw(CanvasDrawingSession ds, EditorElement element)
    {
        var stroke = Parse(element.StrokeColor);
        float width = (float)Math.Max(1, element.StrokeWidth);

        switch (element)
        {
            case RectangleElement rect:
                ds.DrawRectangle(ToRect(rect.Bounds), stroke, width);
                if (rect.FillColor is not null)
                {
                    ds.FillRectangle(ToRect(rect.Bounds), Parse(rect.FillColor));
                }
                break;
            case EllipseElement ellipse:
                ds.DrawEllipse(
                    (float)(ellipse.Bounds.X + ellipse.Bounds.Width / 2),
                    (float)(ellipse.Bounds.Y + ellipse.Bounds.Height / 2),
                    (float)(ellipse.Bounds.Width / 2),
                    (float)(ellipse.Bounds.Height / 2),
                    stroke,
                    width);
                break;
            case ArrowElement arrow:
                ds.DrawLine((float)arrow.Start.X, (float)arrow.Start.Y, (float)arrow.End.X, (float)arrow.End.Y, stroke, width);
                DrawArrowHead(ds, arrow.Start, arrow.End, stroke, width, (float)arrow.HeadSize);
                break;
            case LineElement line:
                ds.DrawLine((float)line.Start.X, (float)line.Start.Y, (float)line.End.X, (float)line.End.Y, stroke, width);
                break;
            case PenElement pen:
                if (pen.Points.Count == 1)
                {
                    ds.FillCircle((float)pen.Points[0].X, (float)pen.Points[0].Y, width / 2, stroke);
                }
                else if (pen.Points.Count > 1)
                {
                    using var path = new CanvasPathBuilder(ds);
                    path.BeginFigure((float)pen.Points[0].X, (float)pen.Points[0].Y);
                    for (int i = 1; i < pen.Points.Count; i++)
                    {
                        path.AddLine((float)pen.Points[i].X, (float)pen.Points[i].Y);
                    }
                    path.EndFigure(CanvasFigureLoop.Open);
                    using var geo = CanvasGeometry.CreatePath(path);
                    ds.DrawGeometry(geo, stroke, width);
                }
                break;
            case TextElement text:
                using (var format = new CanvasTextFormat { FontSize = (float)text.FontSize, FontFamily = text.FontFamily })
                {
                    ds.DrawText(text.Text ?? "", (float)text.Bounds.X, (float)text.Bounds.Y, stroke, format);
                }
                break;
            case MosaicElement mosaic:
                ds.FillRectangle(ToRect(mosaic.Bounds), Color.FromArgb(80, 80, 80, 80));
                ds.DrawRectangle(ToRect(mosaic.Bounds), stroke, 1);
                break;
            case NumberElement number:
                float r = (float)Math.Max(12, Math.Min(number.Bounds.Width, number.Bounds.Height) / 2);
                float cx = (float)(number.Bounds.X + number.Bounds.Width / 2);
                float cy = (float)(number.Bounds.Y + number.Bounds.Height / 2);
                ds.FillCircle(cx, cy, r, stroke);
                using (var format = new CanvasTextFormat { FontSize = r, HorizontalAlignment = CanvasHorizontalAlignment.Center, VerticalAlignment = CanvasVerticalAlignment.Center })
                {
                    ds.DrawText(number.Number.ToString(), cx, cy, Colors.White, format);
                }
                break;
        }

        if (element.IsSelected)
        {
            ds.DrawRectangle(ToRect(element.Bounds), Colors.White, 1);
        }
    }

    public static void Flatten(CanvasDrawingSession ds, CanvasBitmap background, IEnumerable<EditorElement> elements)
    {
        ds.DrawImage(background);
        foreach (var element in elements)
        {
            if (element is MosaicElement mosaic)
            {
                DrawPixelate(ds, background, mosaic);
            }
            else
            {
                Draw(ds, element);
            }
        }
    }

    public static void DrawPixelate(CanvasDrawingSession ds, CanvasBitmap background, MosaicElement mosaic)
    {
        int block = Math.Max(4, mosaic.BlockSize);
        int x0 = (int)Math.Floor(mosaic.Bounds.X);
        int y0 = (int)Math.Floor(mosaic.Bounds.Y);
        int x1 = (int)Math.Ceiling(mosaic.Bounds.X + mosaic.Bounds.Width);
        int y1 = (int)Math.Ceiling(mosaic.Bounds.Y + mosaic.Bounds.Height);
        x0 = Math.Clamp(x0, 0, (int)background.SizeInPixels.Width - 1);
        y0 = Math.Clamp(y0, 0, (int)background.SizeInPixels.Height - 1);
        x1 = Math.Clamp(x1, 0, (int)background.SizeInPixels.Width);
        y1 = Math.Clamp(y1, 0, (int)background.SizeInPixels.Height);

        for (int y = y0; y < y1; y += block)
        {
            for (int x = x0; x < x1; x += block)
            {
                int bw = Math.Min(block, x1 - x);
                int bh = Math.Min(block, y1 - y);
                var colors = background.GetPixelColors(x, y, 1, 1);
                ds.FillRectangle(x, y, bw, bh, colors[0]);
            }
        }
    }

    private static void DrawArrowHead(CanvasDrawingSession ds, EditorPoint start, EditorPoint end, Color color, float width, float head)
    {
        double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        float size = Math.Max(head, width * 3);
        var left = new System.Numerics.Vector2(
            (float)(end.X - size * Math.Cos(angle - 0.45)),
            (float)(end.Y - size * Math.Sin(angle - 0.45)));
        var right = new System.Numerics.Vector2(
            (float)(end.X - size * Math.Cos(angle + 0.45)),
            (float)(end.Y - size * Math.Sin(angle + 0.45)));
        ds.DrawLine((float)end.X, (float)end.Y, left.X, left.Y, color, width);
        ds.DrawLine((float)end.X, (float)end.Y, right.X, right.Y, color, width);
    }

    private static Rect ToRect(EditorRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    private static Color Parse(string color)
    {
        if (string.IsNullOrWhiteSpace(color) || color.Length < 7)
        {
            return Colors.Red;
        }

        try
        {
            string hex = color.TrimStart('#');
            if (hex.Length == 6)
            {
                hex = "FF" + hex;
            }

            byte a = Convert.ToByte(hex[..2], 16);
            byte r = Convert.ToByte(hex.Substring(2, 2), 16);
            byte g = Convert.ToByte(hex.Substring(4, 2), 16);
            byte b = Convert.ToByte(hex.Substring(6, 2), 16);
            return Color.FromArgb(a, r, g, b);
        }
        catch
        {
            return Colors.Red;
        }
    }
}
