using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Zshot.Core.Overlay;
using Zshot.Features.Screenshot;
using Zshot.Frameworks;
using Zshot.Helpers;
using Zshot.Language;
using Windows.System;

namespace Zshot.Features.Setting;

public sealed partial class ScreenshotSetting : PageBase
{

    private TextBox? _lastFocusedTemplateBox;

    private static readonly string[] _tokens =
    {
        "process", "processPath", "title", "timestamp", "time", "date",
        "year", "month", "day", "hour", "minute", "second", "width", "height",
    };


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
        _lastFocusedTemplateBox = FileNameTextBox;
        BuildPlaceholderLinks();
        UpdateHiddenCount();
    }



    #region Overlay Toolbar


    public ObservableCollection<OverlayToolbarItemOption> ToolbarItems { get; } = [];


    public string HiddenCountText { get; set => SetProperty(ref field, value); } = "";


    private void OverlayToolbarItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OverlayToolbarItemOption.IsVisible))
        {
            return;
        }

        AppConfig.OverlayToolbarHidden = OverlayToolbarCatalog.SerializeHidden(
            ToolbarItems.Where(item => !item.IsVisible).Select(item => item.Id));
        UpdateHiddenCount();
    }


    private void UpdateHiddenCount()
    {
        int hidden = ToolbarItems.Count(item => !item.IsVisible);
        HiddenCountText = hidden == 0 ? Lang.ScreenshotSetting_ToolbarNoneHidden : string.Format(Lang.ScreenshotSetting_ToolbarHiddenCount, hidden);
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


    #endregion



    #region Save


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


    public string ScreenshotFolderPath { get; set => SetProperty(ref field, value); } = ResolveScreenshotFolder();


    private static string ResolveScreenshotFolder()
    {
        string? folder = AppConfig.ScreenshotFolder;
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = Path.Join(AppConfig.LogFolder, "Screenshots");
        }
        try { Directory.CreateDirectory(folder); } catch { }
        return folder;
    }


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


    #endregion



    #region File Name Template


    public string FileNamePattern
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.ScreenshotFileNamePattern = value;
                FileNamePreview = BuildPreview(value);
            }
        }
    } = AppConfig.ScreenshotFileNamePattern;


    public string FileNamePreview { get; set => SetProperty(ref field, value); } = BuildPreview(AppConfig.ScreenshotFileNamePattern);


    public string RegionFileNamePattern
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.RegionScreenshotFileNamePattern = value;
                RegionFileNamePreview = BuildPreview(value);
            }
        }
    } = AppConfig.RegionScreenshotFileNamePattern;


    public string RegionFileNamePreview { get; set => SetProperty(ref field, value); } = BuildPreview(AppConfig.RegionScreenshotFileNamePattern);


    public int FileNameTitleMaxLength
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.ScreenshotFileNameTitleMaxLength = value;
                FileNamePreview = BuildPreview(FileNamePattern);
                RegionFileNamePreview = BuildPreview(RegionFileNamePattern);
            }
        }
    } = AppConfig.ScreenshotFileNameTitleMaxLength;


    private static string BuildPreview(string pattern)
    {
        return ScreenCaptureService.BuildFileName("explorer", "explorer.exe", "StarRail", DateTimeOffset.Now, 3840, 2160, pattern) + ".png";
    }


    private void BuildPlaceholderLinks()
    {
        PlaceholderTextBlock.Inlines.Clear();
        // 第一行：说明 + GitHub 链接
        PlaceholderTextBlock.Inlines.Add(new Run { Text = Lang.Zshot_ClickToInsert });
        var help = new Hyperlink { NavigateUri = new Uri(GetHelpUrl()) };
        help.Inlines.Add(new Run { Text = "Github" + Lang.Zshot_ClickToInsertSuffix });
        PlaceholderTextBlock.Inlines.Add(help);
        PlaceholderTextBlock.Inlines.Add(new LineBreak());
        // 按钮区：每个占位符一个链接（文字不带 {}，点击插入 {token}）
        for (int i = 0; i < _tokens.Length; i++)
        {
            if (i > 0)
            {
                PlaceholderTextBlock.Inlines.Add(new Run { Text = "  " });
            }
            string token = "{" + _tokens[i] + "}";
            var link = new Hyperlink { UnderlineStyle = UnderlineStyle.None };
            link.Inlines.Add(new Run
            {
                Text = _tokens[i],
                FontFamily = new FontFamily("Consolas, Cascadia Code, Microsoft YaHei UI"),
            });
            link.Click += (_, _) => InsertToken(token);
            PlaceholderTextBlock.Inlines.Add(link);
        }
    }


    private static string GetHelpUrl()
    {
        const string repo = "https://github.com/Zyl0812/Zshot";
        // 文档只维护中英两份：中文是根 README，其余语言走 docs/README.en.md
        return AppConfig.Language switch
        {
            "zh-CN" or "zh-TW" => $"{repo}#文件名模板",
            _ => $"{repo}/blob/main/docs/README.en.md#filename-templates",
        };
    }


    private void TemplateTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _lastFocusedTemplateBox = (TextBox)sender;
    }


    private void InsertToken(string token)
    {
        var box = _lastFocusedTemplateBox ?? FileNameTextBox;
        int pos = box.SelectionStart;
        box.Text = box.Text.Insert(pos, token);
        box.SelectionStart = pos + token.Length;
        box.Focus(FocusState.Programmatic);
    }


    #endregion

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
