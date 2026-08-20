using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Zshot.Features.Codec;
using Zshot.Frameworks;
using Zshot.Helpers;
using Zshot.Language;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Win32;
using TaskService = Microsoft.Win32.TaskScheduler.TaskService;
using Windows.System;

namespace Zshot.Features.Setting;

public sealed partial class GeneralSetting : PageBase
{

    private readonly ILogger<GeneralSetting> _logger = AppConfig.GetLogger<GeneralSetting>();


    public GeneralSetting()
    {
        InitializeComponent();
        LoadShieldIcon();
    }



    #region Language


    public int LanguageIndex
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                string? lang = value switch { 1 => "en-US", 2 => "zh-CN", 3 => "ja-JP", _ => null };
                AppConfig.Language = lang;
                AppConfig.SetLanguage(lang);
                Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true });
                Environment.Exit(0);
            }
        }
    } = AppConfig.Language switch { "en-US" => 1, "zh-CN" => 2, "ja-JP" => 3, _ => 0 };


    #endregion



    #region Auto Start


    public bool EnableAutoStart
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) _ = ApplyEnableAutoStartAsync(value);
        }
    } = IsAutoStartActive();


    private async Task ApplyEnableAutoStartAsync(bool value)
    {
        if (value)
        {
            if (PriorityStart) await UpdateAutoStartTaskAsync(true);
            else UpdateAutoStartRegistry(true);
        }
        else
        {
            UpdateAutoStartRegistry(false);
            if (_priorityStart)
            {
                if (await UpdateAutoStartTaskAsync(false)) _priorityStart = false;
            }
        }
        OnPropertyChanged(nameof(PriorityStartVisibility));
        OnPropertyChanged(nameof(PriorityStart));
    }


    private static bool IsAutoStartActive()
    {
        if (AppConfig.EnableAutoStart) return true;
        try { using var ts = new TaskService(); return ts.GetTask("Zshot") is not null; }
        catch { return false; }
    }


    public Microsoft.UI.Xaml.Media.ImageSource? ShieldSource { get; set; }

    public Visibility PriorityStartVisibility => EnableAutoStart ? Visibility.Visible : Visibility.Collapsed;

    public bool AutoStartEnabled => !PriorityStart;


    /// <summary>
    /// Task Scheduler 高优先级启动（ONLOGON + High），独立于注册表 Run
    /// </summary>
    private bool _priorityStart = IsTaskExists();
    public bool PriorityStart
    {
        get => _priorityStart;
        set
        {
            _priorityStart = value;
            OnPropertyChanged(nameof(AutoStartEnabled));
            OnPropertyChanged(nameof(PriorityStartHintVisibility));
            _ = ApplyPriorityStartAsync(value);
        }
    }


    private async Task ApplyPriorityStartAsync(bool value)
    {
        bool ok = await UpdateAutoStartTaskAsync(value);
        if (ok)
        {
            UpdateAutoStartRegistry(!value);
        }
        else
        {
            _priorityStart = !value;
            OnPropertyChanged(nameof(PriorityStart));
            OnPropertyChanged(nameof(AutoStartEnabled));
            OnPropertyChanged(nameof(PriorityStartHintVisibility));
        }
    }


    public Visibility PriorityStartHintVisibility => _priorityStart ? Visibility.Visible : Visibility.Collapsed;


    private static bool IsTaskExists()
    {
        try { using var ts = new TaskService(); return ts.GetTask("Zshot") is not null; }
        catch { return false; }
    }


    private static readonly string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Zshot";


    private void UpdateAutoStartRegistry(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null) return;

            if (enable)
            {
                // 启动永远静默挂托盘，命令行不带参数
                key.SetValue(RunValueName, $"\"{GetLauncherPath()}\"");
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update auto-start registry");
        }
    }


    /// <summary>
    /// Task Scheduler 高优先级启动（ONLOGON 触发 + High 优先级），独立于注册表 Run。
    /// </summary>
    /// <returns>true 成功（ExitCode 0）；false 失败（子进程异常或非 0 退出）</returns>
    private async Task<bool> UpdateAutoStartTaskAsync(bool enable)
    {
        try
        {
            string launcherPath = GetLauncherPath();
            string mode = enable ? "create" : "delete";
            // 提权子进程：UAC 弹窗，admin 权限调 TaskScheduler API（同步）；await 不阻塞 UI
            var psi = new ProcessStartInfo(Environment.ProcessPath!, $"--manage-task {mode} \"{launcherPath}\" \"\"")
            {
                Verb = "runas",
                UseShellExecute = true,
            };
            var p = Process.Start(psi);
            if (p is null) return false;
            await p.WaitForExitAsync();
            return p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogError(ex, "Failed to update auto-start task");
            InAppToast.MainWindow?.Warning(null, new System.ComponentModel.Win32Exception(ex.NativeErrorCode).Message, 5000);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update auto-start task");
            InAppToast.MainWindow?.Warning(null, ex.Message, 5000);
            return false;
        }
    }


    private static string GetLauncherPath()
    {
        string exePath = Environment.ProcessPath ?? "";
        string appDir = Path.GetDirectoryName(exePath) ?? "";
        string rootDir = Path.GetDirectoryName(appDir) ?? "";
        string launcher = Path.Combine(rootDir, "Zshot.exe");
        return File.Exists(launcher) ? launcher : exePath;
    }


    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);


    private void LoadShieldIcon()
    {
        try
        {
            // IDI_SHIELD = 32518，系统标准 UAC 盾牌图标
            IntPtr hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32518);
            if (hIcon == IntPtr.Zero) return;
            using var icon = System.Drawing.Icon.FromHandle(hIcon);
            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            bitmap.SetSource(ms.AsRandomAccessStream());
            ShieldSource = bitmap;
        }
        catch { }
    }


    #endregion



    #region Maintenance


    public string DataFolder => AppConfig.UserDataFolder;

    public string LogFolder => Path.Combine(AppConfig.LogFolder, "log");


    [RelayCommand]
    private async Task OpenDataFolder()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(DataFolder))
            {
                await Launcher.LaunchFolderPathAsync(DataFolder);
            }
        }
        catch { }
    }


    [RelayCommand]
    private async Task OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(LogFolder);
            await Launcher.LaunchFolderPathAsync(LogFolder);
        }
        catch { }
    }


    [RelayCommand]
    private void ClearCache()
    {
        try
        {
            ImageThumbnail.ClearThumbnailCache();
            InAppToast.MainWindow?.Success(Lang.ScreenshotSetting_ClearSuccessfully);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cache");
            InAppToast.MainWindow?.Error(ex, Lang.ScreenshotSetting_ClearFailed);
        }
    }


    #endregion

}
