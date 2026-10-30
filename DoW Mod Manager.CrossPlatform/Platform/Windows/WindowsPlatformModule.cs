using System.Runtime.Versioning;
using DoW_Mod_Manager.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DoW_Mod_Manager.Platform.Windows;

/// <summary>
/// DI registration for the Windows platform. The composition root (WS7) calls
/// <see cref="AddWindowsPlatform"/> on Windows hosts in place of the stub registration. The
/// fine-grained sub-service re-exports (<c>IGameLauncher</c>, <c>IFileSystemService</c>, …) are
/// added by the composition root against <see cref="IPlatformService"/>, so this module only has
/// to register the aggregate.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsPlatformModule
{
    public static IServiceCollection AddWindowsPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IPlatformService, WindowsPlatformService>();
        return services;
    }
}
