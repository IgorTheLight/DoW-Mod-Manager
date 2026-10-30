using DoW_Mod_Manager.Abstractions;

namespace DoW_Mod_Manager.Platform.Linux;

/// <summary>
/// Aggregate <see cref="IPlatformService"/> for Linux hosts. Wires the Proton/Wine launcher,
/// Steam install discovery, xdg/.desktop file operations, GPU enumeration and the no-op
/// performance service. Selected by the DI container via
/// <see cref="LinuxPlatformModule.AddLinuxPlatform"/> on non-Windows hosts.
/// </summary>
public sealed class LinuxPlatformService : IPlatformService
{
    public IGameLauncher Launcher { get; }
    public IFileSystemService FileSystem { get; }
    public ISystemPerformanceService Performance { get; }
    public IGpuInfoService Gpu { get; }
    public IGameInstallLocator InstallLocator { get; }

    public bool IsWindows => false;

    /// <summary>Parameterless constructor used when resolved directly (no DI).</summary>
    public LinuxPlatformService()
        : this(new LinuxGameLauncher(),
               new LinuxFileSystemService(),
               new LinuxPerformanceService(),
               new LinuxGpuInfoService(),
               new LinuxGameInstallLocator())
    {
    }

    /// <summary>DI-friendly constructor; the module injects the shared sub-service singletons.</summary>
    public LinuxPlatformService(
        IGameLauncher launcher,
        IFileSystemService fileSystem,
        ISystemPerformanceService performance,
        IGpuInfoService gpu,
        IGameInstallLocator installLocator)
    {
        Launcher = launcher;
        FileSystem = fileSystem;
        Performance = performance;
        Gpu = gpu;
        InstallLocator = installLocator;
    }
}
