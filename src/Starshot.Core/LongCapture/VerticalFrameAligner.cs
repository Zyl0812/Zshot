namespace Starshot.Core.LongCapture;

public readonly record struct FrameAlignResult(int OffsetY, double Score, bool Accepted);

public static class VerticalFrameAligner
{
    /// <summary>
    /// 在相邻同尺寸灰度帧上找纵向重叠：用上一帧底部条带在当前帧上部做 SAD 匹配。
    /// offsetY 表示当前帧应跳过的顶部行数后再接到输出上。
    /// </summary>
    public static FrameAlignResult Align(
        ReadOnlySpan<byte> previous,
        ReadOnlySpan<byte> current,
        int width,
        int height,
        int stripHeight = 32,
        double acceptScore = 0.35)
    {
        if (width <= 0 || height <= 0 || previous.Length < width * height || current.Length < width * height)
        {
            return new FrameAlignResult(0, 0, false);
        }

        stripHeight = Math.Clamp(stripHeight, 4, Math.Max(4, height / 3));
        int searchMax = height - stripHeight;
        if (searchMax <= 0)
        {
            return new FrameAlignResult(0, 0, false);
        }

        int bestY = 0;
        long bestSad = long.MaxValue;
        int prevStart = (height - stripHeight) * width;

        for (int y = 0; y <= searchMax; y++)
        {
            long sad = 0;
            int currStart = y * width;
            for (int i = 0; i < stripHeight * width; i++)
            {
                sad += Math.Abs(previous[prevStart + i] - current[currStart + i]);
            }

            if (sad < bestSad)
            {
                bestSad = sad;
                bestY = y;
            }
        }

        double score = 1.0 - (bestSad / (double)(stripHeight * width * 255));
        bool accepted = score >= acceptScore;
        return new FrameAlignResult(bestY, score, accepted);
    }
}

public sealed class LongImageBuffer
{
    private readonly List<int> _segmentHeights = new();

    public LongImageBuffer(int maxHeight = 32000)
    {
        MaxHeight = Math.Max(1, maxHeight);
    }

    public int MaxHeight { get; }
    public int TotalHeight { get; private set; }
    public int SegmentCount => _segmentHeights.Count;

    public bool CanAppend(int height) => height > 0 && TotalHeight + height <= MaxHeight;

    public bool TryAppend(int height)
    {
        if (!CanAppend(height))
        {
            return false;
        }

        _segmentHeights.Add(height);
        TotalHeight += height;
        return true;
    }
}
