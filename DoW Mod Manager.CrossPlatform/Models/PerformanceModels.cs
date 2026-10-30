using System.Collections.Generic;

namespace DoW_Mod_Manager.Models;

/// <summary>
/// What the host platform can actually do regarding performance tweaks. On Linux every
/// flag is <c>false</c> / empty so the UI can grey out the relevant controls.
/// </summary>
public sealed class PerfCapabilities
{
    public bool SupportsTimerResolution { get; init; }
    public bool SupportsPowerPlans { get; init; }
    public bool SupportsCompatibilityFlags { get; init; }

    /// <summary>Power plans available on this machine (Windows). Empty on Linux.</summary>
    public IReadOnlyList<string> AvailablePowerPlans { get; init; } = [];

    /// <summary>True when the "Ultimate Performance" plan can be unlocked (Windows).</summary>
    public bool CanUnlockUltimatePerformance { get; init; }

    public static PerfCapabilities None => new();
}

/// <summary>Current NT timer-resolution readings, all expressed in milliseconds.</summary>
public sealed class TimerResolutionInfo
{
    public double MinimumMs { get; init; }
    public double MaximumMs { get; init; }
    public double CurrentMs { get; init; }
}

/// <summary>
/// The Windows "Compatibility" tab settings for the game executable, persisted in the
/// AppCompatFlags registry key. No-op on Linux.
/// </summary>
public sealed class PerfProfile
{
    public bool FullscreenOptimizationsDisabled { get; init; }
    public bool RunAsAdmin { get; init; }
    public bool HighDpiAware { get; init; }
    public bool Win7CompatibilityMode { get; init; }
}

/// <summary>An installed graphics adapter.</summary>
public sealed class GpuInfo
{
    public required string Description { get; init; }
    public string? Driver { get; init; }

    public override string ToString() => Description;
}
