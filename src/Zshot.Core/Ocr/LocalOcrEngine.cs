namespace Zshot.Core.Ocr;

public interface IOcrRecognizer
{
    string Name { get; }
    Task<IReadOnlyList<OcrBlock>> DetectAsync(OcrRequest request, CancellationToken cancellationToken = default);
}

public sealed class LocalOcrEngine : IOcrEngine
{
    private readonly IOcrRecognizer _recognizer;

    public LocalOcrEngine(IOcrRecognizer recognizer)
    {
        _recognizer = recognizer;
    }

    public string Name => _recognizer.Name;

    public async Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Width <= 0 || request.Height <= 0 || request.BgraPixels.Length < request.Width * request.Height * 4)
        {
            throw new ArgumentException("OCR request bitmap is invalid.", nameof(request));
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var raw = await _recognizer.DetectAsync(request, cancellationToken).ConfigureAwait(false);
        var ordered = OcrReadingOrder.Sort(raw);
        return new OcrResult
        {
            Blocks = ordered,
            PlainText = OcrReadingOrder.ToPlainText(ordered),
            Elapsed = sw.Elapsed,
        };
    }
}
