using Starshot.Core.LongCapture;
using Xunit;

namespace Starshot.Core.Tests;

public class LongCaptureTests
{
    [Fact]
    public void Align_finds_vertical_overlap_on_shifted_frame()
    {
        const int w = 16;
        const int h = 40;
        byte[] previous = new byte[w * h];
        byte[] current = new byte[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                previous[y * w + x] = (byte)((y * 11 + x * 3) % 200);
            }
        }

        const int shift = 9;
        for (int y = 0; y < h - shift; y++)
        {
            Array.Copy(previous, (y + shift) * w, current, y * w, w);
        }

        var result = VerticalFrameAligner.Align(previous, current, w, h, stripHeight: 8, acceptScore: 0.5);
        Assert.True(result.Accepted);
        Assert.Equal(h - shift - 8, result.OffsetY);
    }

    [Fact]
    public void Buffer_rejects_overflow_instead_of_growing()
    {
        var buffer = new LongImageBuffer(maxHeight: 100);
        Assert.True(buffer.TryAppend(60));
        Assert.False(buffer.CanAppend(50));
        Assert.False(buffer.TryAppend(50));
        Assert.Equal(60, buffer.TotalHeight);
        Assert.Equal(1, buffer.SegmentCount);
    }

    [Fact]
    public void Bgra_to_gray_uses_luma()
    {
        byte[] bgra = [10, 20, 30, 255];
        byte[] gray = GrayscaleConvert.BgraToGray(bgra, 1, 1);
        Assert.Single(gray);
        Assert.Equal((byte)((10 * 29 + 20 * 150 + 30 * 77) >> 8), gray[0]);
    }

    [Fact]
    public void Align_rejects_unrelated_frames()
    {
        const int w = 8;
        const int h = 16;
        byte[] previous = Enumerable.Repeat((byte)10, w * h).ToArray();
        byte[] current = Enumerable.Repeat((byte)200, w * h).ToArray();
        var result = VerticalFrameAligner.Align(previous, current, w, h, stripHeight: 4, acceptScore: 0.8);
        Assert.False(result.Accepted);
    }
}
