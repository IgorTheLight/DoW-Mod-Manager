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

/// <summary>A mod of the curated list, with whether it is installed in the current game.</summary>
public partial class ModListEntry(DownloadableMod mod) : ObservableObject
{
    public DownloadableMod Mod { get; } = mod;

    public string Name => Mod.Name;

    [ObservableProperty] private bool _isInstalled;

    public override string ToString() => Name;
}

/// <summary>
/// MVVM port of the legacy <c>ModDownloaderForm</c>: browse the curated mod list for the current game,
/// then install or uninstall a mod in one click (<see cref="IModInstallService"/>: the download goes
/// through the system browser, the manager does the rest). The manual buttons stay for mods the
/// manager cannot install itself.
/// </summary>
public partial class ModDownloaderViewModel : ViewModelBase
{
    public const string ModDbUrl = "https://www.moddb.com/games/dawn-of-war/mods?sort=rating-desc";

    private readonly IModlistService _modlist;
    private readonly IDialogService _dialogs;
    private readonly IGameLauncher _launcher;
    private readonly IModInstallService _installer;
    private readonly GameInstall _install;
    private readonly Action _onModulesChanged;
    private IReadOnlyList<ModListEntry> _all = [];
    private CancellationTokenSource? _operation;

    public ObservableCollection<ModListEntry> Mods { get; } = [];

    public string Header { get; }

