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

    [Fact]
    public void Overlay_complete_follows_auto_save_and_auto_copy()
    {
        var decision = ScreenshotSavePolicy.ResolveOverlayExport(OverlayExportAction.Complete, autoSave: true, autoCopy: true);
        Assert.True(decision.WriteFile);
        Assert.True(decision.CopyToClipboard);
        Assert.True(decision.CanEndSession);
    }

    [Fact]
    public void Overlay_complete_with_both_off_does_not_end_session()
    {
        var decision = ScreenshotSavePolicy.ResolveOverlayExport(OverlayExportAction.Complete, autoSave: false, autoCopy: false);
        Assert.False(decision.WriteFile);
        Assert.False(decision.CopyToClipboard);
        Assert.False(decision.CanEndSession);
    }

    [Fact]
    public void Overlay_copy_only_never_writes_file()
    {
        var decision = ScreenshotSavePolicy.ResolveOverlayExport(OverlayExportAction.CopyOnly, autoSave: true, autoCopy: false);
        Assert.False(decision.WriteFile);
        Assert.True(decision.CopyToClipboard);
        Assert.True(decision.CanEndSession);
    }

    [Fact]
    public void Overlay_save_always_writes_file_and_follows_auto_copy()
    {
        var on = ScreenshotSavePolicy.ResolveOverlayExport(OverlayExportAction.Save, autoSave: false, autoCopy: true);
        Assert.True(on.WriteFile);
        Assert.True(on.CopyToClipboard);
        Assert.True(on.CanEndSession);

        var off = ScreenshotSavePolicy.ResolveOverlayExport(OverlayExportAction.Save, autoSave: false, autoCopy: false);
        Assert.True(off.WriteFile);
        Assert.False(off.CopyToClipboard);
        Assert.True(off.CanEndSession);
    }
}
