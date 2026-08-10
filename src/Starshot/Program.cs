global using Starshot.Language;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32.TaskScheduler;

namespace Starshot;

#if DISABLE_XAML_GENERATED_MAIN

/// <summary>
/// Program class
/// </summary>
public static class Program
{


    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2411")]
    [global::System.STAThreadAttribute]
    static void Main(string[] args)
    {
        // 提权子进程：--manage-task create/delete，以管理员权限调 TaskScheduler API 创建/删除任务后退出
        if (args.Length > 0 && args[0] == "--manage-task")
        {
            try
            {
                using var ts = new TaskService();
                if (args.Length > 1 && args[1] == "create")
                {
                    string launcherPath = args.Length > 2 ? args[2] : "";
                    string taskArgs = args.Length > 3 ? args[3] : "";
                    var td = ts.NewTask();
                    td.Triggers.Add(new LogonTrigger());
                    td.Actions.Add(new ExecAction(launcherPath, taskArgs));
                    try { ts.RootFolder.DeleteTask("Starshot", false); } catch { }
                    ts.RootFolder.RegisterTaskDefinition("Starshot", td,
                        TaskCreation.CreateOrUpdate,
                        $"{Environment.UserDomainName}\\{Environment.UserName}",
                        null,
                        TaskLogonType.InteractiveToken);
                    LogManageTask($"Task created: {launcherPath} {taskArgs}");
                }
                else
                {
                    ts.RootFolder.DeleteTask("Starshot", false);
                    LogManageTask("Task deleted");
                }
            }
            catch (Exception ex)
            {
                LogManageTask($"Task operation failed: {ex}");
            }
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;

        global::WinRT.ComWrappersSupport.InitializeComWrappers();
        global::Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }


    private static void LogManageTask(string message)
    {
        try
        {
            string logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Starshot", "log", "TaskScheduler.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
            File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss.fff}] [manage-task] {message}\n");
        }
        catch { }
    }


    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        string logFile = AppConfig.LogFile;
        if (string.IsNullOrWhiteSpace(logFile))
        {
            string logFolder = Path.Combine(AppContext.BaseDirectory, "log");
            Directory.CreateDirectory(logFolder);
            logFile = Path.Combine(logFolder, $"Starshot_{DateTime.Now:yyMMdd}.log");
        }
        var sb = new StringBuilder();
        sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Program Crash:");
        sb.AppendLine(e.ExceptionObject.ToString());
        if (e.ExceptionObject is Exception { Data.Count: > 0 } ex)
        {
            foreach (DictionaryEntry item in ex.Data)
            {
                sb.AppendLine($"{item.Key}: {item.Value}");
            }
        }
        using var fs = File.Open(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        using var sw = new StreamWriter(fs);
        sw.Write(sb);
    }
}

#endif
