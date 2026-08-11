using SharpHDiffPatch.Core;
using SharpHDiffPatch.Core.Event;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Starshot.Features.Update;

/// <summary>
/// 包装 SharpHDiffPatch.Core：apply hdiff 目录二进制 diff。
/// CI 用 hdiffz -m -c-zstd-19 -D 生成（zstd 压缩；lzma2 因 SharpHDiffPatch.Core 2.4.0 解析 bug 弃用）。
/// </summary>
internal static class HPatchInvoker
{
    public static async Task ApplyAsync(
        string oldDir, string patchFile, string newDir,
        IProgress<int>? progress, CancellationToken ct)
    {
        void OnPatch(object? s, PatchEvent e)
        {
            try { progress?.Report((int)Math.Round(e.ProgressPercentage)); }
            catch { }
        }

        HDiffPatch.LogVerbosity = Verbosity.Quiet;
        EventListener.PatchEvent += OnPatch;
        try
        {
            await Task.Run(() =>
            {
                var patcher = new HDiffPatch();
                patcher.Initialize(patchFile);
                patcher.Patch(oldDir, newDir, true, ct);
            }, ct);
        }
        finally
        {
            EventListener.PatchEvent -= OnPatch;
        }
    }
}
