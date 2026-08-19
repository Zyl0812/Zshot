namespace Zshot.Core;

public enum ClipboardExportKind
{
    None,
    SavedFile,
    SdrBitmap,
}

public enum OverlayExportAction
{
    Complete,
    CopyOnly,
    Save,
}

public readonly record struct OverlayExportDecision(bool WriteFile, bool CopyToClipboard, bool CanEndSession);

/// <summary>
/// 决定截图完成后是否写文件、是否进剪贴板。与 WinUI / DXGI 无关，供捕获服务与单测共用。
/// </summary>
public static class ScreenshotSavePolicy
{
    public static bool ShouldWriteFile(bool autoSaveEnabled, bool copyOnlyHotkey)
        => autoSaveEnabled && !copyOnlyHotkey;

    public static bool ShouldCopyToClipboard(bool autoCopyEnabled, bool copyOnlyHotkey)
        => copyOnlyHotkey || autoCopyEnabled;

    /// <summary>
    /// 全屏截图剪贴板形态：有文件时沿用 CF_HDROP；无文件时改为 SDR 位图。
    /// </summary>
    public static ClipboardExportKind GetFullscreenClipboardKind(bool autoSaveEnabled, bool autoCopyEnabled, bool copyOnlyHotkey)
    {
        if (!ShouldCopyToClipboard(autoCopyEnabled, copyOnlyHotkey))
        {
            return ClipboardExportKind.None;
        }

        return ShouldWriteFile(autoSaveEnabled, copyOnlyHotkey)
            ? ClipboardExportKind.SavedFile
            : ClipboardExportKind.SdrBitmap;
    }

    public static OverlayExportDecision ResolveOverlayExport(OverlayExportAction action, bool autoSave, bool autoCopy)
    {
        return action switch
        {
            OverlayExportAction.CopyOnly => new OverlayExportDecision(false, true, true),
            OverlayExportAction.Save => new OverlayExportDecision(true, autoCopy, true),
            OverlayExportAction.Complete => new OverlayExportDecision(autoSave, autoCopy, autoSave || autoCopy),
            _ => new OverlayExportDecision(false, false, false),
        };
    }
}
