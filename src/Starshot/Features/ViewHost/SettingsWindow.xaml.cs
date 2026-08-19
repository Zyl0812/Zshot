using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Starshot.Features.About;
using Starshot.Features.Setting;
using Starshot.Frameworks;
using Starshot.Helpers;
using Windows.Graphics;

namespace Starshot.Features.ViewHost;

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
        CenterInScreen(960, 720);
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
                "Hotkey" => typeof(HotkeySetting),
                "Storage" => typeof(StorageSetting),
                "Settings" => typeof(GeneralSetting),
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
