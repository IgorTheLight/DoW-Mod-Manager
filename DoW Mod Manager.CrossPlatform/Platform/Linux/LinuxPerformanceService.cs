using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Platform.Linux;

/// <summary>
/// System performance tweaks are a clean no-op on Linux. The Windows manager twiddles the NT
/// timer resolution, power plans and AppCompatFlags registry keys; Linux does not expose those
/// concepts the same way (timer resolution is kernel-governed, power management is handled by
/// TLP / the desktop environment, and Wine/Proton have no AppCompat registry), so every
/// capability is reported as unavailable and the operations do nothing.
/// </summary>
public sealed class LinuxPerformanceService : ISystemPerformanceService
{
    public PerfCapabilities GetCapabilities() => PerfCapabilities.None;

    // No NT timer-resolution API on Linux.
    public TimerResolutionInfo? GetTimerResolution() => null;
    public void SetTimerResolution(double milliseconds) { }
    public void ResetTimerResolution() { }

    // No AppCompatFlags registry on Linux; compatibility is handled by the Proton prefix.
    public void ApplyCompatibilityProfile(GameInstall game, PerfProfile profile) { }
    public PerfProfile GetCompatibilityProfile(GameInstall game) => new();

    // Power plans are managed by the distro / desktop environment, not by this app.
    public string? GetActivePowerPlan() => null;
    public void SetPowerPlan(string planName) { }
    public void UnlockUltimatePerformancePlan() { }
    public void OpenPowerOptions() { }
}
