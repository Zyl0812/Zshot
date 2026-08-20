using RapidOcrNet;
using SkiaSharp;
using Zshot.Core.Ocr;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Zshot.Features.Screenshot.Ocr;

/// <summary>
/// PP-OCRv6 Small（RapidOcrNet + ONNX Runtime，本地推理，截图不出机器）。
/// 模型随安装包发布在 models/v6 下；缺文件时回退 Windows 本地 OCR——
/// 开发构建和解压不完整的安装都能继续用，而不是直接报错。
/// </summary>
internal sealed class RapidOcrRecognizer : IOcrRecognizer
{
    private readonly WindowsMediaOcrRecognizer _fallback = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RapidOcr? _ocr;
    private bool _modelsMissing;

    public string Name => _modelsMissing ? _fallback.Name : "PP-OCRv6 Small";

    public async Task<IReadOnlyList<OcrBlock>> DetectAsync(OcrRequest request, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var engine = EnsureEngine();
            if (engine is null)
            {
                return await _fallback.DetectAsync(request, cancellationToken).ConfigureAwait(false);
            }

            // Detect 是同步 CPU 密集调用，挪出 UI 线程
            return await Task.Run(() => Recognize(engine, request, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>首次调用时加载模型（约几百毫秒），之后复用同一实例。</summary>
    private RapidOcr? EnsureEngine()
    {
        if (_ocr is not null)
        {
            return _ocr;
        }

        if (_modelsMissing)
        {
            return null;
        }

        var models = ResolveModelSet();
        if (!File.Exists(models.DetModelPath) || !File.Exists(models.RecModelPath)
            || !File.Exists(models.KeysPath) || !File.Exists(models.ClsModelPath))
        {
            _modelsMissing = true;
            return null;
        }

        var ocr = new RapidOcr();
        ocr.InitModels(models, InferenceThreads());
        _ocr = ocr;
        return _ocr;
    }

    /// <summary>
    /// ONNX Runtime 的 intra-op 线程池超订会严重反噬：20 逻辑核实测下
    /// numThread=20 要 3383ms，而 4~6 只要约 1095ms。取逻辑核一半并封顶 6。
    /// </summary>
    private static int InferenceThreads() => Math.Clamp(Environment.ProcessorCount / 2, 2, 6);

    /// <summary>
    /// 预设里的路径是相对路径，托盘应用的工作目录不一定是安装目录，
    /// 统一按 BaseDirectory 解析成绝对路径。
    /// </summary>
    private static RapidOcrModelSet ResolveModelSet()
    {
        var preset = RapidOcrModelSet.PPOCRv6Small;
        string root = AppContext.BaseDirectory;
        return preset with
        {
            DetModelPath = Path.Combine(root, preset.DetModelPath),
            ClsModelPath = Path.Combine(root, preset.ClsModelPath),
            RecModelPath = Path.Combine(root, preset.RecModelPath),
            KeysPath = Path.Combine(root, preset.KeysPath),
        };
    }

    private static IReadOnlyList<OcrBlock> Recognize(RapidOcr engine, OcrRequest request, CancellationToken cancellationToken)
    {
        var info = new SKImageInfo(request.Width, request.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        Marshal.Copy(request.BgraPixels, 0, bitmap.GetPixels(), info.BytesSize);

        var result = engine.Detect(bitmap, RapidOcrOptions.PPOCRv6, cancellationToken);
        var blocks = new List<OcrBlock>();
        foreach (var block in result.TextBlocks)
        {
            if (string.IsNullOrWhiteSpace(block.Text))
            {
                continue;
            }

            var polygon = new OcrPoint[block.BoxPoints.Length];
            for (int i = 0; i < block.BoxPoints.Length; i++)
            {
                polygon[i] = new OcrPoint(block.BoxPoints[i].X, block.BoxPoints[i].Y);
            }

            blocks.Add(new OcrBlock
            {
                Text = block.Text,
                Confidence = block.BoxScore,
                Polygon = polygon,
            });
        }

        return blocks;
    }
}
