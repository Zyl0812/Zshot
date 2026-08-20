using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Zshot.Core.Overlay;
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


    public ObservableCollection<OverlayToolbarItemOption> ToolbarItems { get; } = [];


    public ScreenshotSetting()
    {
        var hidden = OverlayToolbarCatalog.ParseHidden(AppConfig.OverlayToolbarHidden);
        foreach (var id in OverlayToolbarCatalog.Customizable)
        {
            var item = new OverlayToolbarItemOption(id, LabelFor(id), !hidden.Contains(id));
            item.PropertyChanged += OverlayToolbarItem_PropertyChanged;
            ToolbarItems.Add(item);
        }

        InitializeComponent();
        string? key = SecretStorageService.Load("apiKey");
        if (!string.IsNullOrEmpty(key))
        {
            ApiKeyBox.Password = key;
        }
    }


    private void OverlayToolbarItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OverlayToolbarItemOption.IsVisible))
        {
            return;
        }

        AppConfig.OverlayToolbarHidden = OverlayToolbarCatalog.SerializeHidden(
            ToolbarItems.Where(item => !item.IsVisible).Select(item => item.Id));
    }


    private static string LabelFor(string id) => id switch
    {
        OverlayToolbarCatalog.Color => Lang.Overlay_Color,
        OverlayToolbarCatalog.Select => Lang.Overlay_Select,
        OverlayToolbarCatalog.Rect => Lang.Overlay_Rect,
        OverlayToolbarCatalog.Ellipse => Lang.Overlay_Ellipse,
        OverlayToolbarCatalog.Line => Lang.Overlay_Line,
        OverlayToolbarCatalog.Arrow => Lang.Overlay_Arrow,
        OverlayToolbarCatalog.Pen => Lang.Overlay_Pen,
        OverlayToolbarCatalog.Text => Lang.Overlay_Text,
        OverlayToolbarCatalog.Mosaic => Lang.Overlay_Mosaic,
        OverlayToolbarCatalog.Number => Lang.Overlay_Number,
        OverlayToolbarCatalog.Undo => Lang.Overlay_Undo,
        OverlayToolbarCatalog.Redo => Lang.Overlay_Redo,
        OverlayToolbarCatalog.Clear => Lang.Overlay_Clear,
        OverlayToolbarCatalog.Ocr => Lang.Overlay_Ocr,
        OverlayToolbarCatalog.Translate => Lang.Overlay_Translate,
        OverlayToolbarCatalog.LongCapture => Lang.Overlay_LongCapture,
        OverlayToolbarCatalog.Save => Lang.Overlay_Save,
        _ => id,
    } ?? id;


    private void ApiKeyBox_LostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SecretStorageService.Save("apiKey", ApiKeyBox.Password);
    }


}

public sealed class OverlayToolbarItemOption : ObservableObject
{
    private bool _isVisible;

    public OverlayToolbarItemOption(string id, string label, bool isVisible)
    {
        Id = id;
        Label = label;
        _isVisible = isVisible;
    }

    public string Id { get; }

    public string Label { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}
