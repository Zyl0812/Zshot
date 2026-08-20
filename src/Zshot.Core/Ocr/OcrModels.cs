namespace Zshot.Core.Ocr;

public sealed class OcrBlock
{
    public required string Text { get; init; }
    public double Confidence { get; init; }
    public IReadOnlyList<OcrPoint> Polygon { get; init; } = Array.Empty<OcrPoint>();

    public OcrPoint Centroid
    {
        get
        {
            if (Polygon.Count == 0)
            {
                return default;
            }

            return new OcrPoint(Polygon.Average(p => p.X), Polygon.Average(p => p.Y));
        }
    }

    public double Height
    {
        get
        {
            if (Polygon.Count == 0)
            {
                return 0;
            }

            return Polygon.Max(p => p.Y) - Polygon.Min(p => p.Y);
        }
    }
}

public readonly record struct OcrPoint(double X, double Y);

public sealed class OcrRequest
{
    public required byte[] BgraPixels { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public string? LanguageHint { get; init; }
}

public sealed class OcrResult
{
    public required IReadOnlyList<OcrBlock> Blocks { get; init; }
    public required string PlainText { get; init; }
    public TimeSpan Elapsed { get; init; }
}

public interface IOcrEngine
{
    string Name { get; }
    Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default);
}
