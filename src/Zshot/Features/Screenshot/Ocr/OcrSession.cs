using Microsoft.Graphics.Canvas;
using Zshot.Core.Ocr;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.DirectX;

namespace Zshot.Features.Screenshot.Ocr;

internal sealed class OcrSession
{
    private readonly LocalOcrEngine _engine;

    public OcrSession(IOcrRecognizer recognizer)
    {
        _engine = new LocalOcrEngine(recognizer);
    }

    public string Name => _engine.Name;

    public async Task<OcrResult> RecognizeAsync(CanvasBitmap bitmap, CancellationToken cancellationToken = default)
    {
        byte[] pixels = GetBgra(bitmap);
        return await _engine.RecognizeAsync(new OcrRequest
        {
            BgraPixels = pixels,
            Width = (int)bitmap.SizeInPixels.Width,
            Height = (int)bitmap.SizeInPixels.Height,
        }, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] GetBgra(CanvasBitmap bitmap)
    {
        int w = (int)bitmap.SizeInPixels.Width;
        int h = (int)bitmap.SizeInPixels.Height;
        if (bitmap.Format == DirectXPixelFormat.B8G8R8A8UIntNormalized)
        {
            return bitmap.GetPixelBytes();
        }

        using var converted = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), w, h, 96, DirectXPixelFormat.B8G8R8A8UIntNormalized, CanvasAlphaMode.Premultiplied);
        using (var ds = converted.CreateDrawingSession())
        {
            ds.DrawImage(bitmap);
        }

        return converted.GetPixelBytes();
    }
}
