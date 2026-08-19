using Starshot.Core.Ocr;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Starshot.Features.Screenshot.Ocr;

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
            foreach (var word in line.Words)
            {
                var r = word.BoundingRect;
                blocks.Add(new OcrBlock
                {
                    Text = word.Text,
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
        }

        return blocks;
    }
}
