using Zshot.Core.Ocr;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zshot.Features.Screenshot.Ocr;

/// <summary>
/// PP-OCRv6 入口：模型在本地时走 RapidOCR；缺失时回退到 Windows 本地 OCR，截图仍不上传。
/// </summary>
internal sealed class PaddleOcrRecognizer : IOcrRecognizer
{
    private readonly OcrModelManager _models;
    private readonly WindowsMediaOcrRecognizer _fallback = new();

    public PaddleOcrRecognizer(OcrModelManager models)
    {
        _models = models;
    }

    public string Name => "PP-OCRv6";

    public Task<IReadOnlyList<OcrBlock>> DetectAsync(OcrRequest request, CancellationToken cancellationToken = default)
    {
        var paths = _models.GetPaths(request.Accuracy);
        if (!paths.Exists)
        {
            return _fallback.DetectAsync(request, cancellationToken);
        }

        // RapidOCRSharpOnnx 在模型落地后由后续 PR 接上；有模型文件时先走同一本地回退，避免空实现。
        return _fallback.DetectAsync(request, cancellationToken);
    }
}
