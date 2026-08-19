using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Display;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Starshot.Core.LongCapture;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Vanara.PInvoke;

namespace Starshot.Features.Screenshot.LongCapture;

internal sealed class LongCaptureRunner
{
    public async Task<CanvasRenderTarget?> RunAsync(
        Rect regionOnVirtualScreen,
        int maxHeight,
        Action<string> setStatus,
        Task<bool> userDecision,
        CancellationToken cancellationToken)
    {
        var firstFrame = await CaptureRegionSdrAsync(regionOnVirtualScreen).ConfigureAwait(true);
        if (firstFrame is null)
        {
            return null;
        }

        var buffer = new LongImageBuffer(maxHeight);
        int width = (int)firstFrame.SizeInPixels.Width;
        int height = (int)firstFrame.SizeInPixels.Height;
        if (!buffer.TryAppend(height))
        {
            firstFrame.Dispose();
            throw new InvalidOperationException("First frame exceeds the long-capture height limit.");
        }

        var segments = new List<(CanvasBitmap Bitmap, int CropY, int Height)>
        {
            (firstFrame, 0, height),
        };

        byte[] previousGray = ToGray(firstFrame);
        setStatus($"已捕获 1 段 / {buffer.TotalHeight}px");

        try
        {
            while (!cancellationToken.IsCancellationRequested && !userDecision.IsCompleted)
            {
                await Task.Delay(450, cancellationToken).ConfigureAwait(true);
                var frame = await CaptureRegionSdrAsync(regionOnVirtualScreen).ConfigureAwait(true);
                if (frame is null)
                {
                    continue;
                }

                byte[] currentGray = ToGray(frame);
                var align = VerticalFrameAligner.Align(previousGray, currentGray, width, height, stripHeight: Math.Min(32, height / 4));
                if (!align.Accepted)
                {
                    frame.Dispose();
                    continue;
                }

                int appendHeight = height - align.OffsetY;
                if (appendHeight <= 2)
                {
                    frame.Dispose();
                    continue;
                }

                if (!buffer.TryAppend(appendHeight))
                {
                    setStatus("已达最大高度");
                    frame.Dispose();
                    break;
                }

                segments.Add((frame, align.OffsetY, appendHeight));
                previousGray = currentGray;
                setStatus($"已捕获 {buffer.SegmentCount} 段 / {buffer.TotalHeight}px");
            }
        }
        catch (OperationCanceledException)
        {
        }

        bool confirmed;
        try
        {
            confirmed = await userDecision.ConfigureAwait(true);
        }
        catch
        {
            confirmed = false;
        }

        if (!confirmed)
        {
            foreach (var s in segments)
            {
                s.Bitmap.Dispose();
            }

            return null;
        }

        var output = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), width, buffer.TotalHeight, 96, DirectXPixelFormat.B8G8R8A8UIntNormalized, CanvasAlphaMode.Premultiplied);
        using (var ds = output.CreateDrawingSession())
        {
            ds.Clear(Colors.Transparent);
            float y = 0;
            foreach (var segment in segments)
            {
                ds.DrawImage(segment.Bitmap, new Rect(0, y, width, segment.Height), new Rect(0, segment.CropY, width, segment.Height));
                y += segment.Height;
                segment.Bitmap.Dispose();
            }
        }

        return output;
    }

    private static async Task<CanvasBitmap?> CaptureRegionSdrAsync(Rect region)
    {
        int vx = User32.GetSystemMetrics((User32.SystemMetric)76);
        int vy = User32.GetSystemMetrics((User32.SystemMetric)77);
        var displays = DisplayArea.FindAll();
        DisplayArea? display = null;
        for (int i = 0; i < displays.Count; i++)
        {
            var b = displays[i].OuterBounds;
            int ox = b.X - vx;
            int oy = b.Y - vy;
            if (region.X >= ox && region.Y >= oy && region.X < ox + b.Width && region.Y < oy + b.Height)
            {
                display = displays[i];
                break;
            }
        }

        display ??= DisplayArea.Primary;
        int localX = (int)region.X - (display.OuterBounds.X - vx);
        int localY = (int)region.Y - (display.OuterBounds.Y - vy);

        using var info = DisplayInformation.CreateForDisplayId(display.DisplayId);
        var color = info.GetAdvancedColorInfo();
        bool hdr = color.CurrentAdvancedColorKind is DisplayAdvancedColorKind.HighDynamicRange;
        var format = hdr ? DirectXPixelFormat.R16G16B16A16Float : DirectXPixelFormat.R8G8B8A8UIntNormalized;
        using var frame = await ScreenCaptureHelper.CaptureMonitorAsync((nint)display.DisplayId.Value, format);
        using var full = CanvasBitmap.CreateFromDirect3D11Surface(CanvasDevice.GetSharedDevice(), frame.Surface, 96);
        CanvasBitmap source = full;
        CanvasRenderTarget? toneMapped = null;
        if (hdr)
        {
            toneMapped = ScreenCaptureHelper.TonemapToSdr(full, (float)color.SdrWhiteLevelInNits);
            source = toneMapped;
        }

        try
        {
            var cropped = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), (float)region.Width, (float)region.Height, 96, DirectXPixelFormat.B8G8R8A8UIntNormalized, CanvasAlphaMode.Premultiplied);
            using (var ds = cropped.CreateDrawingSession())
            {
                ds.DrawImage(source, new Rect(0, 0, region.Width, region.Height), new Rect(localX, localY, region.Width, region.Height));
            }

            return cropped;
        }
        finally
        {
            toneMapped?.Dispose();
        }
    }

    private static byte[] ToGray(CanvasBitmap bitmap)
    {
        using var sdr = bitmap.Format == DirectXPixelFormat.B8G8R8A8UIntNormalized
            ? null
            : new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height, 96, DirectXPixelFormat.B8G8R8A8UIntNormalized, CanvasAlphaMode.Premultiplied);
        if (sdr is not null)
        {
            using var ds = sdr.CreateDrawingSession();
            ds.DrawImage(bitmap);
        }

        var src = sdr ?? bitmap;
        return GrayscaleConvert.BgraToGray(src.GetPixelBytes(), (int)src.SizeInPixels.Width, (int)src.SizeInPixels.Height);
    }
}
