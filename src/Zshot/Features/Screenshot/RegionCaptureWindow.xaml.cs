using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Zshot.Features.Codec;
using Zshot.Core;
using Zshot.Core.Editor;
using Zshot.Core.Overlay;
using Zshot.Core.Translation;
using Zshot.Features.Screenshot.Editor;
using Zshot.Features.Screenshot.LongCapture;
using Zshot.Features.Screenshot.Ocr;
using Zshot.Frameworks;
using Zshot.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;

namespace Zshot.Features.Screenshot;

public sealed partial class RegionCaptureWindow : WindowEx
{
    private const int MinimumRectangleSize = 5;
    private const int MagnifierPixelCount = 15;
    private const int MagnifierPixelSize = 10;
    private const double HandleSize = 8;
    private const uint WdaExcludeFromCapture = 0x11;
    private const uint WdaNone = 0;
    private const int WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;

    public Rect SelectionRect { get; private set; }
    // 确认时从 _displayBitmap（冻结帧，已 tonemap 的 SDR）裁出的选区，供剪贴板直接复用，不再二次 tonemap
    public CanvasRenderTarget? SdrCrop { get; private set; }

    private CanvasBitmap _canvasOriginal;    // 原始帧（裁剪用，可能 HDR），每次 SetCapture 更新
    private CanvasBitmap? _displayBitmap;    // 显示用（SDR 色调映射后），每次 SetCapture 重建；会话间为 null（CloseWindow 清引用）
    private float _scale;
    private readonly int _vx, _vy;  // 虚拟屏幕物理坐标原点（放大镜钳制到当前显示器用）

    private Point _positionOnClick;
    private bool _isMouseDown;
    private bool _pressedOnHover;  // 左键按下瞬间是否悬停在某个窗口上（单击截图用）
    private Point _currentMousePos;
    // 选区来源：true=鼠标框选（端点是光标像素索引，需 +1，对应 CreateRectangle）；
    // false=窗口矩形（本身就是正常尺寸，不 +1）
    private bool _selectionFromDrag;

    private List<Rect> _windowRects = new();
    private Rect _hoverRect;
    private bool _hasHover;

    private float _dashOffset;
    private readonly System.Diagnostics.Stopwatch _timer;
    private bool _isClosed;
    private CanvasSwapChain? _swapChain;
    private DispatcherTimer _renderTimer;

    // 锁定画布尺寸（首帧后固定，防止布局抖动导致冻结帧移动）
    private float _lockedW;
    private float _lockedH;
    private bool _sizeLocked;

    // HDR 时 _displayBitmap 是本窗新建的 SDR 副本，由本窗释放；
    // SDR 时它就是传入的 canvas（= composite），归调用方，不能动
    private bool _ownsDisplayBitmap;
    private bool _cleanedUp;
    // 关窗移屏外方案配套：截图前的前台窗口（关窗时还焦点）、待移回屏内标记与节拍计数
    private nint _prevForeground;
    private bool _pendingMoveIn;
    private int _moveInTick;

    // 单例：整段 Overlay 会话结束才完成；窗口不 Close，只移出屏幕
    public TaskCompletionSource<RegionOverlayResult> Completion { get; private set; }

    private OverlayPhase _phase;
    private bool _copyOnlySession;
    private OverlayAnnotationController _editor = new();
    private SelectionHitKind _activeHit;
    private EditorRect _selectionAtPress;
    private EditorPoint _pointerAtPress;
    private bool _manipulatingSelection;
    private EditorPoint _textPoint;
    private string _lastOcrText = "";
    private bool _ocrRunning;
    private static readonly HttpClient TranslationHttp = new() { Timeout = Timeout.InfiniteTimeSpan };
    // OCR 引擎持有已加载的 ONNX 模型，整个进程复用一份
    private static readonly OcrSession SharedOcr = new(new RapidOcrRecognizer());
    private CancellationTokenSource? _longCts;
    private TaskCompletionSource<bool>? _longDecision;


    public RegionCaptureWindow()
    {
        InitializeComponent();
        _timer = System.Diagnostics.Stopwatch.StartNew();
        this.Closed += RegionCaptureWindow_Closed;

        // 窗口设置（单例，只一次）
        WindowEx.MainWindowId = AppWindow.Id;
        Title = "Zshot";
        AppWindow.IsShownInSwitchers = false;
        SystemBackdrop = new TransparentBackdrop();

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(false, false);
        }

        int vx = User32.GetSystemMetrics((User32.SystemMetric)76);
        int vy = User32.GetSystemMetrics((User32.SystemMetric)77);
        int vw = User32.GetSystemMetrics((User32.SystemMetric)78);
        int vh = User32.GetSystemMetrics((User32.SystemMetric)79);
        _vx = vx;
        _vy = vy;
        AppWindow.MoveAndResize(new RectInt32(vx, vy, vw, vh));