    [NotifyPropertyChangedFor(nameof(SelectedMod))]
    [NotifyCanExecuteChangedFor(nameof(OpenModPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(UninstallCommand))]
    [ObservableProperty] private ModListEntry? _selectedEntry;

    public DownloadableMod? SelectedMod => SelectedEntry?.Mod;

    [ObservableProperty] private string _searchText;
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _status = "Loading the mod list…";

    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(UninstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunInstallerCommand))]
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private bool _isProgressIndeterminate = true;
    [ObservableProperty] private double _progressValue;

    /// <param name="moduleFilter">
    /// Name of a missing required module (the "fix missing mod" flow): the list starts filtered on it.
    /// </param>
    /// <param name="onModulesChanged">Called after a mod was installed or uninstalled, so the main list reloads.</param>
    public ModDownloaderViewModel(
        GameInstall install,
        string moduleFilter,
        IModlistService modlist,
        IDialogService dialogs,
        IGameLauncher launcher,
        IModInstallService installer,
        Action onModulesChanged)
    {
        _modlist = modlist;
        _dialogs = dialogs;
        _launcher = launcher;
        _installer = installer;
        _install = install;
        _onModulesChanged = onModulesChanged;
        _searchText = moduleFilter;

        Header = "Popular mods for " + install.Variant switch
        {
            GameVariant.DefinitiveEdition => "Definitive Edition:",
            GameVariant.Soulstorm => "Soulstorm:",
            GameVariant.DarkCrusade => "Dark Crusade:",
            GameVariant.WinterAssault => "Winter Assault:",
            _ => "Original:",
        };

        _ = LoadAsync(install.Variant);
    }

    private async Task LoadAsync(GameVariant variant)
    {
        try
        {
            var mods = await _modlist.LoadAsync(variant);
            _all = mods.Select(m => new ModListEntry(m)).ToList();
            RefreshInstalled();
            ApplyFilter();
            Status = $"{_all.Count} mods available. Mod list cached in {_modlist.ModlistPath}";
        }
        catch (Exception ex)
        {
            Status = $"Could not download the mod list: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    /// <summary>Matches the display name or the module file name, case-insensitively.</summary>
    private void ApplyFilter()
    {
        string query = SearchText.Trim();
        Mods.Clear();
        foreach (var entry in _all)
        {
            if (query.Length == 0 ||
                entry.Mod.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                entry.Mod.ModuleFileName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                Mods.Add(entry);
            }
        }

        SelectedEntry = Mods.Count > 0 ? Mods[0] : null;
        if (!IsLoading && Mods.Count == 0 && query.Length > 0)
            Status = $"No mod matches \"{query}\". Try ModDB.";
    }

    private void RefreshInstalled()
    {
        foreach (var entry in _all)
            entry.IsInstalled = _installer.IsInstalled(_install, entry.Mod);
        InstallCommand.NotifyCanExecuteChanged();
        UninstallCommand.NotifyCanExecuteChanged();
    }

    // ---- one-click install / uninstall ------------------------------------------------------

    private bool CanInstall => !IsBusy && SelectedEntry is { IsInstalled: false } entry && ModInstallService.Links(entry.Mod).Count > 0;

    /// <summary>
    /// Downloads the mod's files through the browser (or reuses an earlier download), then lets
    /// <see cref="IModInstallService"/> run the installer or extract the archive.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        var mod = SelectedEntry!.Mod;
        var ct = BeginOperation();
        var progress = new Progress<ModInstallProgress>(ShowProgress);
        try
        {
            var files = _installer.FindDownloadedFiles(mod).ToList();
            if (files.Count > 0 && !await _dialogs.ConfirmAsync(
                    $"Already downloaded in {_installer.DownloadsFolder}:\n\n{string.Join("\n", files.Select(Path.GetFileName))}\n\n"
                    + "Install from these files? Cancel downloads them again.",
                    "Install " + mod.Name))
            {
                files.Clear();
            }

            if (files.Count == 0)
            {
                if (_installer.DownloadsFolder is null)
                {
                    Status = "No downloads folder was found. Use \"Download only\", then \"Run installer…\".";
                    return;
                }
                foreach (var link in ModInstallService.Links(mod))
                    files.Add(await _installer.DownloadInBrowserAsync(link, _dialogs.OpenUrlAsync, progress, ct));
            }

            var result = await _installer.InstallAsync(_install, mod, files, progress, ct);
            Status = result.Message;
            if (result.Success)
                _onModulesChanged();
        }
        catch (OperationCanceledException)
        {
            Status = $"Installing {mod.Name} was cancelled.";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            Status = $"Could not install {mod.Name}: {ex.Message}";
        }
        finally
        {
            EndOperation();
        }
    }

    private bool CanUninstall => !IsBusy && SelectedEntry is { IsInstalled: true } entry && _installer.CanUninstall(_install, entry.Mod);

    [RelayCommand(CanExecute = nameof(CanUninstall))]
    private async Task UninstallAsync()
    {
        var mod = SelectedEntry!.Mod;
        if (!await _dialogs.ConfirmAsync($"Uninstall {mod.Name} from {_install.InstallDir}?", "Uninstall " + mod.Name))
            return;

        var ct = BeginOperation();
        try
        {
            var result = await _installer.UninstallAsync(_install, mod, new Progress<ModInstallProgress>(ShowProgress), ct);
            Status = result.Message;
            if (result.Success)
                _onModulesChanged();
        }
        catch (OperationCanceledException)
        {
            Status = $"Stopped waiting for the {mod.Name} uninstaller.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not uninstall {mod.Name}: {ex.Message}";
        }
        finally
        {
            EndOperation();
        }
    }

    private bool CanCancel => IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _operation?.Cancel();

    private CancellationToken BeginOperation()
    {
        _operation = new CancellationTokenSource();
        ShowProgress(new ModInstallProgress("Starting…"));
        IsBusy = true;
        return _operation.Token;
    }

    private void EndOperation()
    {
        IsBusy = false;
        _operation?.Dispose();
        _operation = null;
        RefreshInstalled();
    }

    private void ShowProgress(ModInstallProgress p)
    {
        ProgressText = p.Text;
        IsProgressIndeterminate = p.Fraction is null;
        ProgressValue = (p.Fraction ?? 0) * 100;
    }

    // ---- manual steps -----------------------------------------------------------------------

    [RelayCommand]
    private Task OpenModDbAsync() => _dialogs.OpenUrlAsync(ModDbUrl);

    private bool CanOpenPage => SelectedMod is { SiteLink.Length: > 0 };

    [RelayCommand(CanExecute = nameof(CanOpenPage))]
    private Task OpenModPageAsync() => _dialogs.OpenUrlAsync(SelectedMod!.SiteLink);

    private bool CanDownload => SelectedMod is { DownloadLink.Length: > 0 };

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
    {
        var mod = SelectedMod!;
        await _dialogs.OpenUrlAsync(mod.DownloadLink);

        if (mod.PatchLink.Length > 0)
        {
            // Small delay so the browser does not drop the second download.
            await Task.Delay(250);
            await _dialogs.OpenUrlAsync(mod.PatchLink);
        }

        Status = mod.PatchLink.Length > 0
            ? $"Opened the download and patch links of {mod.Name} in your browser."
            : $"Opened the download link of {mod.Name} in your browser.";
    }

    private bool CanRunInstaller => !IsBusy;

    /// <summary>
    /// Runs a downloaded Windows installer (e.g. a mod's Setup.exe) against the game: directly on
    /// Windows, inside the game's Proton/Wine prefix on Linux so it finds the install.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRunInstaller))]
    private async Task RunInstallerAsync()
    {
        var exe = await _dialogs.PickFileAsync("Select the mod installer (.exe) to run", _installer.DownloadsFolder);
        if (exe is null)
            return;

        var result = await _launcher.RunInGamePrefixAsync(_install, exe);
        Status = result.Started
            ? $"Started {Path.GetFileName(exe)}. {result.Detail}"
            : $"Could not run the installer: {result.Detail}";
    }

    [RelayCommand]
    private void Close()
    {
        _operation?.Cancel();
        RequestClose();
    }
}
