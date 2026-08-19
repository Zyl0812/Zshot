using System;
using System.Linq;
using Starshot.Core.Editor;

namespace Starshot.Features.Screenshot.Editor;

internal sealed class OverlayAnnotationController
{
    private EditorPoint _pressPoint;
    private bool _dragging;
    private double _moveDx;
    private double _moveDy;
    private int _nextNumber = 1;

    public EditorDocument Document { get; } = new();
    public EditorHistory History { get; } = new();
    public string Tool { get; set; } = "select";
    public EditorElement? Draft { get; private set; }
    public EditorElement? Selected { get; private set; }
    public bool IsDragging => _dragging;

    public bool Undo()
    {
        Selected = null;
        return History.Undo(Document);
    }

    public bool Redo() => History.Redo(Document);

    public void Clear()
    {
        History.Execute(Document, new ClearElementsCommand());
        Selected = null;
        Draft = null;
    }

    public bool DeleteSelected()
    {
        if (Selected is null)
        {
            return false;
        }

        History.Execute(Document, new RemoveElementCommand(Selected));
        Selected = null;
        return true;
    }

    public enum PressKind
    {
        None,
        Capture,
        RequestText,
    }

    public PressKind PointerPressed(EditorPoint pt)
    {
        _pressPoint = pt;
        _dragging = true;
        _moveDx = 0;
        _moveDy = 0;

        if (Tool == "select")
        {
            Selected = Document.Elements.LastOrDefault(el => el.HitTest(pt));
            foreach (var el in Document.Elements)
            {
                el.IsSelected = el == Selected;
            }

            return PressKind.Capture;
        }

        if (Tool == "text")
        {
            _dragging = false;
            return PressKind.RequestText;
        }

        if (Tool == "number")
        {
            History.Execute(Document, new AddElementCommand(new NumberElement
            {
                Number = _nextNumber++,
                Bounds = new EditorRect(pt.X - 14, pt.Y - 14, 28, 28),
            }));
            _dragging = false;
            return PressKind.None;
        }

        Draft = CreateDraft(pt);
        return PressKind.Capture;
    }

    public void PointerMoved(EditorPoint pt)
    {
        if (!_dragging)
        {
            return;
        }

        if (Tool == "select" && Selected is not null)
        {
            double dx = pt.X - _pressPoint.X;
            double dy = pt.Y - _pressPoint.Y;
            Selected.MoveBy(dx, dy);
            _moveDx += dx;
            _moveDy += dy;
            _pressPoint = pt;
            return;
        }

        if (Draft is not null)
        {
            UpdateDraft(Draft, _pressPoint, pt);
        }
    }

    public void PointerReleased()
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        if (Tool == "select" && Selected is not null && (_moveDx != 0 || _moveDy != 0))
        {
            Selected.MoveBy(-_moveDx, -_moveDy);
            History.Execute(Document, new MoveElementCommand(Selected, _moveDx, _moveDy));
        }

        if (Draft is not null)
        {
            bool tiny = Draft.Bounds.Width < 2 && Draft.Bounds.Height < 2 && Draft is not PenElement;
            if (!tiny)
            {
                History.Execute(Document, new AddElementCommand(Draft));
            }

            Draft = null;
        }
    }

    public void AddText(EditorPoint pt, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        History.Execute(Document, new AddElementCommand(new TextElement
        {
            Text = text,
            FontSize = 20,
            Bounds = new EditorRect(pt.X, pt.Y, Math.Max(40, text.Length * 12), 28),
        }));
    }

    public void CancelDraft()
    {
        Draft = null;
        _dragging = false;
    }

    public void ClearSelection()
    {
        if (Selected is not null)
        {
            Selected.IsSelected = false;
            Selected = null;
        }
    }

    private EditorElement CreateDraft(EditorPoint pt) => Tool switch
    {
        "ellipse" => new EllipseElement { Bounds = new EditorRect(pt.X, pt.Y, 1, 1) },
        "line" => new LineElement { Start = pt, End = pt, Bounds = new EditorRect(pt.X, pt.Y, 1, 1) },
        "arrow" => new ArrowElement { Start = pt, End = pt, Bounds = new EditorRect(pt.X, pt.Y, 1, 1) },
        "pen" => CreatePen(pt),
        "mosaic" => new MosaicElement { Bounds = new EditorRect(pt.X, pt.Y, 1, 1) },
        _ => new RectangleElement { Bounds = new EditorRect(pt.X, pt.Y, 1, 1) },
    };

    private static PenElement CreatePen(EditorPoint pt)
    {
        var pen = new PenElement();
        pen.Points.Add(pt);
        pen.RecalculateBounds();
        return pen;
    }

    private static void UpdateDraft(EditorElement draft, EditorPoint start, EditorPoint current)
    {
        switch (draft)
        {
            case LineElement line:
                line.End = current;
                line.Bounds = RectFromPoints(start, current);
                break;
            case PenElement pen:
                pen.Points.Add(current);
                pen.RecalculateBounds();
                break;
            default:
                draft.Bounds = RectFromPoints(start, current);
                break;
        }
    }

    private static EditorRect RectFromPoints(EditorPoint a, EditorPoint b)
    {
        double x = Math.Min(a.X, b.X);
        double y = Math.Min(a.Y, b.Y);
        return new EditorRect(x, y, Math.Max(1, Math.Abs(b.X - a.X)), Math.Max(1, Math.Abs(b.Y - a.Y)));
    }
}
