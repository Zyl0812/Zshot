namespace Starshot.Core.Ocr;

public static class OcrReadingOrder
{
    public static IReadOnlyList<OcrBlock> Sort(IReadOnlyList<OcrBlock> blocks)
    {
        if (blocks.Count <= 1)
        {
            return blocks;
        }

        double medianHeight = MedianHeight(blocks);
        double threshold = Math.Max(8, medianHeight * 0.6);

        var ordered = blocks.OrderBy(b => b.Centroid.Y).ThenBy(b => b.Centroid.X).ToList();
        var lines = new List<List<OcrBlock>>();
        foreach (var block in ordered)
        {
            var line = lines.LastOrDefault();
            if (line is null || Math.Abs(block.Centroid.Y - line.Average(x => x.Centroid.Y)) > threshold)
            {
                lines.Add(new List<OcrBlock> { block });
            }
            else
            {
                line.Add(block);
            }
        }

        return lines
            .SelectMany(line => line.OrderBy(b => b.Centroid.X))
            .ToList();
    }

    public static string ToPlainText(IEnumerable<OcrBlock> blocks, bool keepLineBreaks = true)
    {
        var sorted = Sort(blocks as IReadOnlyList<OcrBlock> ?? blocks.ToList());
        if (!keepLineBreaks)
        {
            return string.Join(" ", sorted.Select(b => b.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
        }

        if (sorted.Count == 0)
        {
            return "";
        }

        double medianHeight = MedianHeight(sorted);
        double threshold = Math.Max(8, medianHeight * 0.6);
        var lines = new List<List<OcrBlock>>();
        foreach (var block in sorted)
        {
            var line = lines.LastOrDefault();
            if (line is null || Math.Abs(block.Centroid.Y - line.Average(x => x.Centroid.Y)) > threshold)
            {
                lines.Add(new List<OcrBlock> { block });
            }
            else
            {
                line.Add(block);
            }
        }

        return string.Join(Environment.NewLine, lines.Select(line => string.Join(" ", line.Select(b => b.Text))));
    }

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
