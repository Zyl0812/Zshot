using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Zshot.Features.About;
using Zshot.Features.Setting;
using Zshot.Frameworks;
using Zshot.Helpers;
using Windows.Graphics;

namespace Zshot.Features.ViewHost;

public sealed partial class SettingsWindow : WindowEx
{
    public bool ForceExit;

    public SettingsWindow()
    {
        InitializeComponent();
        WindowEx.MainWindowId = AppWindow.Id;
        Title = "Zshot";
        SetIcon();
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AdaptTitleBarButtonColorToActuallTheme();
        SetDragRectangles(new RectInt32(0, 0, 100000, (int)(48 * UIScale)));
        CenterInScreen(720, 620);
        new SystemBackdropHelper(this).TrySetMica();
        AppWindow.Closing += AppWindow_Closing;
        ((FrameworkElement)Content).Loaded += SettingsWindow_Loaded;
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ((FrameworkElement)sender).Loaded -= SettingsWindow_Loaded;
        InAppToast.FlushPending();
        HotkeyManager.ShowRegistrationErrors();
        if (NavView.MenuItems.Count > 0)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            var pageType = tag switch
            {
                "Screenshot" => typeof(ScreenshotSetting),
                "Format" => typeof(FormatSetting),
                "Ocr" => typeof(OcrSetting),
                "Hotkey" => typeof(HotkeySetting),
                "General" => typeof(GeneralSetting),
                "About" => typeof(AboutPage),
                _ => typeof(ScreenshotSetting),
            };
            ContentFrame.Navigate(pageType);
        }
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (ForceExit)
        {
            return;
        }
        args.Cancel = true;
        Hide();
    }
}
