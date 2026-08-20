using Zshot.Core.Ocr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Zshot.Features.Screenshot.Ocr;

internal sealed class WindowsMediaOcrRecognizer : IOcrRecognizer
{
    public string Name => "Windows.Media.Ocr";

    public async Task<IReadOnlyList<OcrBlock>> DetectAsync(OcrRequest request, CancellationToken cancellationToken = default)
    {
        var engine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("zh-Hans"))
            ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en"))
            ?? OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            throw new InvalidOperationException("Windows OCR is not available on this system.");
        }

        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            request.BgraPixels.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            request.Width,
            request.Height,
            BitmapAlphaMode.Premultiplied);

        cancellationToken.ThrowIfCancellationRequested();
        var result = await engine.RecognizeAsync(bitmap).AsTask(cancellationToken).ConfigureAwait(false);
        var blocks = new List<OcrBlock>();
        foreach (var line in result.Lines)
        {
            var r = line.Words.Count > 0
                ? Union(line.Words.Select(w => w.BoundingRect))
                : default;
            blocks.Add(new OcrBlock
            {
                Text = line.Text,
                Confidence = 1,
                Polygon =
                [
                    new OcrPoint(r.X, r.Y),
                    new OcrPoint(r.X + r.Width, r.Y),
                    new OcrPoint(r.X + r.Width, r.Y + r.Height),
                    new OcrPoint(r.X, r.Y + r.Height),
                ],
            });
        }

        return blocks;
    }

    private static Windows.Foundation.Rect Union(IEnumerable<Windows.Foundation.Rect> rects)
    {
        double x1 = double.MaxValue, y1 = double.MaxValue, x2 = double.MinValue, y2 = double.MinValue;
        foreach (var r in rects)
        {
            x1 = Math.Min(x1, r.X);
            y1 = Math.Min(y1, r.Y);
            x2 = Math.Max(x2, r.X + r.Width);
            y2 = Math.Max(y2, r.Y + r.Height);
        }

        if (x1 == double.MaxValue)
        {
            return default;
        }

        return new Windows.Foundation.Rect(x1, y1, x2 - x1, y2 - y1);
    }
}
