using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Services;

namespace DoW_Mod_Manager.Platform.Linux;

/// <summary>
/// Launches Dawn of War on Linux. Route order (see <see cref="LinuxLaunchPlanner"/>):
/// <list type="number">
/// <item><c>DOWMM_PROTON</c> set: Proton is run directly with that script.</item>
/// <item>Steam installs with an appid go through the Steam client so the game runs with the
/// Proton version and Steam Linux Runtime container the user configured: <c>steam -applaunch</c>
/// (native, or snap via <c>/snap/bin/steam</c>), then
/// <c>flatpak run com.valvesoftware.Steam -applaunch</c> (Flatpak Steam), then
/// <c>xdg-open steam://run/&lt;appid&gt;//&lt;args&gt;/</c> when only a URL handler is registered.</item>
/// <item>Otherwise the newest installed Proton is run directly (<c>proton run</c>), inside the
/// Steam Linux Runtime container its toolmanifest requires, as Steam does.</item>
/// <item>Last resort: plain Wine (GOG installs via Heroic/Lutris/Bottles, or plain wine).</item>
/// </list>
/// When Steam (or Proton) starts the game, the started process is not the game, so after
/// returning the launcher keeps polling <c>/proc</c> (<see cref="IGameProcessFinder"/>) for the game
/// exe in the background and reports its pid through <see cref="GameLaunchResult.GameConfirmed"/>.
/// Direct Proton uses the build Steam would pick (<see cref="ProtonToolResolver"/>).
/// The legacy Windows post-launch tweaks (CPU affinity, in-memory fog removal) are Windows-only and
/// become no-ops here; when requested that is noted in <see cref="GameLaunchResult.Detail"/>.
/// <para>Environment overrides:</para>
/// <list type="bullet">
/// <item><c>DOWMM_PROTON</c>: full path to a <c>proton</c> script; bypasses Steam and forces the
/// direct Proton route (also used by <see cref="RunInGamePrefixAsync"/>).</item>
/// <item><c>DOWMM_WINE</c>: wine binary name or path for the Wine route (default: <c>wine</c> on PATH).</item>
/// <item><c>DOWMM_WINEPREFIX</c>: WINEPREFIX for the Wine route; takes precedence over the Proton
/// prefix and an inherited <c>WINEPREFIX</c>, and forces <see cref="RunInGamePrefixAsync"/> to use Wine.</item>
/// <item><c>WINEPREFIX</c> (inherited, e.g. from Lutris/Bottles/Heroic): used by the Wine route when
/// neither <c>DOWMM_WINEPREFIX</c> nor a Proton prefix applies; otherwise Wine's default <c>~/.wine</c>.</item>
/// </list>
/// </summary>
public sealed class LinuxGameLauncher : IGameLauncher
{
    private static readonly TimeSpan DefaultGameStartTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly LinuxLaunchHost _host;
    private readonly IGameProcessFinder _finder;
    private readonly TimeSpan _gameStartTimeout;
    private readonly TimeSpan _pollInterval;
    private readonly Func<ProcessStartInfo, Process?> _start;

    /// <param name="host">Environment probes (defaults to the real host).</param>
    /// <param name="finder">Game process finder (defaults to <c>/proc</c>).</param>
    /// <param name="gameStartTimeout">How long to wait for the game process to appear.</param>
    /// <param name="pollInterval">Delay between two process scans.</param>
    /// <param name="startProcess">Starts a process (defaults to <see cref="Process.Start(ProcessStartInfo)"/>).</param>
    public LinuxGameLauncher(
        LinuxLaunchHost? host = null,
        IGameProcessFinder? finder = null,
        TimeSpan? gameStartTimeout = null,
        TimeSpan? pollInterval = null,
        Func<ProcessStartInfo, Process?>? startProcess = null)
    {
        _host = host ?? new LinuxLaunchHost();
        _finder = finder ?? new ProcGameProcessFinder();
        _gameStartTimeout = gameStartTimeout ?? DefaultGameStartTimeout;
        _pollInterval = pollInterval ?? DefaultPollInterval;
        _start = startProcess ?? Process.Start;
    }

