using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Starshot.Core.Editor;
using Starshot.Frameworks;
using Starshot.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;

namespace Starshot.Features.Screenshot.Editor;

[INotifyPropertyChanged]
public sealed partial class CaptureEditorWindow : WindowEx
{
    private readonly EditorDocument _document = new();
    private readonly EditorHistory _history = new();
    private readonly TaskCompletionSource<bool> _completion = new();
    private CanvasBitmap? _background;
    private EditorElement? _draft;
    private EditorElement? _selected;
    private EditorPoint _pressPoint;
    private string _tool = "rect";
    private int _nextNumber = 1;
    private bool _dragging;
    private double _moveDx;
    private double _moveDy;
    private bool _forceClose;

    public CanvasRenderTarget? Flattened { get; private set; }

    public CaptureEditorWindow()
    {
        InitializeComponent();
        Title = "Zshot";
        SetIcon();
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AdaptTitleBarButtonColorToActuallTheme();
        SetDragRectangles(new RectInt32(0, 0, 100000, (int)(36 * UIScale)));
        new SystemBackdropHelper(this).TrySetMica();
        AppWindow.Closing += AppWindow_Closing;
        ((FrameworkElement)Content).KeyDown += CaptureEditorWindow_KeyDown;
    }

    public async Task<bool> EditAsync(CanvasBitmap background)
    {
        _background = background;
        int w = (int)background.SizeInPixels.Width;
        int h = (int)background.SizeInPixels.Height;
        CenterInScreen(Math.Clamp(w + 40, 640, 1400), Math.Clamp(h + 120, 480, 900));
        Activate();
        return await _completion.Task;
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_forceClose)
        {
            return;
        }

