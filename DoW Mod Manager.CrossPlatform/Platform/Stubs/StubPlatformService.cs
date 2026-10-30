using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Platform.Stubs;

/// <summary>
/// Safe no-op platform implementation used until the real Windows (WS1) / Linux (WS2)
/// services are wired in by WS7. Lets the Avalonia shell build and run on any OS without
/// crashing — every operation degrades gracefully instead of throwing.
/// </summary>
public sealed class StubPlatformService : IPlatformService
{
    public IGameLauncher Launcher { get; } = new StubGameLauncher();
    public IFileSystemService FileSystem { get; } = new StubFileSystemService();
    public ISystemPerformanceService Performance { get; } = new StubPerformanceService();
    public IGpuInfoService Gpu { get; } = new StubGpuInfoService();
    public IGameInstallLocator InstallLocator { get; } = new StubInstallLocator();
    public bool IsWindows => System.OperatingSystem.IsWindows();
}

internal sealed class StubGameLauncher : IGameLauncher
{
    public Task<GameLaunchResult> LaunchAsync(GameInstall game, LaunchOptions options, CancellationToken ct = default)
        => Task.FromResult(new GameLaunchResult { Started = false, Detail = "No platform launcher wired (stub)." });

    public Task<GameLaunchResult> RunInGamePrefixAsync(GameInstall game, string exePath, CancellationToken ct = default)
        => Task.FromResult(new GameLaunchResult { Started = false, Detail = "No platform launcher wired (stub)." });
}

internal sealed class StubFileSystemService : IFileSystemService
{
    public void OpenFolder(string path) { }
    public void ShowFileProperties(string path) { }
    public void CreateShortcut(ShortcutSpec spec) { }
}

internal sealed class StubPerformanceService : ISystemPerformanceService
{
    public PerfCapabilities GetCapabilities() => PerfCapabilities.None;
    public TimerResolutionInfo? GetTimerResolution() => null;
    public void SetTimerResolution(double milliseconds) { }
    public void ResetTimerResolution() { }
    public void ApplyCompatibilityProfile(GameInstall game, PerfProfile profile) { }
    public PerfProfile GetCompatibilityProfile(GameInstall game) => new();
    public string? GetActivePowerPlan() => null;
    public void SetPowerPlan(string planName) { }
    public void UnlockUltimatePerformancePlan() { }
    public void OpenPowerOptions() { }
}

internal sealed class StubGpuInfoService : IGpuInfoService
{
    public Task<IReadOnlyList<GpuInfo>> GetGpusAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<GpuInfo>>([]);
}

internal sealed class StubInstallLocator : IGameInstallLocator
{
    public Task<IReadOnlyList<GameInstall>> DiscoverAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<GameInstall>>([]);
    public GameInstall? IdentifyInstall(string directory) => null;
}
