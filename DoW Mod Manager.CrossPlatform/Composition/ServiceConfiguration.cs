using System;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Platform.Linux;
using DoW_Mod_Manager.Platform.Stubs;
#if WINDOWS
using DoW_Mod_Manager.Platform.Windows;
#endif
using DoW_Mod_Manager.Services;
using DoW_Mod_Manager.ViewModels;
using DoW_Mod_Manager.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DoW_Mod_Manager.Composition;

/// <summary>
/// Single composition root for the whole application. WS1/WS2 add their platform registration
/// extension methods; WS3 adds the cross-platform services; WS5 adds the feature ViewModels.
/// </summary>
public static class ServiceConfiguration
{
    public static IServiceCollection AddDowModManager(this IServiceCollection services)
    {
        services.AddPlatformServices();
        services.AddCoreServices();
        services.AddViewModels();
        return services;
    }

    /// <summary>
    /// Selects the OS-specific <see cref="IPlatformService"/>: <c>WindowsPlatformService</c>
    /// (WS1) on Windows, <c>LinuxPlatformService</c> (WS2) on Linux, otherwise a safe no-op
    /// stub. The Windows registration is behind <c>#if WINDOWS</c> because its P/Invoke code
    /// only compiles under the net10.0-windows target.
    /// </summary>
    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
#if WINDOWS
        if (OperatingSystem.IsWindows())
            services.AddWindowsPlatform();
        else
#endif
        if (OperatingSystem.IsLinux())
            services.AddLinuxPlatform();
        else
            services.AddSingleton<IPlatformService, StubPlatformService>();

        // Convenience: expose the sub-services directly so ViewModels can take fine-grained deps.
        // TryAdd: the Linux module already registers the sub-services and builds IPlatformService
        // from them, so re-registering them through IPlatformService would be a resolution cycle.
        services.TryAddSingleton(sp => sp.GetRequiredService<IPlatformService>().Launcher);
        services.TryAddSingleton(sp => sp.GetRequiredService<IPlatformService>().FileSystem);
        services.TryAddSingleton(sp => sp.GetRequiredService<IPlatformService>().Performance);
        services.TryAddSingleton(sp => sp.GetRequiredService<IPlatformService>().Gpu);
        services.TryAddSingleton(sp => sp.GetRequiredService<IPlatformService>().InstallLocator);
        return services;
    }

    /// <summary>Cross-platform domain services (WS3: parsers, mod/download/settings/LAA services).</summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
        => services.AddDomainServices();

    /// <summary>MVVM ViewModels (WS4 shell + WS5 feature windows).</summary>
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<ModManagerViewModel>();
        services.AddSingleton<IFeatureViewModelFactory, FeatureViewModelFactory>();
        services.AddSingleton<IDialogService, DialogService>();
        return services;
    }
}
