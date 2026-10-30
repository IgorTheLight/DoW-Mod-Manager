using DoW_Mod_Manager.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DoW_Mod_Manager.Platform.Linux;

/// <summary>
/// DI wiring for the Linux platform. The composition root calls <see cref="AddLinuxPlatform"/>
/// on non-Windows hosts (WS7 integration). Sub-services are re-exported as singletons so that
/// ViewModels can depend on the fine-grained interfaces directly, exactly as the Windows module
/// and the stub registration do.
/// </summary>
public static class LinuxPlatformModule
{
    public static IServiceCollection AddLinuxPlatform(this IServiceCollection services)
    {
        // Sub-services as shared singletons.
        services.AddSingleton<IGameLauncher, LinuxGameLauncher>();
        services.AddSingleton<IFileSystemService, LinuxFileSystemService>();
        services.AddSingleton<ISystemPerformanceService, LinuxPerformanceService>();
        services.AddSingleton<IGpuInfoService, LinuxGpuInfoService>();
        services.AddSingleton<IGameInstallLocator, LinuxGameInstallLocator>();

        // Aggregate platform service composed from the registered sub-services.
        services.AddSingleton<IPlatformService>(sp => new LinuxPlatformService(
            sp.GetRequiredService<IGameLauncher>(),
            sp.GetRequiredService<IFileSystemService>(),
            sp.GetRequiredService<ISystemPerformanceService>(),
            sp.GetRequiredService<IGpuInfoService>(),
            sp.GetRequiredService<IGameInstallLocator>()));

        return services;
    }
}