        args.Cancel = true;
        Finish(false);
    }

    private void CaptureEditorWindow_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        bool ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        if (ctrl && e.Key == VirtualKey.Z)
        {
            Undo();
            e.Handled = true;
        }
        else if (ctrl && (e.Key == VirtualKey.Y || e.Key == VirtualKey.R))
        {
            Redo();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Delete && _selected is not null)
        {
            _history.Execute(_document, new RemoveElementCommand(_selected));
            _selected = null;
            DrawCanvas.Invalidate();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            Finish(false);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter)
        {
            Complete();
            e.Handled = true;
        }
    }

    [RelayCommand]
    private void SetTool(string? tool)
    {
        if (!string.IsNullOrWhiteSpace(tool))
        {
            _tool = tool;
            if (_selected is not null)
            {
                _selected.IsSelected = false;
                _selected = null;
                DrawCanvas.Invalidate();
            }
        }
    }

    [RelayCommand]
    private void Undo()
    {
        if (_history.Undo(_document))
        {
            _selected = null;
            DrawCanvas.Invalidate();
        }
    }

    [RelayCommand]
    private void Redo()
    {
        if (_history.Redo(_document))
        {
            DrawCanvas.Invalidate();
        }
    }

    [RelayCommand]
    private void Clear()
    {
        _history.Execute(_document, new ClearElementsCommand());
        _selected = null;
        DrawCanvas.Invalidate();
    }

    [RelayCommand]
    private void Complete() => Finish(true);

    [RelayCommand]
    private void Cancel() => Finish(false);

    private void Finish(bool confirmed)
    {
        if (_completion.Task.IsCompleted)
        {
            return;
        }

        if (confirmed && _background is not null)
        {
            int w = (int)_background.SizeInPixels.Width;
            int h = (int)_background.SizeInPixels.Height;
            Flattened = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), w, h, 96, _background.Format, Microsoft.Graphics.Canvas.CanvasAlphaMode.Premultiplied);
            using var ds = Flattened.CreateDrawingSession();
            EditorRenderer.Flatten(ds, _background, _document.Elements);
        }

        _forceClose = true;
        _completion.TrySetResult(confirmed);
        Close();
    }

    private void DrawCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (_background is null)
        {
            return;
        }

        var (scale, ox, oy) = GetView();
        args.DrawingSession.Transform = System.Numerics.Matrix3x2.CreateScale(scale) * System.Numerics.Matrix3x2.CreateTranslation(ox, oy);
        args.DrawingSession.DrawImage(_background);
        foreach (var element in _document.Elements)
        {
            EditorRenderer.Draw(args.DrawingSession, element);
        }

        if (_draft is not null)
        {
            EditorRenderer.Draw(args.DrawingSession, _draft);
        }
    }

    private void DrawCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_background is null)
        {
            return;
        }

        DrawCanvas.Focus(FocusState.Programmatic);
        var pt = ToImage(e.GetCurrentPoint(DrawCanvas).Position);
        _pressPoint = pt;
        _dragging = true;

        if (_tool == "select")
        {
            _selected = _document.Elements.LastOrDefault(el => el.HitTest(pt));
            foreach (var el in _document.Elements)
            {
                el.IsSelected = el == _selected;
            }
            _moveDx = 0;
            _moveDy = 0;
        }
        else if (_tool == "text")
        {
            _ = AddTextAsync(pt);
            _dragging = false;
        }
        else if (_tool == "number")
        {
            var number = new NumberElement
            {
                Number = _nextNumber++,
                Bounds = new EditorRect(pt.X - 14, pt.Y - 14, 28, 28),
            };
            _history.Execute(_document, new AddElementCommand(number));
            _dragging = false;
        }
        else
        {
            _draft = CreateDraft(pt);
        }

        DrawCanvas.Invalidate();
        e.Handled = true;
    }

    private void DrawCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var pt = ToImage(e.GetCurrentPoint(DrawCanvas).Position);
        if (_tool == "select" && _selected is not null)
        {
            double dx = pt.X - _pressPoint.X;
            double dy = pt.Y - _pressPoint.Y;
            _selected.MoveBy(dx, dy);
            _moveDx += dx;
            _moveDy += dy;
            _pressPoint = pt;
        }
        else if (_draft is not null)
        {
            UpdateDraft(_draft, _pressPoint, pt);
        }

        DrawCanvas.Invalidate();
    }

    private void DrawCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        if (_tool == "select" && _selected is not null && (_moveDx != 0 || _moveDy != 0))
        {
            _selected.MoveBy(-_moveDx, -_moveDy);
            _history.Execute(_document, new MoveElementCommand(_selected, _moveDx, _moveDy));
        }

        if (_draft is not null)
        {
            bool tiny = _draft.Bounds.Width < 2 && _draft.Bounds.Height < 2 && _draft is not PenElement;
            if (!tiny)
            {
                _history.Execute(_document, new AddElementCommand(_draft));
            }

            _draft = null;
        }

        DrawCanvas.Invalidate();
    }

    private async Task AddTextAsync(EditorPoint pt)
    {
        var box = new TextBox { Text = "", MinWidth = 240 };
        var dialog = new ContentDialog
        {
            Title = "文字",
            Content = box,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            XamlRoot = Content.XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(box.Text))
        {
            var text = new TextElement
            {
                Text = box.Text,
                FontSize = 20,
                Bounds = new EditorRect(pt.X, pt.Y, Math.Max(40, box.Text.Length * 12), 28),
            };
            _history.Execute(_document, new AddElementCommand(text));
            DrawCanvas.Invalidate();
        }
    }

    private EditorElement CreateDraft(EditorPoint pt) => _tool switch
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

    private (float scale, float ox, float oy) GetView()
    {
        if (_background is null)
        {
            return (1, 0, 0);
        }

        float cw = (float)DrawCanvas.ActualWidth;
        float ch = (float)DrawCanvas.ActualHeight;
        float iw = _background.SizeInPixels.Width;
        float ih = _background.SizeInPixels.Height;
        float scale = Math.Min(cw / Math.Max(1, iw), ch / Math.Max(1, ih));
        return (scale, (cw - iw * scale) / 2, (ch - ih * scale) / 2);
    }

    private EditorPoint ToImage(Point pt)
    {
        var (scale, ox, oy) = GetView();
        return new EditorPoint((pt.X - ox) / scale, (pt.Y - oy) / scale);
    }
}
