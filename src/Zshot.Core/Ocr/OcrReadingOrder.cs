namespace Zshot.Core.Ocr;

public static class OcrReadingOrder
{
    public static IReadOnlyList<OcrBlock> Sort(IReadOnlyList<OcrBlock> blocks)
        => GroupLines(blocks).SelectMany(line => line).ToList();

    public static string ToPlainText(IEnumerable<OcrBlock> blocks, bool keepLineBreaks = true)
    {
        var lines = GroupLines(blocks as IReadOnlyList<OcrBlock> ?? blocks.ToList());
        if (lines.Count == 0)
        {
            return "";
        }

        if (!keepLineBreaks)
        {
            return string.Join(" ", lines.SelectMany(line => line).Select(b => b.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
        }

        return string.Join(Environment.NewLine, lines.Select(JoinLine));
    }

    private static List<List<OcrBlock>> GroupLines(IReadOnlyList<OcrBlock> blocks)
    {
        if (blocks.Count == 0)
        {
            return [];
        }

        if (blocks.Count == 1)
        {
            return [[blocks[0]]];
        }

        double threshold = Math.Max(8, MedianHeight(blocks) * 0.6);
        var ordered = blocks.OrderBy(b => b.Centroid.Y).ThenBy(b => b.Centroid.X).ToList();
        var lines = new List<List<OcrBlock>>();
        foreach (var block in ordered)
        {
            var line = lines.LastOrDefault();
            if (line is null || Math.Abs(block.Centroid.Y - line.Average(x => x.Centroid.Y)) > threshold)
            {
                lines.Add([block]);
            }
            else
            {
                line.Add(block);
            }
        }

        foreach (var line in lines)
        {
            line.Sort((a, b) => a.Centroid.X.CompareTo(b.Centroid.X));
        }

        return lines;
    }

    private static string JoinLine(List<OcrBlock> line)
        => string.Join(" ", line.Select(b => b.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

    private static double MedianHeight(IReadOnlyList<OcrBlock> blocks)
    {
        var heights = blocks.Select(b => b.Height).Where(h => h > 0).OrderBy(h => h).ToList();
        if (heights.Count == 0)
        {
            return 16;
        }

        return heights[heights.Count / 2];
    }
}
