using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Starshot.Features.Update;

/// <summary>
/// apply hdiff 目录二进制 diff（CI: hdiffz -m -c-zstd-19 -D 生成）。
/// 用 native hpatchz.exe 子进程——跨进程读 old 目录，避开主进程加载的 dll 锁冲突。
/// hpatchz.exe 在 release 根目录（同 Starshot.exe），CI 构建带，本地 debug 不带。
/// </summary>
internal static class HPatchInvoker
{
    public static async Task<bool> ApplyAsync(
        string oldDir, string patchFile, string newDir, CancellationToken ct)
    {
        string exe = FindHpatchz();
        if (exe is null) return false;

        var psi = new ProcessStartInfo(exe, $"-f \"{oldDir}\" \"{patchFile}\" \"{newDir}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        try
        {
            var p = Process.Start(psi);
            if (p is null) return false;
            await p.WaitForExitAsync(ct);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string? FindHpatchz()
    {
        // release 根目录（Starshot.exe 旁）。CI 构建带 hpatchz.exe；本地 debug 没有则返回 null（触发 fallback 整包）。
        string exe = Path.Combine(AppContext.BaseDirectory, "hpatchz.exe");
        return File.Exists(exe) ? exe : null;
    }
}
