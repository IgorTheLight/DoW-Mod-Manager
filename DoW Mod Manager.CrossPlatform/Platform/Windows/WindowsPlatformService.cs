using System.Runtime.Versioning;
using DoW_Mod_Manager.Abstractions;

namespace DoW_Mod_Manager.Platform.Windows;

/// <summary>
/// Aggregate Windows implementation of <see cref="IPlatformService"/>. Wires together the
/// individual Windows services (launcher, file-system, performance, GPU, install locator). Selected by the DI container on Windows hosts via
/// <see cref="WindowsPlatformModule.AddWindowsPlatform"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformService : IPlatformService
{
    public IGameLauncher Launcher { get; } = new WindowsGameLauncher();
    public IFileSystemService FileSystem { get; } = new WindowsFileSystemService();
    public ISystemPerformanceService Performance { get; } = new WindowsPerformanceService();
    public IGpuInfoService Gpu { get; } = new WindowsGpuInfoService();
    public IGameInstallLocator InstallLocator { get; } = new WindowsGameInstallLocator();

    public bool IsWindows => true;
}
