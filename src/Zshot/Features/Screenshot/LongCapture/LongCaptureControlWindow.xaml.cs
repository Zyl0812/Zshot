using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Zshot.Frameworks;
using System.Threading.Tasks;
using Windows.Graphics;

namespace Zshot.Features.Screenshot.LongCapture;

public sealed partial class LongCaptureControlWindow : WindowEx
{
    private readonly TaskCompletionSource<bool> _tcs = new();
    private bool _forceClose;

    public LongCaptureControlWindow()
    {
        InitializeComponent();
        SetIcon();
        CenterInScreen(320, 140);
        AppWindow.IsShownInSwitchers = false;
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.IsAlwaysOnTop = true;
            p.IsResizable = false;
            p.IsMaximizable = false;
        }

        AppWindow.Closing += (_, e) =>
        {
            if (_forceClose)
            {
                return;
            }

            e.Cancel = true;
            Complete(false);
        };
    }

    public Task<bool> WaitAsync()
    {
        Activate();
        return _tcs.Task;
    }

    public void SetStatus(string text) => StatusText.Text = text;

    private void Finish_Click(object sender, RoutedEventArgs e) => Complete(true);

    private void Cancel_Click(object sender, RoutedEventArgs e) => Complete(false);

    private void Complete(bool ok)
    {
        if (_tcs.Task.IsCompleted)
        {
            return;
        }

        _forceClose = true;
        _tcs.TrySetResult(ok);
        Close();
    }
}
