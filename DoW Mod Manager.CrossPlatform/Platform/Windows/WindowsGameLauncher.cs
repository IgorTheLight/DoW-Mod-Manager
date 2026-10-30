using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Platform.Windows;

/// <summary>
/// Windows game launcher. Faithful port of the legacy
/// <c>ModManagerForm.StartGameWithOptions</c>: starts the game executable directly, then spins
/// up the optional post-launch background threads (CPU affinity / high priority and the
/// Soulstorm fog-removal memory patch). These threads survive the manager closing, exactly like
/// the original WinForms behaviour.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsGameLauncher : IGameLauncher
{
    // Affinity 6 means using only CPU threads 2 and 3 (6 = 0b0110), matching the legacy code.
    static readonly IntPtr DowProcessorAffinity = (IntPtr)0x0006;

    public Task<GameLaunchResult> LaunchAsync(GameInstall game, LaunchOptions options, CancellationToken ct = default)
    {
        var proc = new Process();
        proc.StartInfo.FileName = game.ExecutablePath;
        proc.StartInfo.Arguments = options.ToArgumentString();
        // The legacy code relied on the manager's working directory being the install dir.
        // Set it explicitly so the game finds its data regardless of where the manager runs.
        proc.StartInfo.WorkingDirectory = game.InstallDir;
        proc.Start();

        string dowProcessName = proc.ProcessName;

        // Create a new thread to change the process CPU affinity after the game has started.
        if (options.CpuAffinityOptimization)
            StartAffinityThread(dowProcessName);

        // Create a new thread for the fog removal which manipulates process memory (Soulstorm only).
        if (options.NoFog && game.SupportsNoFog)
            StartFogRemovalThread(dowProcessName);

        return Task.FromResult(new GameLaunchResult
        {
            ProcessId = SafeProcessId(proc),
            ProcessName = dowProcessName,
            Started = true,
            Detail = $"Launched {game.ExecutableName} with: {proc.StartInfo.Arguments}"
        });
    }

    /// <summary>On Windows there is no prefix: the program is simply started from its own folder.</summary>
    public Task<GameLaunchResult> RunInGamePrefixAsync(GameInstall game, string exePath, CancellationToken ct = default)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = System.IO.Path.GetDirectoryName(exePath) ?? game.InstallDir,
                UseShellExecute = true
            });

            return Task.FromResult(proc is null
                ? new GameLaunchResult { Started = false, Detail = $"Could not start {exePath}" }
                : new GameLaunchResult
                {
                    ProcessId = SafeProcessId(proc),
                    ProcessName = System.IO.Path.GetFileNameWithoutExtension(exePath),
                    Started = true,
                    Detail = $"Started {exePath}"
                });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new GameLaunchResult { Started = false, Detail = $"Could not start {exePath}: {ex.Message}" });
        }
    }

    static int SafeProcessId(Process proc)
    {
        try { return proc.Id; }
        catch (Exception) { return 0; }
    }

    static void StartAffinityThread(string procName)
    {
        // Threads could work even if the application would be closed.
        new Thread(() =>
        {
            int triesCount = 0;

            // We will try 30 times and then the thread will be terminated regardless.
            while (triesCount < 30)
            {
                Thread.Sleep(1000);
                try
                {
                    Process[] dow = Process.GetProcessesByName(procName);
                    dow[0].PriorityClass = ProcessPriorityClass.High;
                    dow[0].ProcessorAffinity = DowProcessorAffinity;
                    break;    // We've done what we intended to do.
                }
                catch (Exception)
                {
                    triesCount++;
                }
            }
        })
        { IsBackground = false }.Start();
    }

    static void StartFogRemovalThread(string procName)
    {
        new Thread(() =>
        {
            int timeOutCounter = 0;

            // We will try 30 times and then the thread will be terminated regardless.
            while (timeOutCounter < 30)
            {
                Thread.Sleep(1000);
                try
                {
                    Process[] dow = Process.GetProcessesByName(procName);
                    WindowsFogRemover.DisableFog(dow[0]);
                    break;     // We've done what we intended to do.
                }
                catch (Exception)
                {
                    timeOutCounter++;
                }
            }
        })
        { IsBackground = false }.Start();
    }
}
