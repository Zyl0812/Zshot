using Starshot.Core;
using Xunit;

namespace Starshot.Core.Tests;

public class ScreenshotSavePolicyTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    public void ShouldWriteFile_respects_auto_save_and_copy_only(bool autoSave, bool copyOnly, bool expected)
    {
        Assert.Equal(expected, ScreenshotSavePolicy.ShouldWriteFile(autoSave, copyOnly));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void ShouldCopyToClipboard_copy_only_always_copies(bool autoCopy, bool copyOnly, bool expected)
    {
        Assert.Equal(expected, ScreenshotSavePolicy.ShouldCopyToClipboard(autoCopy, copyOnly));
    }

    [Fact]
    public void Fullscreen_with_save_and_copy_uses_saved_file()
    {
        Assert.Equal(
            ClipboardExportKind.SavedFile,
            ScreenshotSavePolicy.GetFullscreenClipboardKind(autoSaveEnabled: true, autoCopyEnabled: true, copyOnlyHotkey: false));
    }

    [Fact]
    public void Fullscreen_copy_without_save_uses_sdr_bitmap()
    {
        Assert.Equal(
            ClipboardExportKind.SdrBitmap,
            ScreenshotSavePolicy.GetFullscreenClipboardKind(autoSaveEnabled: false, autoCopyEnabled: true, copyOnlyHotkey: false));
    }

    [Fact]
    public void Fullscreen_neither_save_nor_copy_exports_nothing()
    {
        Assert.Equal(
            ClipboardExportKind.None,
            ScreenshotSavePolicy.GetFullscreenClipboardKind(autoSaveEnabled: false, autoCopyEnabled: false, copyOnlyHotkey: false));
    }

    [Fact]
    public void Copy_only_hotkey_never_writes_file()
    {
        Assert.False(ScreenshotSavePolicy.ShouldWriteFile(autoSaveEnabled: true, copyOnlyHotkey: true));
        Assert.Equal(
            ClipboardExportKind.SdrBitmap,
            ScreenshotSavePolicy.GetFullscreenClipboardKind(autoSaveEnabled: true, autoCopyEnabled: false, copyOnlyHotkey: true));
    }
}
