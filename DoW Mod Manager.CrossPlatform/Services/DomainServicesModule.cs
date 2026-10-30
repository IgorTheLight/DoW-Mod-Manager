using Microsoft.Extensions.DependencyInjection;

namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Registers the WS3 cross-platform domain services. The orchestrator calls
    /// <see cref="AddDomainServices"/> from <c>AddCoreServices</c> during integration.
    /// </summary>
    public static class DomainServicesModule
    {
        public static IServiceCollection AddDomainServices(this IServiceCollection services)
        {
            services.AddSingleton<IModuleParser, ModuleParser>();
            services.AddSingleton<IModService, ModService>();
            services.AddSingleton<IWarningsLogService, WarningsLogService>();
            services.AddSingleton<ILaaService, LaaService>();
            services.AddSingleton<ISettingsStore, IniSettingsStore>();
            services.AddSingleton<IModMergerService, ModMergerService>();
            services.AddSingleton<IGameSettingsService, GameSettingsService>();
            services.AddSingleton<IFontsService, FontsService>();

            // Typed HttpClient for the download service (cross-platform).
            services.AddHttpClient<IDownloadService, DownloadService>();
            services.AddSingleton<IModlistService, ModlistService>();
            services.AddSingleton<IGameExtrasService, GameExtrasService>();
            services.AddSingleton<IGraphicsModService, GraphicsModService>();
            services.AddSingleton<IModInstallService, ModInstallService>();

            return services;
        }
    }
}
