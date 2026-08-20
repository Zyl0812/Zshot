using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Zshot.Features.Screenshot;
using Zshot.Frameworks;
using Zshot.Helpers;
using Zshot.Language;
using Windows.System;

namespace Zshot.Features.Setting;

public sealed partial class ScreenshotSetting : PageBase
{


    public int ScreenshotSDRFormat
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.ScreenCaptureSDRFormat = value;
            }
        }
    } = AppConfig.ScreenCaptureSDRFormat;


    public int ScreenshotHDRFormat
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.ScreenCaptureHDRFormat = value;
            }
        }
    } = AppConfig.ScreenCaptureHDRFormat;


    public int ScreenshotQuality
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.ScreenCaptureEncodeQuality = value;
            }
        }
    } = AppConfig.ScreenCaptureEncodeQuality;


    private bool _enableColorManagement = AppConfig.EnableScreenshotColorManagement;
    public bool EnableScreenshotColorManagement
    {
        get => _enableColorManagement;
        set
        {
            if (value && !_enableColorManagement)
            {
                // 打开前先校验主显示器 primaries；畸形（VM/无 ICC）则弹 Error 并弹回关，避免截图编码时 lcms2 崩溃
                _ = TryEnableColorManagementAsync();
                return;
            }
            if (SetProperty(ref _enableColorManagement, value))
            {
                AppConfig.EnableScreenshotColorManagement = value;
            }
        }
    }


    private async Task TryEnableColorManagementAsync()
    {
        bool ok = await ScreenCaptureService.CanEnableColorManagementAsync();
        if (ok)
        {
            _enableColorManagement = true;
            AppConfig.EnableScreenshotColorManagement = true;
            OnPropertyChanged(nameof(EnableScreenshotColorManagement));
        }
        else
        {
            InAppToast.MainWindow?.Error((string?)null, Lang.Zshot_ColorManagementUnavailable, 7000);
            OnPropertyChanged(nameof(EnableScreenshotColorManagement));  // 刷新绑定，UI 弹回关
        }
    }


    public bool AutoConvertScreenshotToSDR
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.AutoConvertScreenshotToSDR = value;
            }
        }
    } = AppConfig.AutoConvertScreenshotToSDR;


    public bool DeleteHDRIfSDRContent
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.DeleteHDRIfSDRContent = value;
            }
        }
    } = AppConfig.DeleteHDRIfSDRContent;


    public bool AutoCopyScreenshotToClipboard
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.AutoCopyScreenshotToClipboard = value;
            }
        }
    } = AppConfig.AutoCopyScreenshotToClipboard;


    public bool AutoSaveScreenshotToFile
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.AutoSaveScreenshotToFile = value;
            }
        }
    } = AppConfig.AutoSaveScreenshotToFile;


    public string ScreenshotFolderPath { get; set => SetProperty(ref field, value); } = AppConfig.ScreenshotFolder ?? "";


    [RelayCommand]
    private async Task ChangeScreenshotFolder()
    {
        string? folder = await FileDialogHelper.PickFolderAsync(this.XamlRoot);
        if (Directory.Exists(folder))
        {
            ScreenshotFolderPath = folder;
            AppConfig.ScreenshotFolder = folder;
        }
    }


    [RelayCommand]
    private async Task OpenScreenshotFolder()
    {
        if (Directory.Exists(ScreenshotFolderPath))
        {
            await Launcher.LaunchFolderPathAsync(ScreenshotFolderPath);
        }
    }


    public int CaptureMonitorSource
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.ScreenshotCaptureMonitorSource = value;
            }
        }
    } = AppConfig.ScreenshotCaptureMonitorSource;


    public double TranslationTimeoutSeconds
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.TranslationTimeoutSeconds = (int)value;
            }
        }
    } = AppConfig.TranslationTimeoutSeconds;


    public string TranslationBaseUrl
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.TranslationBaseUrl = value;
            }
        }
    } = AppConfig.TranslationBaseUrl;


    public string TranslationModel
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.TranslationModel = value;
            }
        }
    } = AppConfig.TranslationModel;


    public string TranslationTargetLanguage
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.TranslationTargetLanguage = value;
            }
        }
    } = AppConfig.TranslationTargetLanguage;


    public string TranslationPrompt
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.TranslationPrompt = value;
            }
        }
    } = AppConfig.TranslationPrompt;


    public double LongCaptureMaxHeight
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.LongCaptureMaxHeight = (int)value;
            }
        }
    } = AppConfig.LongCaptureMaxHeight;


    public ScreenshotSetting()
    {
        InitializeComponent();
        string? key = SecretStorageService.Load("apiKey");
        if (!string.IsNullOrEmpty(key))
        {
            ApiKeyBox.Password = key;
        }
    }


    private void ApiKeyBox_LostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SecretStorageService.Save("apiKey", ApiKeyBox.Password);
    }


}
