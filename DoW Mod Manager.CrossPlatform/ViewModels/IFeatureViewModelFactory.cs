using System;
using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Services;

namespace DoW_Mod_Manager.ViewModels;

/// <summary>
/// Creates the secondary-window ViewModels with their runtime context (the selected install,
/// callbacks into the main screen) while the DI container supplies the services.
/// </summary>
public interface IFeatureViewModelFactory
{
    ModMergerViewModel CreateModMerger(GameInstall install, Action onModulesChanged);
    ModDownloaderViewModel CreateModDownloader(GameInstall install, Action onModulesChanged, string moduleFilter = "");
    SettingsViewModel CreateSettings(GameInstall install, Action<LaunchFlagChange> applyLaunchFlags);
    SystemPerformanceViewModel CreateSystemPerformance(GameInstall install);
    FontsManagerViewModel CreateFontsManager(GameInstall install, string gameScreenWidth);
    AboutViewModel CreateAbout();
}
