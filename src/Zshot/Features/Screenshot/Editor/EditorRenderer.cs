using System;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI;
using Zshot.Core.Editor;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;

namespace Zshot.Features.Screenshot.Editor;

internal static class EditorRenderer
{
    public static void Draw(CanvasDrawingSession ds, EditorElement element, CanvasBitmap? background = null, double sampleScaleX = 1, double sampleScaleY = 1)
    {
        if (element is MosaicElement mosaic && background is not null)
        {
            DrawPixelate(ds, background, mosaic, sampleScaleX, sampleScaleY);
            if (mosaic.IsSelected)
            {
                ds.DrawRectangle(ToRect(mosaic.Bounds), Colors.White, 1);
            }

            return;
        }

        DrawShape(ds, element);
    }

    private static void DrawShape(CanvasDrawingSession ds, EditorElement element)
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

    public static void DrawPixelate(CanvasDrawingSession ds, CanvasBitmap background, MosaicElement mosaic, double sampleScaleX = 1, double sampleScaleY = 1)
    {
        var dest = ToRect(mosaic.Bounds);
        if (dest.Width < 1 || dest.Height < 1)
        {
            return;
        }

        // 采样矩形必须钳在 background 内：越界会让 DrawImage 抛 E_BOUNDS（同放大镜那处）
        float bw = background.SizeInPixels.Width;
        float bh = background.SizeInPixels.Height;
        float sx = Math.Clamp((float)(dest.X * sampleScaleX), 0, bw);
        float sy = Math.Clamp((float)(dest.Y * sampleScaleY), 0, bh);
        float sw = Math.Clamp((float)(dest.Width * sampleScaleX), 0, bw - sx);
        float sh = Math.Clamp((float)(dest.Height * sampleScaleY), 0, bh - sy);
        if (sw < 1 || sh < 1)
        {
            return;
        }

        int block = Math.Max(2, mosaic.BlockSize);
        float tw = Math.Max(1, (float)dest.Width / block);
        float th = Math.Max(1, (float)dest.Height / block);
        using var tiny = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), tw, th, 96, DirectXPixelFormat.B8G8R8A8UIntNormalized, CanvasAlphaMode.Premultiplied);
        using (var tds = tiny.CreateDrawingSession())
        {
            tds.DrawImage(background, new Rect(0, 0, tw, th), new Rect(sx, sy, sw, sh));
        }

        ds.DrawImage(tiny, dest, new Rect(0, 0, tw, th), 1f, CanvasImageInterpolation.NearestNeighbor);
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
