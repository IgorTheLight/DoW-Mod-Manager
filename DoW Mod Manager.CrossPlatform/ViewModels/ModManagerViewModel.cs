using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Services;

namespace DoW_Mod_Manager.ViewModels;

/// <summary>
/// The main screen — a faithful MVVM port of the legacy <c>ModManagerForm</c> core: discover the
/// install(s), list playable mods, show required-mod status, and launch the game (directly on
/// Windows, via Proton/Wine on Linux) with the selected options.
/// </summary>
public partial class ModManagerViewModel : ViewModelBase
{
    private readonly IPlatformService _platform;
    private readonly IGameInstallLocator _locator;
    private readonly IModService _mods;
    private readonly IGameLauncher _launcher;
    private readonly IFileSystemService _files;
    private readonly IWarningsLogService _warnings;
    private readonly ILaaService _laa;
    private readonly ISettingsStore _settings;
    private readonly IDialogService _dialogs;
    private readonly IFeatureViewModelFactory _features;
    private readonly IGameExtrasService _extras;
    private readonly IGraphicsModService _graphics;

    /// <summary>Cancels the background game-process confirmation of the previous launch.</summary>
    private CancellationTokenSource? _launchTracking;

    public ObservableCollection<GameInstall> Installs { get; } = [];
    public ObservableCollection<ModInfo> Mods { get; } = [];
    public ObservableCollection<ModDependencyStatus> RequiredMods { get; } = [];

    [ObservableProperty] private GameInstall? _selectedInstall;
    [ObservableProperty] private ModInfo? _selectedMod;

    [NotifyCanExecuteChangedFor(nameof(FixMissingModCommand))]
    [ObservableProperty] private ModDependencyStatus? _selectedRequiredMod;

    [ObservableProperty] private string _manualPath = "";
    [ObservableProperty] private string _status = "Discovering Dawn of War installations…";
    [NotifyPropertyChangedFor(nameof(IsLaaEnabled))]
    [NotifyPropertyChangedFor(nameof(IsLaaDisabled))]
    [ObservableProperty] private string _laaStatus = "";

    /// <summary>Colours the LAA status label green or red, like the legacy UI.</summary>
    public bool IsLaaEnabled => LaaStatus == "LAA: Enabled";
    public bool IsLaaDisabled => LaaStatus == "LAA: Disabled";

    /// <summary>False for 64-bit (Definitive Edition) or unreadable executables — the toggle is disabled.</summary>
    [NotifyCanExecuteChangedFor(nameof(ToggleLaaCommand))]
    [ObservableProperty] private bool _laaApplicable;

    // Optional game tweaks (legacy camera / larger fonts / DXVK / dgVoodoo2 buttons).
    [NotifyPropertyChangedFor(nameof(CameraLabel))]
    [ObservableProperty] private bool _isCameraInstalled;

    [NotifyPropertyChangedFor(nameof(LargerFontsLabel))]
    [ObservableProperty] private bool _isLargerFontsInstalled;

    [NotifyPropertyChangedFor(nameof(DxvkLabel))]
    [ObservableProperty] private GraphicsWrapperState _dxvkState;

    [NotifyPropertyChangedFor(nameof(DgVoodoo2Label))]
    [ObservableProperty] private GraphicsWrapperState _dgVoodoo2State;

    public string CameraLabel => IsCameraInstalled ? "Remove better camera" : "Install better camera";
    public string LargerFontsLabel => IsLargerFontsInstalled ? "Remove larger fonts" : "Install larger fonts";
    public string DxvkLabel => WrapperLabel(DxvkState, "DXVK", "Remove");
    public string DgVoodoo2Label => WrapperLabel(DgVoodoo2State, "dgVoodoo2", "Delete");

    private static string WrapperLabel(GraphicsWrapperState state, string name, string removeVerb) => state switch
    {
        GraphicsWrapperState.Installed => $"{removeVerb} {name}",
        GraphicsWrapperState.UpdateAvailable => $"Update {name}",
        _ => $"Install {name}"
    };

    // Launch options (persisted to DoW Mod Manager.ini).
    [ObservableProperty] private bool _dev;
    [ObservableProperty] private bool _noMovies = true;
    [ObservableProperty] private bool _forceHighPoly;
    [ObservableProperty] private bool _noPrecacheModels;