        // 清除残留窗口边框样式（WinUI 的 SetBorderAndTitleBar 仍留 ~2px resize frame）
        var style = (User32.WindowStyles)User32.GetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_STYLE);
        style &= ~(User32.WindowStyles.WS_THICKFRAME | User32.WindowStyles.WS_BORDER | User32.WindowStyles.WS_CAPTION | User32.WindowStyles.WS_DLGFRAME);
        User32.SetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_STYLE, (nint)style);
        User32.SetWindowPos(WindowHandle, IntPtr.Zero, vx, vy, vw, vh, (User32.SetWindowPosFlags)0x0020 | User32.SetWindowPosFlags.SWP_NOZORDER);

        PointerCursor.SetCursorShape(Canvas, InputSystemCursorShape.Cross);

        // _scale 按覆盖层窗口 DPI（d56df02）；swapChain 移到 SetCapture 创建（CloseWindow 释放本进程显存）
        float dpi = User32.GetDpiForWindow(WindowHandle);
        _scale = dpi / 96f;
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _renderTimer.Tick += (_, _) => Redraw();
        // 不 Start：SetCapture 时启动（单例每次截图复用窗口）
    }


    /// <summary>
    /// 每次截图调用：更新冻结帧 + 重置交互状态 + 显示。窗口单例且永不 Hide——
    /// 关窗时移到屏外保持 IsWindowVisible（合成管线不停摆），下次截图先把新帧 Present 上屏
    /// 再移回屏内，从根上避免 Show 瞬间 DWM 先合成保留的旧会话帧（启动闪上次截图界面）。
    /// </summary>
    public void SetCapture(CanvasBitmap canvas, float sdrWhiteLevel, int physW, int physH, bool copyOnly = false)
    {
        // swapChain 常驻（关窗只移屏外不销毁）；分辨率变了尺寸过期则重建
        float needW = physW / _scale, needH = physH / _scale;
        if (_swapChain is null
            || Math.Abs((float)_swapChain.Size.Width - needW) > 0.5f
            || Math.Abs((float)_swapChain.Size.Height - needH) > 0.5f)
        {
            try { Canvas.SwapChain = null; _swapChain?.Dispose(); } catch { }
            _swapChain = new CanvasSwapChain(CanvasDevice.GetSharedDevice(), needW, needH, _scale * 96f, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, CanvasAlphaMode.Premultiplied);
            Canvas.SwapChain = _swapChain;
        }

        // 更新帧（旧的已在 CloseWindow 释放并清引用）
        _canvasOriginal = canvas;
        _displayBitmap = CreateDisplayBitmap(canvas, physW, physH, sdrWhiteLevel);
        _ownsDisplayBitmap = !ReferenceEquals(_displayBitmap, canvas);

        // 重置交互状态（为本次截图清场）
        SelectionRect = default;
        SdrCrop = null;
        _positionOnClick = default;
        _isMouseDown = false;
        _pressedOnHover = false;
        if (User32.GetCursorPos(out var initCursor))
        {
            _currentMousePos = new Point((initCursor.x - _vx) / _scale, (initCursor.y - _vy) / _scale);
        }
        _selectionFromDrag = false;
        _windowRects = new List<Rect>();
        _hoverRect = default;
        _hasHover = false;
        _lockedW = 0;
        _lockedH = 0;
        _sizeLocked = false;  // 首帧重新锁尺寸 + 触发 DetectWindows
        _isClosed = false;
        _cleanedUp = false;
        _phase = OverlayPhase.Selecting;
        _copyOnlySession = copyOnly;
        _editor = new OverlayAnnotationController();
        _activeHit = SelectionHitKind.None;
        _manipulatingSelection = false;
        _lastOcrText = "";
        _ocrRunning = false;
        CancelLongCaptureInternal(restorePhase: false);
        SetExcludeFromCapture(false);
        HideChrome();
        Completion = new TaskCompletionSource<RegionOverlayResult>();
        _prevForeground = (nint)User32.GetForegroundWindow();

        _renderTimer.Start();
        Show();          // 首次显示；后续会话窗口一直可见（在屏外），no-op
        Redraw();        // 屏外先把新冻结帧 Present 上屏（窗口可见，合成照常提交）
        _pendingMoveIn = true;   // 第 2 个 tick（新帧确定已合成）再移回屏内，移回瞬间不可能是旧内容
        _moveInTick = 0;
    }


    private static CanvasBitmap CreateDisplayBitmap(CanvasBitmap source, int w, int h, float sdrWhiteLevel)
    {
        if (source.Format is DirectXPixelFormat.R8G8B8A8UIntNormalized or DirectXPixelFormat.B8G8R8A8UIntNormalized)
        {
            return source;
        }

        var device = CanvasDevice.GetSharedDevice();
        var sdr = new CanvasRenderTarget(device, w, h, 96, DirectXPixelFormat.B8G8R8A8UIntNormalized, CanvasAlphaMode.Premultiplied);
        using (var ds = sdr.CreateDrawingSession())
        {
            var wle = new WhiteLevelAdjustmentEffect
            {
                Source = source,
                InputWhiteLevel = 80,
                OutputWhiteLevel = sdrWhiteLevel,
                BufferPrecision = CanvasBufferPrecision.Precision16Float,
            };
            var gamma = new SrgbGammaEffect
            {
                Source = wle,
                GammaMode = SrgbGammaMode.OETF,
                BufferPrecision = CanvasBufferPrecision.Precision16Float,
            };
            ds.DrawImage(gamma);
        }
        return sdr;
    }


    // 直接 P/Invoke DwmGetWindowAttribute，避免 Vanara 泛型重载在 DWMWA_CLOAKED 上 marshal 不可靠
    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetCloaked(IntPtr hwnd, int attr, out int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetExtendedFrameBounds(IntPtr hwnd, int attr, ref RECT pvAttribute, int cbAttribute);


    // 移植自 WindowsRectangleList：跳过 cloaked / TOOLWINDOW&NOACTIVATE 等垃圾窗口，
    // DWM 扩展边界去阴影，额外加入 client rect（可吸到内容区），最后去重。
    private void DetectWindows()
    {
        var raw = new List<(Rect rect, bool isWindow)>();

        User32.EnumWindows((hWnd, _) =>
        {
            try
            {
                if (!User32.IsWindowVisible(hWnd)) return true;
                if (User32.IsIconic(hWnd)) return true;
                if (hWnd == WindowHandle) return true;

                // cloaked（隐藏的 UWP / 最小化到任务栏 / 其它虚拟桌面等，真正不可见）
                try
                {
                    if (DwmGetCloaked(hWnd.DangerousGetHandle(), 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                        return true;
                }
                catch { }

                // 跳过 non-activatable tool windows：任务栏/托盘/平铺管理器 overlay/各种小工具
                var exStyle = (User32.WindowStylesEx)User32.GetWindowLong(hWnd, User32.WindowLongFlags.GWL_EXSTYLE);
                const User32.WindowStylesEx junk = User32.WindowStylesEx.WS_EX_TOOLWINDOW | User32.WindowStylesEx.WS_EX_NOACTIVATE;
                if ((exStyle & junk) == junk) return true;

                // 窗口矩形：DWM 扩展边界（去阴影），失败回退 GetWindowRect
                RECT wr = default;
                bool hasWr = false;
                try
                {
                    if (DwmGetExtendedFrameBounds(hWnd.DangerousGetHandle(), 9, ref wr, Marshal.SizeOf<RECT>()) == 0)
                        hasWr = wr.Width > 0 && wr.Height > 0;
                }
                catch { }
                if (!hasWr)
                {
                    if (!User32.GetWindowRect(hWnd, out wr)) return true;
                }
                if (wr.Width <= 5 || wr.Height <= 5) return true;

                var winRect = new Rect(wr.left / _scale, wr.top / _scale, wr.Width / _scale, wr.Height / _scale);

                // 客户区（若与窗口矩形明显不同）：放在窗口矩形之前入列，使悬停优先命中内容区
                Rect? clientRect = null;
                try
                {
                    if (User32.GetClientRect(hWnd, out RECT cr) && cr.Width > 5 && cr.Height > 5)
                    {
                        POINT tl = new POINT { x = 0, y = 0 };
                        if (User32.ClientToScreen(hWnd, ref tl))
                        {
                            var c = new Rect((tl.x + cr.left) / _scale, (tl.y + cr.top) / _scale,
                                cr.Width / _scale, cr.Height / _scale);
                            if (Math.Abs(c.X - winRect.X) > 2 || Math.Abs(c.Y - winRect.Y) > 2 ||
                                Math.Abs(c.Width - winRect.Width) > 2 || Math.Abs(c.Height - winRect.Height) > 2)
                            {
                                clientRect = c;
                            }
                        }
                    }
                }
                catch { }

                if (clientRect.HasValue) raw.Add((clientRect.Value, false));
                raw.Add((winRect, true));
            }
            catch { }
            return true;
        }, IntPtr.Zero);

        // 去重：仅对非顶级窗口（client rect）做包含剔除，顶级窗口始终保留
        var result = new List<Rect>();
        foreach (var (rect, isWindow) in raw)
        {
            bool keep = true;
            if (!isWindow)
            {
                foreach (var r in result)
                {
                    // Windows.Foundation.Rect 没有 Contains(Rect)，手动判断 outer 是否包含 inner
                    if (r.X <= rect.X && r.Y <= rect.Y &&
                        r.X + r.Width >= rect.X + rect.Width &&
                        r.Y + r.Height >= rect.Y + rect.Height)
                    { keep = false; break; }
                }
            }
            if (keep) result.Add(rect);
        }
        _windowRects = result;

        // 窗口列表就绪后，立即对初始光标位置做悬停命中——不必等第一次 PointerMoved
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_isClosed)
            {
                UpdateHover(_currentMousePos);
            }
        });
    }


    private void Redraw()
    {
        if (_isClosed || _swapChain is null || _displayBitmap is null) return;

        _dashOffset = (float)_timer.Elapsed.TotalSeconds * -15;

        // 首帧锁定画布尺寸（_scale 构造时已按覆盖层窗口 DPI 设定）
        if (!_sizeLocked)
        {
            _lockedW = (float)_swapChain.Size.Width;
            _lockedH = (float)_swapChain.Size.Height;
            _sizeLocked = true;
            if (User32.GetCursorPos(out var initCursor))
            {
                _currentMousePos = new Point((initCursor.x - _vx) / _scale, (initCursor.y - _vy) / _scale);
            }
            _ = Task.Run(DetectWindows);
        }

        using (var ds = _swapChain.CreateDrawingSession(Colors.Transparent))
        {
            float physW = (float)_displayBitmap.SizeInPixels.Width;
            float physH = (float)_displayBitmap.SizeInPixels.Height;

        if (_phase == OverlayPhase.LongCapturing)
        {
            FillDimOutside(ds, SelectionRect);
        }
        else
        {
            ds.DrawImage(_displayBitmap,
                new Rect(0, 0, _lockedW, _lockedH),
                new Rect(0, 0, physW, physH),
                1f, CanvasImageInterpolation.Linear);
            ds.FillRectangle(new Rect(0, 0, _lockedW, _lockedH), Color.FromArgb(51, 0, 0, 0));
        }

        Rect rect = default;
        bool hasRect = false;
        bool fromDrag = _selectionFromDrag;

        if (_phase is OverlayPhase.SelectionActive or OverlayPhase.LongCapturing)
        {
            rect = SelectionRect;
            hasRect = rect.Width > 2 && rect.Height > 2;
        }
        else if (_isMouseDown && SelectionRect.Width > MinimumRectangleSize && SelectionRect.Height > MinimumRectangleSize)
        {
            rect = SelectionRect;
            hasRect = true;
            fromDrag = true;
        }
        else if (_hasHover && _hoverRect.Width > 2 && _hoverRect.Height > 2)
        {
            rect = _hoverRect;
            hasRect = true;
            fromDrag = false;
        }

        if (hasRect)
        {
            if (_phase != OverlayPhase.LongCapturing)
            {
                HighlightRect(ds, rect, physW, physH);
            }

            ds.DrawRectangle(rect, Colors.Black, 1);
            using var anim = new CanvasStrokeStyle { CustomDashStyle = new float[] { 5, 5 }, DashOffset = _dashOffset };
            ds.DrawRectangle(rect, Colors.White, 1, anim);

            if (_phase == OverlayPhase.SelectionActive)
            {
                DrawHandles(ds, rect);
                using (ds.CreateLayer(1, rect))
                {
                    double sampleX = physW / Math.Max(1, _lockedW);
                    double sampleY = physH / Math.Max(1, _lockedH);
                    foreach (var element in _editor.Document.Elements)
                    {
                        EditorRenderer.Draw(ds, element, _displayBitmap, sampleX, sampleY);
                    }

                    if (_editor.Draft is not null)
                    {
                        EditorRenderer.Draw(ds, _editor.Draft, _displayBitmap, sampleX, sampleY);
                    }
                }
            }

            var phys = ComputePhysicalRect(rect, fromDrag);
            DrawInfoBox(ds, $"X: {(int)phys.X}, Y: {(int)phys.Y}, W: {(int)phys.Width}, H: {(int)phys.Height}",
                new Vector2((float)rect.X + 3, (float)rect.Y + 3));
        }

        if (_phase == OverlayPhase.Selecting)
        {
            float mx = (float)_currentMousePos.X, my = (float)_currentMousePos.Y;
            GetActiveMonitorDip(mx, my, out float ml, out float mt, out float mr, out float mb);
            DrawMagnifier(ds, mx, my, ml, mt, mr, mb);

            const float cbW = 160, cbH = 22, cbOff = 12;
            float cbX = mx + cbOff, cbY = my + cbOff;
            if (cbX + cbW > mr) cbX = mx - cbOff - cbW;
            if (cbY + cbH > mb) cbY = my - cbOff - cbH;
            if (cbX < ml) cbX = ml;
            if (cbY < mt) cbY = mt;
            DrawInfoBox(ds, $"X: {(int)(mx * _scale)} Y: {(int)(my * _scale)}", new Vector2(cbX, cbY));
        }
        }
        _swapChain.Present();

        // SetCapture 挂的移回任务：第 2 个 tick（首帧 Present 已过 16ms，新帧确定合成进 surface）移回屏内
        if (_pendingMoveIn)
        {
            _moveInTick++;
            if (_moveInTick >= 2)
            {
                _pendingMoveIn = false;
                MoveOnscreen();
            }
        }
    }


    // 光标所在显示器在 canvas DIP 坐标下的边界（放大镜、坐标框共用，不跨屏）
    private void GetActiveMonitorDip(float mx, float my, out float l, out float t, out float r, out float b)
    {
        l = 0; t = 0; r = _lockedW; b = _lockedH;
        try
        {
            POINT phys = new POINT { x = (int)(mx * _scale + _vx), y = (int)(my * _scale + _vy) };
            var mon = User32.MonitorFromPoint(phys, User32.MonitorFlags.MONITOR_DEFAULTTONEAREST);
            var mi = new User32.MONITORINFOEX { cbSize = (uint)Marshal.SizeOf<User32.MONITORINFOEX>() };
            if (User32.GetMonitorInfo(mon, ref mi))
            {
                l = (mi.rcMonitor.left - _vx) / _scale;
                t = (mi.rcMonitor.top - _vy) / _scale;
                r = (mi.rcMonitor.right - _vx) / _scale;
                b = (mi.rcMonitor.bottom - _vy) / _scale;
            }
        }
        catch { }
    }

    private void DrawMagnifier(CanvasDrawingSession ds, float mx, float my, float monLeft, float monTop, float monRight, float monBottom)
    {
        if (_displayBitmap is null) return;
        int halfCount = MagnifierPixelCount / 2;
        int magSize = MagnifierPixelCount * MagnifierPixelSize;
        const int offset = 10;

        float destX = mx + offset;
        float destY = my + offset;
        if (destX + magSize > monRight) destX = mx - offset - magSize;
        if (destY + magSize > monBottom) destY = my - offset - magSize;
        if (destX < monLeft) destX = monLeft;
        if (destY < monTop) destY = monTop;

        // 源矩形整数对齐，让 NearestNeighbor 真正锐利（不再糊）
        int srcX = (int)Math.Floor(mx * _scale) - halfCount;
        int srcY = (int)Math.Floor(my * _scale) - halfCount;
        // 钳制到 bitmap bounds 内：鼠标在屏幕边缘时 srcX/srcY 可能负或越界，
        // DrawImage sourceRect 越出 bitmap → E_BOUNDS → stowed exception → fail-fast
        srcX = Math.Clamp(srcX, 0, (int)_displayBitmap.SizeInPixels.Width - MagnifierPixelCount);
        srcY = Math.Clamp(srcY, 0, (int)_displayBitmap.SizeInPixels.Height - MagnifierPixelCount);

        ds.DrawImage(_displayBitmap,
            new Rect(destX, destY, magSize, magSize),
            new Rect(srcX, srcY, MagnifierPixelCount, MagnifierPixelCount),
            1f, CanvasImageInterpolation.NearestNeighbor);

        // 像素网格：让放大的每个像素清晰可辨
        var grid = Color.FromArgb(45, 0, 0, 0);
        for (int i = 1; i < MagnifierPixelCount; i++)
        {
            float gx = destX + i * MagnifierPixelSize;
            float gy = destY + i * MagnifierPixelSize;
            ds.DrawLine(new Vector2(gx, destY), new Vector2(gx, destY + magSize), grid, 1);
            ds.DrawLine(new Vector2(destX, gy), new Vector2(destX + magSize, gy), grid, 1);
        }

        ds.DrawRectangle(new Rect(destX - 1, destY - 1, magSize + 2, magSize + 2), Colors.White, 1);
        ds.DrawRectangle(new Rect(destX, destY, magSize, magSize), Colors.Black, 1);

        float cx = destX + magSize / 2f;
        float cy = destY + magSize / 2f;
        float ps = MagnifierPixelSize / 2f;
        var cc = Color.FromArgb(125, 173, 216, 230);
        ds.FillRectangle(new Rect(destX, cy - ps / 2, cx - ps / 2 - destX, ps), cc);
        ds.FillRectangle(new Rect(cx + ps / 2, cy - ps / 2, destX + magSize - cx - ps / 2, ps), cc);
        ds.FillRectangle(new Rect(cx - ps / 2, destY, ps, cy - ps / 2 - destY), cc);
        ds.FillRectangle(new Rect(cx - ps / 2, cy + ps / 2, ps, destY + magSize - cy - ps / 2), cc);
    }


    private void DrawInfoBox(CanvasDrawingSession ds, string text, Vector2 pos)
    {
        try
        {
            using var fmt = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat { FontSize = 13, FontFamily = "Consolas" };
            using var layout = new Microsoft.Graphics.Canvas.Text.CanvasTextLayout(ds, text, fmt, 400, 30);
            float w = (float)layout.LayoutBounds.Width;
            float h = (float)layout.LayoutBounds.Height;
            var bgRect = new Rect(pos.X - 3, pos.Y - 2, w + 6, h + 4);
            ds.FillRoundedRectangle(bgRect, 3, 3, Color.FromArgb(200, 0, 0, 0));
            ds.DrawRoundedRectangle(bgRect, 3, 3, Color.FromArgb(200, 128, 128, 128), 1);
            ds.DrawTextLayout(layout, pos, Colors.White);
        }
        catch { }
    }


    // ===== 鼠标事件 =====

    private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(Canvas);
        _currentMousePos = pt.Position;
        if (!pt.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_phase == OverlayPhase.LongCapturing)
        {
            return;
        }

        if (_phase == OverlayPhase.SelectionActive)
        {
            HandleEditingPressed(pt.Position, e);
            return;
        }

        _positionOnClick = pt.Position;
        _isMouseDown = true;
        _selectionFromDrag = true;
        _pressedOnHover = _hasHover;
        SelectionRect = new Rect(pt.Position.X, pt.Position.Y, 0, 0);
        e.Handled = true;
    }

    private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(Canvas).Position;
        _currentMousePos = pos;

        if (_phase == OverlayPhase.SelectionActive)
        {
            HandleEditingMoved(pos);
            e.Handled = true;
            return;
        }

        if (_phase == OverlayPhase.LongCapturing)
        {
            e.Handled = true;
            return;
        }

        if (_isMouseDown)
        {
            double x = Math.Min(_positionOnClick.X, pos.X);
            double y = Math.Min(_positionOnClick.Y, pos.Y);
            double w = Math.Abs(pos.X - _positionOnClick.X);
            double h = Math.Abs(pos.Y - _positionOnClick.Y);
            SelectionRect = new Rect(x, y, w, h);
        }
        else
        {
            UpdateHover(pos);
        }
        e.Handled = true;
    }

    private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(Canvas);

        if (pt.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonReleased)
        {
            HandleRightRelease();
            e.Handled = true;
            return;
        }

        if (_phase == OverlayPhase.SelectionActive)
        {
            if (_manipulatingSelection)
            {
                _manipulatingSelection = false;
                _activeHit = SelectionHitKind.None;
                Canvas.ReleasePointerCapture(e.Pointer);
                LayoutChrome();
            }
            else
            {
                _editor.PointerReleased();
            }

            e.Handled = true;
            return;
        }

        if (_isMouseDown)
        {
            _isMouseDown = false;
            if (SelectionRect.Width > MinimumRectangleSize && SelectionRect.Height > MinimumRectangleSize)
            {
                EnterSelectionActive(fromDrag: true);
            }
            else if (_pressedOnHover && _hoverRect.Width > 2 && _hoverRect.Height > 2)
            {
                SelectionRect = _hoverRect;
                EnterSelectionActive(fromDrag: false);
            }
            e.Handled = true;
        }
    }

    private void Canvas_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_phase != OverlayPhase.SelectionActive)
        {
            return;
        }

        if (_manipulatingSelection)
        {
            _manipulatingSelection = false;
            _activeHit = SelectionHitKind.None;
        }
        else
        {
            _editor.PointerReleased();
        }
    }

    private void UpdateHover(Point pos)
    {
        // _windowRects 为 EnumWindows 的 Z 序（顶层在前），首个命中即最上层窗口。
        // 不能选"最小矩形"，否则会高亮被遮挡的后台小窗口（同 FindSelectedWindow 语义）。
        _hasHover = false;
        foreach (var rect in _windowRects)
        {
            if (rect.Contains(pos))
            {
                _hoverRect = rect;
                _hasHover = true;
                return;
            }
        }
    }

    private void CloseWindow() => EndSession(new RegionOverlayResult { End = RegionOverlayEnd.Cancelled });

    private void EndSession(RegionOverlayResult result)
    {
        try { _renderTimer?.Stop(); } catch { }
        CancelLongCaptureInternal(restorePhase: false);
        SetExcludeFromCapture(false);
        HideChrome();
        if (result.Confirmed && result.FlattenedSdr is null)
        {
            try { result = new RegionOverlayResult { End = result.End, FlattenedSdr = FlattenSelection() }; } catch { }
        }

        SdrCrop = result.FlattenedSdr;
        _isClosed = true;
        User32.SetWindowPos(WindowHandle, IntPtr.Zero, -32000, -32000, 0, 0,
            User32.SetWindowPosFlags.SWP_NOSIZE | User32.SetWindowPosFlags.SWP_NOZORDER | User32.SetWindowPosFlags.SWP_NOACTIVATE);
        if (_prevForeground != 0 && _prevForeground != (nint)WindowHandle)
        {
            try { User32.SetForegroundWindow(new HWND(_prevForeground)); } catch { }
        }
        if (_ownsDisplayBitmap) { try { _displayBitmap?.Dispose(); } catch { } }
        _displayBitmap = null;
        _ownsDisplayBitmap = false;
        _phase = OverlayPhase.Selecting;
        Completion?.TrySetResult(result);
    }


    /// <summary>移回虚拟屏幕原位（SetCapture 后第 2 个 tick 调：新帧已合成，移回瞬间不闪旧内容）。</summary>
    private void MoveOnscreen()
    {
        int vx = User32.GetSystemMetrics((User32.SystemMetric)76);
        int vy = User32.GetSystemMetrics((User32.SystemMetric)77);
        int vw = User32.GetSystemMetrics((User32.SystemMetric)78);
        int vh = User32.GetSystemMetrics((User32.SystemMetric)79);
        User32.SetWindowPos(WindowHandle, IntPtr.Zero, vx, vy, vw, vh, User32.SetWindowPosFlags.SWP_NOZORDER);
        Activate();
    }

    private void RegionCaptureWindow_Closed(object sender, WindowEventArgs e)
    {
        Cleanup();
    }

    // 释放覆盖层资源（仅应用退出时调用：单例窗口运行期只 Hide 不 Close，进程退出才真销毁）
    public void Cleanup()
    {
        if (_cleanedUp) return;
        _cleanedUp = true;
        _isClosed = true;
        try { _renderTimer?.Stop(); } catch { }
        try { Canvas.SwapChain = null; } catch { }
        try { Canvas.RemoveFromVisualTree(); } catch { }
        try { _swapChain?.Dispose(); _swapChain = null; } catch { }
        if (_ownsDisplayBitmap) { try { _displayBitmap?.Dispose(); } catch { } }
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        bool ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        if (_phase == OverlayPhase.SelectionActive)
        {
            if (ctrl && e.Key == VirtualKey.Z)
            {
                _editor.Undo();
                e.Handled = true;
                return;
            }

            if (ctrl && (e.Key == VirtualKey.Y || e.Key == VirtualKey.R))
            {
                _editor.Redo();
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Delete && _editor.DeleteSelected())
            {
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Enter)
            {
                RequestExport(OverlayExportAction.Complete);
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Escape)
            {
                HandleEditingEscape();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == VirtualKey.Escape)
        {
            if (_phase == OverlayPhase.LongCapturing)
            {
                LongCaptureCancel_Click(this, e);
            }
            else
            {
                CloseWindow();
            }

            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter && _phase == OverlayPhase.Selecting && _hasHover && !_isMouseDown)
        {
            SelectionRect = _hoverRect;
            EnterSelectionActive(fromDrag: false);
            e.Handled = true;
        }
    }

    protected override nint WindowSubclassProc(HWND hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nint dwRefData)
    {
        if (uMsg == WmNcHitTest && _phase == OverlayPhase.LongCapturing)
        {
            int sx = unchecked((short)(lParam.ToInt64() & 0xFFFF));
            int sy = unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF));
            double x = (sx - _vx) / _scale;
            double y = (sy - _vy) / _scale;
            if (SelectionRect.Contains(new Point(x, y)))
            {
                return HtTransparent;
            }
        }

        if (uMsg == (uint)User32.WindowMessage.WM_RBUTTONUP)
        {
            HandleRightRelease();
            return 0;
        }
        return base.WindowSubclassProc(hWnd, uMsg, wParam, lParam, uIdSubclass, dwRefData);
    }

    private void EnterSelectionActive(bool fromDrag)
    {
        _selectionFromDrag = fromDrag;
        _phase = OverlayPhase.SelectionActive;
        _isMouseDown = false;
        PointerCursor.SetCursorShape(Canvas, InputSystemCursorShape.Arrow);
        ToolbarBorder.Visibility = Visibility.Visible;
        LongCaptureBar.Visibility = Visibility.Collapsed;
        ApplyToolbarVisibility();
        LayoutChrome();
        RootGrid.Focus(FocusState.Programmatic);
    }

    private void HandleEditingPressed(Point pos, PointerRoutedEventArgs e)
    {
        var point = new EditorPoint(pos.X, pos.Y);
        var hit = SelectionInteraction.HitTest(ToEditorRect(SelectionRect), point, HandleSize);
        bool canResize = hit is not SelectionHitKind.None and not SelectionHitKind.Inside;
        bool canMove = hit == SelectionHitKind.Inside && _editor.Tool == "select" &&
                       !_editor.Document.Elements.Exists(el => el.HitTest(point));

        if (canResize || canMove)
        {
            _manipulatingSelection = true;
            _activeHit = canMove ? SelectionHitKind.Inside : hit;
            _selectionAtPress = ToEditorRect(SelectionRect);
            _pointerAtPress = point;
            Canvas.CapturePointer(e.Pointer);
            e.Handled = true;
            return;
        }

        if (!SelectionRect.Contains(pos))
        {
            e.Handled = true;
            return;
        }

        var kind = _editor.PointerPressed(point);
        if (kind == OverlayAnnotationController.PressKind.RequestText)
        {
            BeginTextInput(point);
        }
        else if (kind == OverlayAnnotationController.PressKind.Capture)
        {
            Canvas.CapturePointer(e.Pointer);
        }

        e.Handled = true;
    }

    private void HandleEditingMoved(Point pos)
    {
        var point = new EditorPoint(pos.X, pos.Y);
        if (_manipulatingSelection)
        {
            var bounds = CanvasBounds();
            SelectionRect = _activeHit == SelectionHitKind.Inside
                ? ToRect(SelectionInteraction.Move(_selectionAtPress, point.X - _pointerAtPress.X, point.Y - _pointerAtPress.Y, bounds))
                : ToRect(SelectionInteraction.Resize(_selectionAtPress, _activeHit, point, bounds, MinimumRectangleSize));

            LayoutChrome();
            return;
        }

        if (_editor.IsDragging)
        {
            _editor.PointerMoved(point);
            return;
        }

        UpdateEditingCursor(point);
    }

    private void UpdateEditingCursor(EditorPoint point)
    {
        var hit = SelectionInteraction.HitTest(ToEditorRect(SelectionRect), point, HandleSize);
        InputSystemCursorShape shape = hit switch
        {
            SelectionHitKind.North or SelectionHitKind.South => InputSystemCursorShape.SizeNorthSouth,
            SelectionHitKind.East or SelectionHitKind.West => InputSystemCursorShape.SizeWestEast,
            SelectionHitKind.NorthWest or SelectionHitKind.SouthEast => InputSystemCursorShape.SizeNorthwestSoutheast,
            SelectionHitKind.NorthEast or SelectionHitKind.SouthWest => InputSystemCursorShape.SizeNortheastSouthwest,
            SelectionHitKind.Inside when _editor.Tool == "select" => InputSystemCursorShape.SizeAll,
            _ => _editor.Tool == "select" ? InputSystemCursorShape.Arrow : InputSystemCursorShape.Cross,
        };
        PointerCursor.SetCursorShape(Canvas, shape);
    }

    private void HandleRightRelease()
    {
        if (_isMouseDown)
        {
            _isMouseDown = false;
            SelectionRect = default;
            return;
        }

        if (_phase == OverlayPhase.LongCapturing)
        {
            LongCaptureCancel_Click(this, new RoutedEventArgs());
            return;
        }

        CloseWindow();
    }

    private void HandleEditingEscape()
    {
        if (TextInputBorder.Visibility == Visibility.Visible)
        {
            TextInputBorder.Visibility = Visibility.Collapsed;
            return;
        }

        if (_editor.Selected is not null)
        {
            _editor.ClearSelection();
            return;
        }

        if (_editor.Draft is not null)
        {
            _editor.CancelDraft();
            return;
        }

        CloseWindow();
    }

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tool })
        {
            _editor.Tool = tool;
            _editor.ClearSelection();
            SyncToolChrome();
        }
    }

    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        ColorChipBorder.Visibility = ColorChipBorder.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        LayoutChrome();
    }

    private void StrokeColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string color })
        {
            _editor.StrokeColor = color;
            if (TryParseHex(color, out var parsed))
            {
                ColorSwatch.Background = new SolidColorBrush(parsed);
            }
        }
    }

    private void StrokeWidth_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && double.TryParse(tag, out double width))
        {
            _editor.StrokeWidth = width;
            StrokeChipLabel.Text = $"{width:0}px";
        }
    }

    private void MosaicSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out int size))
        {
            _editor.MosaicBlockSize = size;
            MosaicChipLabel.Text = $"{size}px";
            if (_editor.Selected is MosaicElement mosaic)
            {
                mosaic.BlockSize = size;
            }
        }
    }

    private static bool TryParseHex(string hex, out Windows.UI.Color color)
    {
        color = default;
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        if (hex.Length != 8)
        {
            return false;
        }

        color = Windows.UI.Color.FromArgb(
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16),
            Convert.ToByte(hex[6..8], 16));
        return true;
    }

    private void SyncToolChrome()
    {
        var hidden = OverlayToolbarCatalog.ParseHidden(AppConfig.OverlayToolbarHidden);
        bool showColor = OverlayToolbarCatalog.IsVisible(OverlayToolbarCatalog.Color, hidden)
            && _editor.Tool is not "select" and not "";
        ColorChipBorder.Visibility = showColor ? Visibility.Visible : Visibility.Collapsed;
        LayoutChrome();
    }

    private void ApplyToolbarVisibility()
    {
        var hidden = OverlayToolbarCatalog.ParseHidden(AppConfig.OverlayToolbarHidden);
        foreach (var child in ToolbarPanel.Children)
        {
            if (child is FrameworkElement { Tag: string id } element)
            {
                element.Visibility = OverlayToolbarCatalog.IsVisible(id, hidden)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        if (!OverlayToolbarCatalog.IsVisible(_editor.Tool, hidden))
        {
            _editor.Tool = OverlayToolbarCatalog.Select;
        }
    }

    private void ResultClose_Click(object sender, RoutedEventArgs e)
    {
        ResultPanel.Visibility = Visibility.Collapsed;
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => _editor.Undo();

    private void Redo_Click(object sender, RoutedEventArgs e) => _editor.Redo();

    private void Clear_Click(object sender, RoutedEventArgs e) => _editor.Clear();

    private void Save_Click(object sender, RoutedEventArgs e) => RequestExport(OverlayExportAction.Save);

    private void Complete_Click(object sender, RoutedEventArgs e) => RequestExport(OverlayExportAction.Complete);

    private void Cancel_Click(object sender, RoutedEventArgs e) => CloseWindow();

    private async void Ocr_Click(object sender, RoutedEventArgs e) => await RunOcrAsync();

    private async Task RunOcrAsync()
    {
        // PP-OCRv6 单次约 2 秒，连点会叠加多次推理
        if (_ocrRunning)
        {
            return;
        }

        using var crop = FlattenSelection();
        if (crop is null)
        {
            return;
        }

        _ocrRunning = true;
        ShowResult(Lang.ScreenshotSetting_Ocr, Lang.Overlay_Recognizing);
        try
        {
            var result = await SharedOcr.RecognizeAsync(crop);
            _lastOcrText = result.PlainText;
            ShowResult(Lang.ScreenshotSetting_Ocr, string.IsNullOrWhiteSpace(_lastOcrText) ? Lang.Overlay_NoText : _lastOcrText);
        }
        catch (Exception ex)
        {
            ShowResult(Lang.Overlay_OcrFailed, ex.Message);
        }
        finally
        {
            _ocrRunning = false;
        }
    }

    private async void Translate_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastOcrText))
        {
            await RunOcrAsync();
        }

        if (string.IsNullOrWhiteSpace(_lastOcrText))
        {
            return;
        }

        try
        {
            var provider = new CustomApiTranslationProvider(TranslationHttp, new CustomApiTranslationSettings
            {
                BaseUrl = AppConfig.TranslationBaseUrl,
                ApiKey = SecretStorageService.Load("apiKey") ?? "",
                Model = AppConfig.TranslationModel,
                TargetLanguage = AppConfig.TranslationTargetLanguage,
                SystemPrompt = AppConfig.TranslationPrompt,
                TimeoutSeconds = AppConfig.TranslationTimeoutSeconds,
            });
            // 超时由 provider 按 TimeoutSeconds 内部计时，这里不再叠一层
            var result = await provider.TranslateAsync(new TranslationRequest
            {
                Text = _lastOcrText,
                TargetLanguage = AppConfig.TranslationTargetLanguage,
                SystemPrompt = AppConfig.TranslationPrompt,
                Model = AppConfig.TranslationModel,
            });
            ShowResult(Lang.ScreenshotSetting_Translation, result.TranslatedText);
        }
        catch (Exception ex)
        {
            ShowResult(Lang.Overlay_TranslateFailed, ex.Message);
        }
    }

    private async void LongCapture_Click(object sender, RoutedEventArgs e)
    {
        if (_phase != OverlayPhase.SelectionActive)
        {
            return;
        }

        _phase = OverlayPhase.LongCapturing;
        ToolbarBorder.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Collapsed;
        LongCaptureBar.Visibility = Visibility.Visible;
        LongCaptureStatus.Text = Lang.Overlay_ScrollToContinue;
        LayoutChrome();
        SetExcludeFromCapture(true);

        _longCts = new CancellationTokenSource();
        _longDecision = new TaskCompletionSource<bool>();
        var region = GetPhysicalSourceRect();
        try
        {
            var runner = new LongCaptureRunner();
            var image = await runner.RunAsync(
                region,
                AppConfig.LongCaptureMaxHeight,
                status => LongCaptureStatus.Text = status,
                _longDecision.Task,
                _longCts.Token);
            if (image is null)
            {
                RestoreAfterLongCapture();
                return;
            }

            EndSession(new RegionOverlayResult { End = RegionOverlayEnd.LongCapture, FlattenedSdr = image });
        }
        catch (Exception)
        {
            HintText.Text = Lang.Overlay_LongCaptureFailed;
            HintText.Visibility = Visibility.Visible;
            RestoreAfterLongCapture();
        }
    }

    private void LongCaptureFinish_Click(object sender, RoutedEventArgs e)
        => _longDecision?.TrySetResult(true);

    private void LongCaptureCancel_Click(object sender, RoutedEventArgs e)
    {
        _longDecision?.TrySetResult(false);
        _longCts?.Cancel();
    }

    private void RestoreAfterLongCapture()
    {
        CancelLongCaptureInternal(restorePhase: true);
        SetExcludeFromCapture(false);
        ToolbarBorder.Visibility = Visibility.Visible;
        LongCaptureBar.Visibility = Visibility.Collapsed;
        ApplyToolbarVisibility();
        LayoutChrome();
    }

    private void CancelLongCaptureInternal(bool restorePhase)
    {
        _longCts?.Cancel();
        _longDecision?.TrySetResult(false);
        _longCts = null;
        _longDecision = null;
        if (restorePhase)
        {
            _phase = OverlayPhase.SelectionActive;
        }
    }

    private void RequestExport(OverlayExportAction action)
    {
        if (_copyOnlySession && action == OverlayExportAction.Complete)
        {
            action = OverlayExportAction.CopyOnly;
        }

        var decision = ScreenshotSavePolicy.ResolveOverlayExport(
            action,
            AppConfig.AutoSaveScreenshotToFile,
            AppConfig.AutoCopyScreenshotToClipboard);
        if (!decision.CanEndSession)
        {
            HintText.Text = Lang.Overlay_SaveOrCopyFirst;
            HintText.Visibility = Visibility.Visible;
            return;
        }

        HintText.Visibility = Visibility.Collapsed;
        var end = action switch
        {
            OverlayExportAction.CopyOnly => RegionOverlayEnd.CopyOnly,
            OverlayExportAction.Save => RegionOverlayEnd.Save,
            _ => RegionOverlayEnd.Complete,
        };
        EndSession(new RegionOverlayResult { End = end, FlattenedSdr = FlattenSelection() });
    }

    private void BeginTextInput(EditorPoint point)
    {
        _textPoint = point;
        TextInputBox.Text = "";
        TextInputBorder.Visibility = Visibility.Visible;
        Microsoft.UI.Xaml.Controls.Canvas.SetLeft(TextInputBorder, point.X);
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(TextInputBorder, point.Y);
        TextInputBox.Focus(FocusState.Programmatic);
    }

    private void TextInputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            _editor.AddText(_textPoint, TextInputBox.Text);
            TextInputBorder.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            TextInputBorder.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
    }

    private void ShowResult(string title, string text)
    {
        ResultTitle.Text = title;
        ResultText.Text = text;
        ResultPanel.Visibility = Visibility.Visible;
        LayoutChrome();
    }

    private void ResultCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ResultText.Text))
        {
            ClipboardHelper.SetText(ResultText.Text);
        }
    }

    private void LayoutChrome()
    {
        if (_phase is not OverlayPhase.SelectionActive and not OverlayPhase.LongCapturing)
        {
            return;
        }

        FrameworkElement bar = _phase == OverlayPhase.LongCapturing ? LongCaptureBar : ToolbarBorder;
        bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        GetActiveMonitorDip((float)(SelectionRect.X + SelectionRect.Width / 2), (float)(SelectionRect.Y + SelectionRect.Height / 2),
            out float ml, out float mt, out float mr, out float mb);
        var pos = ToolbarAnchor.Place(
            ToEditorRect(SelectionRect),
            bar.DesiredSize.Width,
            bar.DesiredSize.Height,
            new EditorRect(ml, mt, mr - ml, mb - mt),
            8);
        Microsoft.UI.Xaml.Controls.Canvas.SetLeft(bar, pos.X);
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(bar, pos.Y);

        if (ColorChipBorder.Visibility == Visibility.Visible)
        {
            ColorChipBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double chipX = pos.X - ColorChipBorder.DesiredSize.Width - 8;
            if (chipX < ml)
            {
                chipX = pos.X;
            }

            Microsoft.UI.Xaml.Controls.Canvas.SetLeft(ColorChipBorder, chipX);
            Microsoft.UI.Xaml.Controls.Canvas.SetTop(ColorChipBorder, pos.Y - ColorChipBorder.DesiredSize.Height - 8);
        }

        if (ResultPanel.Visibility == Visibility.Visible)
        {
            ResultPanel.Measure(new Size(280, 280));
            double rx = SelectionRect.X + SelectionRect.Width + 8;
            if (rx + 280 > mr)
            {
                rx = SelectionRect.X - 288;
            }

            rx = Math.Clamp(rx, ml, Math.Max(ml, mr - 280));
            double ry = Math.Clamp(SelectionRect.Y, mt, Math.Max(mt, mb - 120));
            Microsoft.UI.Xaml.Controls.Canvas.SetLeft(ResultPanel, rx);
            Microsoft.UI.Xaml.Controls.Canvas.SetTop(ResultPanel, ry);
        }
    }

    private void HideChrome()
    {
        ToolbarBorder.Visibility = Visibility.Collapsed;
        LongCaptureBar.Visibility = Visibility.Collapsed;
        TextInputBorder.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Collapsed;
        ColorChipBorder.Visibility = Visibility.Collapsed;
        HintText.Visibility = Visibility.Collapsed;
    }

    private void HighlightRect(CanvasDrawingSession ds, Rect rect, float physW, float physH)
    {
        double cx = Math.Max(rect.X, 0);
        double cy = Math.Max(rect.Y, 0);
        double cw = Math.Max(0, Math.Min(rect.X + rect.Width, _lockedW) - cx);
        double ch = Math.Max(0, Math.Min(rect.Y + rect.Height, _lockedH) - cy);
        var clip = new Rect(cx, cy, cw, ch);
        if (clip.Width > 0 && clip.Height > 0 && _displayBitmap is not null)
        {
            ds.DrawImage(_displayBitmap,
                clip,
                new Rect(clip.X / _lockedW * physW, clip.Y / _lockedH * physH,
                         clip.Width / _lockedW * physW, clip.Height / _lockedH * physH),
                1f, CanvasImageInterpolation.Linear);
        }
    }

    private void FillDimOutside(CanvasDrawingSession ds, Rect hole)
    {
        var dim = Color.FromArgb(102, 0, 0, 0);
        ds.FillRectangle(new Rect(0, 0, _lockedW, Math.Max(0, hole.Y)), dim);
        ds.FillRectangle(new Rect(0, hole.Y + hole.Height, _lockedW, Math.Max(0, _lockedH - hole.Y - hole.Height)), dim);
        ds.FillRectangle(new Rect(0, hole.Y, Math.Max(0, hole.X), hole.Height), dim);
        ds.FillRectangle(new Rect(hole.X + hole.Width, hole.Y, Math.Max(0, _lockedW - hole.X - hole.Width), hole.Height), dim);
    }

    private void DrawHandles(CanvasDrawingSession ds, Rect rect)
    {
        DrawHandle(ds, rect.X, rect.Y);
        DrawHandle(ds, rect.X + rect.Width, rect.Y);
        DrawHandle(ds, rect.X, rect.Y + rect.Height);
        DrawHandle(ds, rect.X + rect.Width, rect.Y + rect.Height);
        DrawHandle(ds, rect.X + rect.Width / 2, rect.Y);
        DrawHandle(ds, rect.X + rect.Width / 2, rect.Y + rect.Height);
        DrawHandle(ds, rect.X, rect.Y + rect.Height / 2);
        DrawHandle(ds, rect.X + rect.Width, rect.Y + rect.Height / 2);
    }

    private static void DrawHandle(CanvasDrawingSession ds, double cx, double cy)
    {
        float s = (float)HandleSize;
        var r = new Rect(cx - s / 2, cy - s / 2, s, s);
        ds.FillRectangle(r, Colors.White);
        ds.DrawRectangle(r, Colors.Black, 1);
    }

    private CanvasRenderTarget? FlattenSelection()
    {
        if (_displayBitmap is null || SelectionRect.Width < 2 || SelectionRect.Height < 2)
        {
            return null;
        }

        var src = GetPhysicalSourceRect();
        int w = Math.Max(1, (int)src.Width);
        int h = Math.Max(1, (int)src.Height);
        var rt = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), w, h, 96, DirectXPixelFormat.B8G8R8A8UIntNormalized, CanvasAlphaMode.Premultiplied);
        float ratioX = _displayBitmap.SizeInPixels.Width / _lockedW;
        float ratioY = _displayBitmap.SizeInPixels.Height / _lockedH;
        using (var ds = rt.CreateDrawingSession())
        {
            ds.DrawImage(_displayBitmap, new Rect(0, 0, w, h), src, 1f, CanvasImageInterpolation.Linear);
        }

        using (var ds = rt.CreateDrawingSession())
        {
            ds.Transform = Matrix3x2.CreateScale(ratioX, ratioY) * Matrix3x2.CreateTranslation(-(float)src.X, -(float)src.Y);
            foreach (var element in _editor.Document.Elements)
            {
                EditorRenderer.Draw(ds, element, _displayBitmap, ratioX, ratioY);
            }
        }

        return rt;
    }

    private void SetExcludeFromCapture(bool exclude)
    {
        try { SetWindowDisplayAffinity(WindowHandle, exclude ? WdaExcludeFromCapture : WdaNone); } catch { }
    }

    private EditorRect CanvasBounds() => new(0, 0, _lockedW, _lockedH);

    private static EditorRect ToEditorRect(Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    private static Rect ToRect(EditorRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    public Rect GetPhysicalSourceRect()
    {
        return ComputePhysicalRect(SelectionRect, _selectionFromDrag);
    }

    // 鼠标框选时两端是光标像素索引（含端点），宽 = |x2-x1| + 1（CreateRectangle）；
    // 窗口矩形本身就是正常尺寸，不 +1。WinUI 指针是 DIP，先 round 成物理像素索引。
    private Rect ComputePhysicalRect(Rect dipRect, bool fromDrag)
    {
        double ratioX = _canvasOriginal.SizeInPixels.Width / _lockedW;
        double ratioY = _canvasOriginal.SizeInPixels.Height / _lockedH;

        int x1 = (int)Math.Round(dipRect.X * ratioX);
        int y1 = (int)Math.Round(dipRect.Y * ratioY);
        int x2 = (int)Math.Round((dipRect.X + dipRect.Width) * ratioX);
        int y2 = (int)Math.Round((dipRect.Y + dipRect.Height) * ratioY);

        int x = Math.Min(x1, x2);
        int y = Math.Min(y1, y2);
        int physW = (int)_canvasOriginal.SizeInPixels.Width;
        int physH = (int)_canvasOriginal.SizeInPixels.Height;
        int w = Math.Abs(x2 - x1) + (fromDrag ? 1 : 0);
        int h = Math.Abs(y2 - y1) + (fromDrag ? 1 : 0);
        // 选区/hover 经 ratio 缩放 + round 后可能落在画布物理边界外（边缘 round 把 x2/y2 顶到 physW/physH+1），
        // physW-x / physH-y 此时会为负，必须 clamp 到 0，否则 new Rect 负宽高抛 ArgumentOutOfRangeException
        w = Math.Max(0, Math.Min(w, physW - x));
        h = Math.Max(0, Math.Min(h, physH - y));
        return new Rect(x, y, w, h);
    }

}
