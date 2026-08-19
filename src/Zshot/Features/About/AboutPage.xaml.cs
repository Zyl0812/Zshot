using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using Serilog;
using Zshot.Features.Update;
using Zshot.Frameworks;
using Zshot.Helpers;
using Zshot.Language;
using System;
using System.Threading.Tasks;
using Windows.System;

namespace Zshot.Features.About;

public sealed partial class AboutPage : PageBase
{

    public string Version { get; set; } =
#if DEBUG
        "Debug";
#else
        $"Release {AppConfig.AppVersion}";
#endif


    /// <summary>
    /// 检查更新时是否包含预发布版本（代理 AppConfig.EnablePreReleaseUpdateCheck）。
    /// </summary>
    public bool PreReleaseCheck
    {
        get => AppConfig.EnablePreReleaseUpdateCheck;
        set => AppConfig.EnablePreReleaseUpdateCheck = value;
    }


    public AboutPage()
    {
        InitializeComponent();
#if DEBUG
        // DEBUG 不查更新，隐藏按钮和更新相关开关（CheckUpdateAsync 直接 return null，显示「最新」是假的）
        CheckUpdateButton.Visibility = Visibility.Collapsed;
        PreReleaseSwitch.Visibility = Visibility.Collapsed;
#endif
    }


    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }


    [RelayCommand]
    private async Task CheckUpdate()
    {
        try
        {
            var (release, tag) = await UpdateService.CheckUpdateAsync(ignoreSkipped: false);
            if (release is null)
            {
                // 显示 GitHub 最新版号（不是当前版本号——当前可能比 GitHub 还新）
                var t = tag ?? AppConfig.AppVersion;
                InAppToast.MainWindow?.Information(null, Lang.Zshot_LatestVersion, 3000, t);
                return;
            }
            var window = new UpdateWindow();
            window.SetRelease(release);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AboutPage.CheckUpdate failed");
            InAppToast.MainWindow?.Error(ex, Lang.Zshot_UpdateFailed);
        }
    }

}