    [NotifyPropertyChangedFor(nameof(NoFogEnabled))]
    [ObservableProperty] private bool _noFog;

    [ObservableProperty] private bool _cpuAffinityOptimization;

    /// <summary>Definitive Edition: Soulstorm folder whose mods are listed too (edited, then applied).</summary>
    [ObservableProperty] private string _soulstormDir = "";
    [ObservableProperty] private bool _isGogVersion;

    [NotifyCanExecuteChangedFor(nameof(StartModCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartBaseGameCommand))]
    [ObservableProperty] private bool _busy;

    public string PlatformName => _platform.IsWindows ? "Windows" : "Linux / Proton";

    /// <summary>NoFog only works on Soulstorm and only with a Windows host (process-memory patch).</summary>
    public bool NoFogEnabled => SelectedInstall?.SupportsNoFog == true && _platform.IsWindows;

    public bool PerfTweaksSupported => _platform.IsWindows;

    public bool IsDefinitiveEdition => SelectedInstall?.Variant == GameVariant.DefinitiveEdition;

    /// <summary>Green check (all required mods installed) or red cross next to START MOD, like the legacy UI.</summary>
    public bool HasRequiredMods => RequiredMods.Count > 0;
    public bool AllRequiredModsInstalled => HasRequiredMods && RequiredMods.All(d => d.IsInstalled);
    public bool SomeRequiredModsMissing => HasRequiredMods && !AllRequiredModsInstalled;

    /// <summary>
    /// Tooltip of the DXVK / dgVoodoo2 buttons. On Linux the files replace Proton's own DXVK: the
    /// launcher adds the Wine DLL overrides for the direct Proton/Wine routes, Steam's Proton loads
    /// game-folder DLLs on its own.
    /// </summary>
    public string GraphicsWrapperHint => _platform.IsWindows
        ? "Drops the wrapper DLLs into the game folder. DXVK and dgVoodoo2 both provide d3d9.dll, so installing one removes the other."
        : "Drops the wrapper DLLs into the game folder; they take precedence over Proton's built-in DXVK. DXVK and dgVoodoo2 both provide d3d9.dll, so installing one removes the other.";

