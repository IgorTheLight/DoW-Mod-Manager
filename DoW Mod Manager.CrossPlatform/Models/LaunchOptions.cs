using System.Collections.Generic;
using System.Text;

namespace DoW_Mod_Manager.Models;

/// <summary>
/// Everything needed to start the game with a mod. Translated to a command line by
/// <see cref="ToArgumentString"/> and consumed by <see cref="Abstractions.IGameLauncher"/>.
/// </summary>
public sealed class LaunchOptions
{
    /// <summary>Module name passed to <c>-modname</c>, e.g. "W40k" for the base game.</summary>
    public string ModName { get; init; } = "W40k";

    public bool Dev { get; init; }
    public bool NoMovies { get; init; }
    public bool ForceHighPoly { get; init; }
    public bool NoPrecacheModels { get; init; }

    /// <summary>Soulstorm-only in-memory fog removal (Windows host only).</summary>
    public bool NoFog { get; init; }

    /// <summary>Pin the game to high priority / CPU affinity 0b0110 after launch (Windows host only).</summary>
    public bool CpuAffinityOptimization { get; init; }

    public IReadOnlyList<string> ExtraArguments { get; init; } = [];

    /// <summary>Builds the Dawn of War command line, reproducing the legacy behaviour exactly.</summary>
    public string ToArgumentString()
    {
        var sb = new StringBuilder("-modname ").Append(ModName);

        if (Dev) sb.Append(" -dev");
        if (NoMovies) sb.Append(" -nomovies");
        if (ForceHighPoly) sb.Append(" -forcehighpoly");
        if (NoPrecacheModels) sb.Append(" -noprecachemodels");

        foreach (var arg in ExtraArguments)
            sb.Append(' ').Append(arg);

        return sb.ToString();
    }
}

/// <summary>Lightweight handle returned after a launch so the UI can track the running game.</summary>
public sealed class GameLaunchResult
{
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = "";

    /// <summary>True when the game process actually started (false on Linux dry runs / errors).</summary>
    public bool Started { get; init; }

    /// <summary>Human-readable note (e.g. the Proton command used) for logging/diagnostics.</summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Set when the launcher keeps confirming the game process in the background (Linux: the
    /// started process is Steam/Proton, not the game). Completes with the final outcome: the game
    /// pid, <see cref="Started"/> = false when the launch command failed early, or a Detail saying
    /// the pid could not be confirmed. Never faults. Null when the result is already final.
    /// </summary>
    public System.Threading.Tasks.Task<GameLaunchResult>? GameConfirmed { get; init; }
}