    /// <summary>
    /// Starts the launch command and returns at once. The game pid is confirmed in the background
    /// and reported through <see cref="GameLaunchResult.GameConfirmed"/>; <paramref name="ct"/>
    /// cancels that background wait.
    /// </summary>
    public Task<GameLaunchResult> LaunchAsync(GameInstall game, LaunchOptions options, CancellationToken ct = default)
    {
        var arguments = options.ToArgumentString();

        var unsupportedNotes = new List<string>();
        if (options.CpuAffinityOptimization)
            unsupportedNotes.Add("CPU affinity optimization is Windows-only and was skipped");
        if (options.NoFog)
            unsupportedNotes.Add("in-memory fog removal is Windows-only and was skipped");

        try
        {
            // DXVK / dgVoodoo2 in the game folder only work if Wine loads them instead of its builtins.
            var dllOverrides = GraphicsModService.WineDllOverrides(game.InstallDir);
            var plan = LinuxLaunchPlanner.BuildGameLaunchPlan(game, arguments, _host, out var error, dllOverrides);
            if (plan is null)
                return Task.FromResult(new GameLaunchResult { Started = false, Detail = error });

            // Game instances that were already running must not be mistaken for the new one.
            var preexisting = new HashSet<int>(_finder.FindProcesses(game.ExecutableName));

            var process = _start(plan.StartInfo);
            if (process is null)
                return Task.FromResult(new GameLaunchResult { Started = false, Detail = $"Process.Start returned null for: {plan.Description}" });

            var detail = plan.Description;
            if (unsupportedNotes.Count > 0)
                detail += " | " + string.Join("; ", unsupportedNotes);

            // Direct Proton/Wine: the started process lives as long as the game, so it is a usable
            // provisional handle. Through Steam it is the Steam client, so report no pid yet.
            return Task.FromResult(new GameLaunchResult
            {
                Started = true,
                ProcessId = plan.ViaSteam ? 0 : SafeId(process),
                ProcessName = plan.ViaSteam ? "" : SafeProcessName(process),
                Detail = detail,
                GameConfirmed = Task.Run(() => ConfirmGameAsync(game, plan, process, preexisting, detail, ct), CancellationToken.None)
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new GameLaunchResult
            {
                Started = false,
                Detail = $"Launch failed: {ex.GetType().Name}: {ex.Message}"
            });
        }
    }

    /// <summary>Background part of <see cref="LaunchAsync"/>: waits for the game exe. Never throws.</summary>
    private async Task<GameLaunchResult> ConfirmGameAsync(
        GameInstall game, LaunchPlan plan, Process process, HashSet<int> preexisting, string detail, CancellationToken ct)
    {
        using (process)
        {
            try
            {
                // The Steam client may exit at once (already running) or keep running (it was
                // just started): neither says anything about the game, so only stop early on a
                // failing direct Proton/Wine launch.
                var gamePid = await _finder.WaitForNewProcessAsync(
                    game.ExecutableName, preexisting, _gameStartTimeout, _pollInterval,
                    giveUp: plan.ViaSteam ? null : () => HasExited(process),
                    ct).ConfigureAwait(false);

                if (gamePid is { } pid)
                {
                    return new GameLaunchResult
                    {
                        Started = true,
                        ProcessId = pid,
                        ProcessName = game.ExecutableName,
                        Detail = detail
                    };
                }

                if (!plan.ViaSteam && HasExited(process) && SafeExitCode(process) is { } code and not 0)
                {
                    return new GameLaunchResult
                    {
                        Started = false,
                        Detail = $"{plan.Route} exited with code {code} before {game.ExecutableName} appeared. {detail}"
                    };
                }

                var trackLauncher = !plan.ViaSteam && !HasExited(process);
                return new GameLaunchResult
                {
                    Started = true,
                    ProcessId = trackLauncher ? SafeId(process) : 0,
                    ProcessName = trackLauncher ? SafeProcessName(process) : "",
                    Detail = $"{detail} | the {game.ExecutableName} process could not be confirmed within "
                           + $"{_gameStartTimeout.TotalSeconds:0} s"
                };
            }
            catch (OperationCanceledException)
            {
                return new GameLaunchResult { Started = true, Detail = detail + " | stopped waiting for the game process (cancelled)" };
            }
            catch (Exception ex)
            {
                return new GameLaunchResult
                {
                    Started = true,
                    Detail = $"{detail} | could not confirm the game process: {ex.GetType().Name}: {ex.Message}"
                };
            }
        }
    }

    /// <summary>
    /// Runs a Windows program (e.g. a mod's Setup.exe) inside the game's prefix:
    /// Proton (in its Steam Linux Runtime) with STEAM_COMPAT_DATA_PATH pointing at the game's compatdata, or Wine
    /// with the resolved WINEPREFIX. Does not wait for the program to finish. Programs that Wine
    /// would load at the wrong address run from a temporary copy (<see cref="FixedBaseWorkaround"/>),
    /// deleted once the started process exits.
    /// </summary>
    public Task<GameLaunchResult> RunInGamePrefixAsync(GameInstall game, string exePath, CancellationToken ct = default)
    {
        string? copy = null;
        try
        {
            copy = FixedBaseWorkaround.CreateCopy(exePath);
            var plan = LinuxLaunchPlanner.BuildPrefixRunPlan(game, copy ?? exePath, _host, out var error);
            if (plan is null)
            {
                DeleteQuietly(copy);
                return Task.FromResult(new GameLaunchResult { Started = false, Detail = error });
            }

            var process = _start(plan.StartInfo);
            if (process is null)
            {
                DeleteQuietly(copy);
                return Task.FromResult(new GameLaunchResult { Started = false, Detail = $"Process.Start returned null for: {plan.Description}" });
            }

            var result = new GameLaunchResult
            {
                Started = true,
                ProcessId = SafeId(process),
                ProcessName = SafeProcessName(process),
                Detail = copy is null
                    ? plan.Description
                    : plan.Description + $" (run from {Path.GetFileName(copy)}, a copy without the ASLR flag that Wine mishandles for this program)"
            };
            if (copy is null)
                process.Dispose();
            else
                _ = DeleteAfterExitAsync(process, copy);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            DeleteQuietly(copy);
            return Task.FromResult(new GameLaunchResult
            {
                Started = false,
                Detail = $"Could not run the installer: {ex.GetType().Name}: {ex.Message}"
            });
        }
    }

    private static async Task DeleteAfterExitAsync(Process process, string file)
    {
        using (process)
        {
            try { await process.WaitForExitAsync(); }
            catch (InvalidOperationException) { /* not ours any more: delete now */ }
        }
        DeleteQuietly(file);
    }

    private static void DeleteQuietly(string? file)
    {
        if (file is null)
            return;
        try { File.Delete(file); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // ---- helpers ----------------------------------------------------------------------------

    private static bool HasExited(Process process)
    {
        try { return process.HasExited; }
        catch { return false; }
    }

    private static int? SafeExitCode(Process process)
    {
        try { return process.ExitCode; }
        catch { return null; }
    }

    private static int SafeId(Process process)
    {
        try { return process.Id; }
        catch { return 0; }
    }

    private static string SafeProcessName(Process process)
    {
        try { return process.ProcessName; }
        catch { return ""; }
    }
}