    public ModManagerViewModel(
        IPlatformService platform,
        IGameInstallLocator locator,
        IModService mods,
        IGameLauncher launcher,
        IFileSystemService files,
        IWarningsLogService warnings,
        ILaaService laa,
        ISettingsStore settings,
        IDialogService dialogs,
        IFeatureViewModelFactory features,
        IGameExtrasService extras,
        IGraphicsModService graphics)
    {
        _platform = platform;
        _locator = locator;
        _mods = mods;
        _launcher = launcher;
        _files = files;
        _warnings = warnings;
        _laa = laa;
        _settings = settings;
        _dialogs = dialogs;
        _features = features;
        _extras = extras;
        _graphics = graphics;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var found = new List<GameInstall>(await _locator.DiscoverAsync());

            // Also honour the classic "manager sits in the game folder" mode.
            var here = _locator.IdentifyInstall(Directory.GetCurrentDirectory());
            if (here is not null && found.All(g => g.InstallDir != here.InstallDir))
                found.Insert(0, here);

            Installs.Clear();
            foreach (var g in found)
                Installs.Add(WithUserSettings(g));

            if (Installs.Count > 0)
            {
                SelectedInstall = Installs[0];
                Status = $"Found {Installs.Count} installation(s).";
            }
            else
            {
                Status = "No Dawn of War installation found. Enter the game folder path below and press Load.";
            }
        }
        catch (Exception ex)
        {
            Status = $"Discovery failed: {ex.Message}";
        }
    }

    partial void OnSelectedInstallChanged(GameInstall? value)
    {
        OnPropertyChanged(nameof(NoFogEnabled));
        OnPropertyChanged(nameof(IsDefinitiveEdition));
        if (value is null)
            return;

        SoulstormDir = value.SoulstormModsDir ?? "";

        LoadSettings(value);
        LoadMods(value);
        RefreshLaaStatus(value);
        _ = RefreshExtrasAsync(value);
    }

    partial void OnSelectedModChanged(ModInfo? value) => ResolveDependencies();

    private void LoadMods(GameInstall install, string? select = null)
    {
        Mods.Clear();
        RequiredMods.Clear();
        try
        {
            foreach (var mod in _mods.GetPlayableMods(install))
                Mods.Add(mod);

            SelectedMod = Mods.FirstOrDefault(m => m.Name == select) ?? Mods.FirstOrDefault();
            if (Mods.Count == 0)
                Status = "No playable mods found in this installation.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to read mods: {ex.Message}";
        }
    }

    private void ResolveDependencies()
    {
        RequiredMods.Clear();
        if (SelectedInstall is not null && SelectedMod is not null)
        {
            try
            {
                foreach (var dep in _mods.ResolveDependencies(SelectedInstall, SelectedMod))
                    RequiredMods.Add(dep);
            }
            catch (Exception ex)
            {
                Status = $"Failed to resolve dependencies: {ex.Message}";
            }
        }

        StartModCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasRequiredMods));
        OnPropertyChanged(nameof(AllRequiredModsInstalled));
        OnPropertyChanged(nameof(SomeRequiredModsMissing));
    }

    private void LoadSettings(GameInstall install)
    {
        var s = _settings.Read(install.InstallDir);
        Dev = s[SettingsKeys.Dev] == 1;
        NoMovies = s[SettingsKeys.NoMovies] == 1;
        ForceHighPoly = s[SettingsKeys.ForceHighPoly] == 1;
        NoPrecacheModels = s[SettingsKeys.NoPrecacheModels] == 1;
        NoFog = s[SettingsKeys.RemoveMapFog] == 1 && NoFogEnabled;
        CpuAffinityOptimization = s[SettingsKeys.DowOptimizations] == 1;
        IsGogVersion = s[SettingsKeys.IsGOGVersion] == 1;
    }

    private void SaveSettings(GameInstall install)
    {
        var s = _settings.Read(install.InstallDir);
        s[SettingsKeys.Dev] = Dev ? 1 : 0;
        s[SettingsKeys.NoMovies] = NoMovies ? 1 : 0;
        s[SettingsKeys.ForceHighPoly] = ForceHighPoly ? 1 : 0;
        s[SettingsKeys.NoPrecacheModels] = NoPrecacheModels ? 1 : 0;
        s[SettingsKeys.RemoveMapFog] = NoFog ? 1 : 0;
        s[SettingsKeys.DowOptimizations] = CpuAffinityOptimization ? 1 : 0;
        s[SettingsKeys.IsGOGVersion] = IsGogVersion ? 1 : 0;
        _settings.Write(install.InstallDir, s);
    }

    private LaunchOptions BuildOptions(string modName) => new()
    {
        ModName = modName,
        Dev = Dev,
        NoMovies = NoMovies,
        ForceHighPoly = ForceHighPoly,
        NoPrecacheModels = NoPrecacheModels,
        NoFog = NoFog && NoFogEnabled,
        CpuAffinityOptimization = CpuAffinityOptimization && PerfTweaksSupported,
    };

    private bool CanStartMod =>
        !Busy && SelectedInstall is not null && SelectedMod is not null &&
        RequiredMods.All(r => r.IsInstalled);

    private bool CanStartBaseGame => !Busy && SelectedInstall is not null;

    [RelayCommand(CanExecute = nameof(CanStartMod))]
    private Task StartModAsync() => LaunchAsync(SelectedMod!.Name);

    [RelayCommand(CanExecute = nameof(CanStartBaseGame))]
    private Task StartBaseGameAsync() => LaunchAsync(SelectedInstall!.BaseGameModuleName);

    private async Task LaunchAsync(string modName)
    {
        if (SelectedInstall is null)
            return;

        Busy = true;
        try
        {
            SaveSettings(SelectedInstall);
            Status = $"Launching {modName}…";

            _launchTracking?.Cancel();
            _launchTracking?.Dispose();
            _launchTracking = new CancellationTokenSource();

            var result = await _launcher.LaunchAsync(SelectedInstall, BuildOptions(modName), _launchTracking.Token);
            if (!await ShowLaunchResultAsync(modName, result))
                return;

            if (result.GameConfirmed is { } confirmed)
            {
                Status = $"Started {modName}, waiting for the game process… {result.Detail}";
                // Runs after Busy is reset, so the Start buttons are usable while we wait.
                _ = TrackGameAsync(modName, confirmed);
            }
        }
        catch (Exception ex)
        {
            Status = $"Launch error: {ex.Message}";
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>Shows a launch outcome in the status line (and a dialog on failure). Returns Started.</summary>
    private async Task<bool> ShowLaunchResultAsync(string modName, GameLaunchResult result)
    {
        if (result.Started)
        {
            // ProcessId is 0 when the game was started through Steam and its pid is not (yet) confirmed.
            Status = result.ProcessId > 0
                ? $"Started {modName} (pid {result.ProcessId}). {result.Detail}"
                : $"Started {modName}. {result.Detail}";
            return true;
        }

        Status = $"Launch failed: {result.Detail}";
        await _dialogs.ShowMessageAsync($"Could not launch {modName}.\n\n{result.Detail}", "Launch failed");
        return false;
    }

    /// <summary>Reports the background game-process confirmation; never throws.</summary>
    private async Task TrackGameAsync(string modName, Task<GameLaunchResult> confirmed)
    {
        try
        {
            await ShowLaunchResultAsync(modName, await confirmed);
        }
        catch (Exception ex)
        {
            Status = $"Could not confirm that {modName} started: {ex.Message}";
        }
    }

    /// <summary>Stops waiting for a launched game's process.</summary>
    public override void OnClosed() => _launchTracking?.Cancel();

    [RelayCommand]
    private void LoadManualPath()
    {
        if (string.IsNullOrWhiteSpace(ManualPath))
            return;

        var install = _locator.IdentifyInstall(ManualPath.Trim());
        if (install is null)
        {
            Status = $"No Dawn of War executable found in \"{ManualPath}\".";
            return;
        }

        var known = Installs.FirstOrDefault(g => g.InstallDir == install.InstallDir);
        if (known is null)
        {
            known = WithUserSettings(install);
            Installs.Add(known);
        }
        SelectedInstall = known;
        Status = $"Loaded {install.Variant} from {install.InstallDir}.";
    }

    /// <summary>Applies per-install settings that shape the install itself (the DE Soulstorm folder).</summary>
    private GameInstall WithUserSettings(GameInstall install)
    {
        if (install.Variant != GameVariant.DefinitiveEdition)
            return install;
        try
        {
            return install.WithSoulstormModsDir(_settings.ReadString(install.InstallDir, SettingsKeys.SoulstormDir));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return install;
        }
    }

    /// <summary>
    /// Saves <see cref="SoulstormDir"/> for the selected Definitive Edition install (empty clears it)
    /// and reloads the mod list. The folder must be a separate install: the Definitive Edition's
    /// own folders are refused, as in the DE manager.
    /// </summary>
    [RelayCommand]
    private void ApplySoulstormDir()
    {
        if (SelectedInstall is not { Variant: GameVariant.DefinitiveEdition } install)
            return;

        string? dir = string.IsNullOrWhiteSpace(SoulstormDir) ? null : SoulstormDir.Trim();
        string note = "";
        if (dir is not null)
        {
            if (!Directory.Exists(dir))
            {
                Status = $"The Soulstorm folder \"{dir}\" does not exist.";
                return;
            }

            if (new[] { install.InstallDir, install.UserDataDir, install.ModsDir }.Any(d => SameDirectory(d, dir)))
            {
                Status = "Pick a separate Soulstorm installation, not one of the Definitive Edition's own folders.";
                return;
            }

            if (GamePaths.ModuleFiles(dir).Count == 0)
                note = " It has no .module files yet.";
        }

        try
        {
            _settings.WriteString(install.InstallDir, SettingsKeys.SoulstormDir, dir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not save the Soulstorm folder: {ex.Message}";
            return;
        }

        var updated = install.WithSoulstormModsDir(dir);
        int index = Installs.IndexOf(install);
        if (index >= 0)
            Installs[index] = updated;
        SelectedInstall = updated;
        Status = dir is null
            ? "Soulstorm mods are no longer listed."
            : $"Mods from {dir} are listed too.{note}";
    }

    private bool SameDirectory(string? a, string b)
    {
        if (string.IsNullOrEmpty(a))
            return false;
        static string Normalise(string p) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p));
        return string.Equals(Normalise(a), Normalise(b),
            _platform.IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    [RelayCommand]
    private void OpenModFolder()
    {
        if (SelectedInstall is null)
            return;

        var folder = SelectedMod is { ModFolder.Length: > 0 }
            // ModFolder is relative to the .module's directory (install dir or DE Mods folder).
            ? GamePaths.Resolve(ModService.ModRoot(SelectedInstall, SelectedMod), SelectedMod.ModFolder)
            : SelectedInstall.InstallDir;

        if (Directory.Exists(folder))
            _files.OpenFolder(folder);
        else
            _files.OpenFolder(SelectedInstall.InstallDir);
    }

    [RelayCommand]
    private void CheckForErrors()
    {
        if (SelectedInstall is null)
            return;

        var errors = _warnings.GetFatalDataErrors(SelectedInstall.UserDataDir);
        Status = errors.Count > 0
            ? $"{errors.Count} fatal data error(s):\n{string.Join('\n', errors)}"
            : "No fatal errors found in warnings.log.";
    }

    [RelayCommand(CanExecute = nameof(LaaApplicable))]
    private void ToggleLaa()
    {
        if (SelectedInstall is null || !LaaApplicable)
            return;

        try
        {
            bool now = _laa.ToggleLAA(SelectedInstall.ExecutablePath, IsGogVersion);
            LaaStatus = now ? "LAA: Enabled" : "LAA: Disabled";
            Status = $"Large-Address-Aware {(now ? "enabled" : "disabled")} for {SelectedInstall.ExecutableName}.";
        }
        catch (LaaNotApplicableException ex)
        {
            Status = ex.Message;
            RefreshLaaStatus(SelectedInstall);
        }
        catch (Exception ex)
        {
            Status = $"LAA toggle failed: {ex.Message}";
        }
    }

    private void RefreshLaaStatus(GameInstall install)
    {
        var applicability = _laa.GetLaaApplicability(install.ExecutablePath);
        LaaApplicable = applicability == LaaApplicability.Applicable;

        switch (applicability)
        {
            case LaaApplicability.NotNeeded64Bit:
                LaaStatus = "LAA: not needed (64-bit)";
                return;
            case LaaApplicability.UnsupportedImage:
                LaaStatus = "LAA: n/a (not a 32-bit exe)";
                return;
            case LaaApplicability.FileUnavailable:
                LaaStatus = "LAA: unknown";
                return;
        }

        try
        {
            bool aware = _laa.IsLargeAddressAware(install.ExecutablePath, IsGogVersion);
            LaaStatus = aware ? "LAA: Enabled" : "LAA: Disabled";
        }
        catch
        {
            LaaApplicable = false;
            LaaStatus = "LAA: unknown";
        }
    }

    // ------------------------------------------------------------------ secondary windows

    /// <summary>Re-reads the mod list (e.g. after the merger created a <c>_MERGED</c> module).</summary>
    private void ReloadMods()
    {
        if (SelectedInstall is not null)
            LoadMods(SelectedInstall, SelectedMod?.Name);
    }

    /// <summary>Settings presets can toggle -forcehighpoly / -noprecachemodels (legacy ChangeSetting).</summary>
    private void ApplyLaunchFlags(LaunchFlagChange change)
    {
        if (change.ForceHighPoly is bool highPoly)
            ForceHighPoly = highPoly;
        if (change.NoPrecacheModels is bool noPrecache)
            NoPrecacheModels = noPrecache;
        if (SelectedInstall is not null)
            SaveSettings(SelectedInstall);
    }

    [RelayCommand]
    private void OpenModMerger()
    {
        if (SelectedInstall is not null)
            _dialogs.ShowWindow(_features.CreateModMerger(SelectedInstall, ReloadMods));
    }

    [RelayCommand]
    private void OpenModDownloader()
    {
        if (SelectedInstall is not null)
            _dialogs.ShowWindow(_features.CreateModDownloader(SelectedInstall, ReloadMods));
    }

    private bool CanFixMissingMod => SelectedRequiredMod is { IsInstalled: false };

    [RelayCommand(CanExecute = nameof(CanFixMissingMod))]
    private async Task FixMissingModAsync()
    {
        if (SelectedInstall is null || SelectedRequiredMod is null)
            return;

        string name = SelectedRequiredMod.RequiredModName.Replace(".module", "").Trim();
        if (BaseModules.Contains(name))
        {
            await _dialogs.ShowMessageAsync("You are missing one of the base modules! Reinstall the game to fix it.", "Warning!");
            return;
        }

        _dialogs.ShowWindow(_features.CreateModDownloader(SelectedInstall, ReloadMods, name));
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (SelectedInstall is not null)
            _dialogs.ShowWindow(_features.CreateSettings(SelectedInstall, ApplyLaunchFlags));
    }

    [RelayCommand]
    private void OpenAbout() => _dialogs.ShowWindow(_features.CreateAbout());

    // ------------------------------------------------------------------ optional tweaks

    private async Task RefreshExtrasAsync(GameInstall install, bool checkForUpdates = true)
    {
        IsCameraInstalled = _extras.IsCameraInstalled(install.InstallDir);
        IsLargerFontsInstalled = _extras.IsLargerFontsInstalled(install.InstallDir);

        var s = _settings.Read(install.InstallDir);
        DxvkState = await WrapperStateAsync(GraphicsWrapper.Dxvk, s[SettingsKeys.DXVKUpdateCheck] == 1);
        DgVoodoo2State = await WrapperStateAsync(GraphicsWrapper.DgVoodoo2, s[SettingsKeys.DgVoodoo2UpdateCheck] == 1);

        async Task<GraphicsWrapperState> WrapperStateAsync(GraphicsWrapper wrapper, bool updateCheck)
        {
            try
            {
                return await _graphics.GetStateAsync(install.InstallDir, wrapper, checkForUpdates && updateCheck);
            }
            catch
            {
                return GraphicsWrapperState.NotInstalled;
            }
        }
    }

    [RelayCommand]
    private Task ToggleCameraAsync() => ToggleExtraAsync(
        IsCameraInstalled, "better camera",
        dir => _extras.InstallCameraAsync(dir),
        dir => { _extras.RemoveCamera(dir); return Task.CompletedTask; });

    [RelayCommand]
    private Task ToggleLargerFontsAsync() => ToggleExtraAsync(
        IsLargerFontsInstalled, "larger fonts",
        dir => _extras.InstallLargerFontsAsync(dir),
        dir => { _extras.RemoveLargerFonts(dir); return Task.CompletedTask; });

    // As in the legacy buttons, "Update" re-downloads (installed == false) and "Remove" deletes.
    [RelayCommand]
    private Task ToggleDxvkAsync() => ToggleExtraAsync(
        DxvkState == GraphicsWrapperState.Installed, "DXVK",
        dir => _graphics.InstallAsync(dir, GraphicsWrapper.Dxvk),
        dir => { _graphics.Remove(dir, GraphicsWrapper.Dxvk); return Task.CompletedTask; });

    [RelayCommand]
    private Task ToggleDgVoodoo2Async() => ToggleExtraAsync(
        DgVoodoo2State == GraphicsWrapperState.Installed, "dgVoodoo2",
        dir => _graphics.InstallAsync(dir, GraphicsWrapper.DgVoodoo2),
        dir => { _graphics.Remove(dir, GraphicsWrapper.DgVoodoo2); return Task.CompletedTask; },
        "Best way to use it: in Settings > Video disable antialiasing and pick 1280x960 or 1024x768, so the UI is not stretched.");

    private async Task ToggleExtraAsync(bool installed, string name, Func<string, Task> install, Func<string, Task> remove,
        string? installedHint = null)
    {
        if (SelectedInstall is null || Busy)
            return;

        var game = SelectedInstall;
        Busy = true;
        try
        {
            if (installed)
            {
                await remove(game.InstallDir);
                Status = $"The {name} tweak is disabled and deleted.";
            }
            else
            {
                Status = $"Downloading {name}…";
                await install(game.InstallDir);
                Status = $"The {name} tweak is downloaded and enabled." + (installedHint is null ? "" : " " + installedHint);
            }
        }
        catch (Exception ex)
        {
            Status = $"Could not change {name}: {ex.Message}";
        }
        finally
        {
            Busy = false;
            await RefreshExtrasAsync(game, checkForUpdates: false);
        }
    }
}
