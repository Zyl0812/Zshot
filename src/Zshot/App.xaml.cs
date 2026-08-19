using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AppLifecycle;
using Serilog;
using Zshot.Features.ViewHost;
using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Windows.UI;

namespace Zshot;

public partial class App : Application
{

    private readonly DispatcherQueue _uiDispatcherQueue;

    private readonly Timer _gcTimer = new(TimeSpan.FromSeconds(60));

    public static new App Current => (App)Application.Current;


    public App()
    {
        this.InitializeComponent();
        _uiDispatcherQueue = DispatcherQueue.GetForCurrentThread();
        UnhandledException += App_UnhandledException;
        // 后台定时 GC：截图（尤其区域截图覆盖层）残留的 RCW/未引用资源靠 GC 回收，
        // GC 看托管堆不看显存，不主动 Collect 会累积占显存。每 60s 回收一次（参考 Starward，且补上它漏掉的 Start）。
        _gcTimer.Elapsed += (_, _) => GC.Collect();
        _gcTimer.Start();
    }


    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "App Crash");
    }


    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs _)
    {
        await AppConfig.CheckEnviromentAsync();

        instance = AppInstance.GetCurrent();
        instance.Activated += AppInstance_Activated;

        var main = AppInstance.FindOrRegisterForKey("main");
        if (!main.IsCurrent)
        {
            await main.RedirectActivationToAsync(instance.GetActivatedEventArgs());
            Environment.Exit(0);
        }

        // 主实例：检测自启项指向的 exe 是否存在，不存在则清除
        AppConfig.CheckAutoStartValidity();
        AppConfig.CheckTaskValidity();

        // PixPin 式：启动只挂托盘，不创建主窗口。
        EnsureSystemTray();
    }



    private AppInstance instance;

    private MainWindow? m_MainWindow = null;

    /// <summary>
    /// 主窗口引用（供设置页等调用 ApplyTheme）。托盘优先后默认为 null。
    /// </summary>
    public MainWindow? MainWindow => m_MainWindow;

    private SystemTrayWindow? m_SystemTrayWindow;

    private SettingsWindow? m_SettingsWindow;



    public void EnsureSystemTray()
    {
        if (AppConfig.EnableSystemTrayIcon && m_SystemTrayWindow is null)
        {
            m_SystemTrayWindow = new SystemTrayWindow();
        }
    }



    public void EnsureSettingsWindow()
    {
        m_SettingsWindow ??= new SettingsWindow();
        m_SettingsWindow.Activate();
        m_SettingsWindow.Show();
    }



    public void EnsureMainWindow()
    {
        EnsureSettingsWindow();
    }



    private void AppInstance_Activated(object? sender, AppActivationArguments e)
    {
        _uiDispatcherQueue.TryEnqueue(() =>
        {
            Features.Screenshot.ScreenCaptureService.CaptureRegion();
        });
    }



    public new void Exit()
    {
        if (m_MainWindow is not null)
        {
            m_MainWindow.ForceExit = true;
        }
        if (m_SettingsWindow is not null)
        {
            m_SettingsWindow.ForceExit = true;
        }
        m_SystemTrayWindow?.Close();
        m_SettingsWindow?.Close();
        m_MainWindow?.Close();
        Application.Current.Exit();
    }



}
