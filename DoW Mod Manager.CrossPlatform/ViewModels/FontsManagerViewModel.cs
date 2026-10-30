using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Services;

namespace DoW_Mod_Manager.ViewModels;

/// <summary>One editable row of the Fonts Manager.</summary>
public partial class FontRowViewModel : ObservableObject
{
    private readonly Action _changed;

    public FontEntry Entry { get; }
    public string DowFontFile => Entry.DowFontFile;
    public string Description => Entry.Description;
    public bool Exists => Entry.Exists;

    [ObservableProperty] private string _fontFileName;
    [ObservableProperty] private bool _fontFileFound;
    [ObservableProperty] private decimal? _size;

    public FontRowViewModel(FontEntry entry, Action changed)
    {
        Entry = entry;
        _changed = changed;
        _fontFileName = entry.FontFileName;
        _fontFileFound = entry.FontFileFound;
        _size = entry.Size;
    }

    partial void OnFontFileNameChanged(string value) => _changed();
    partial void OnSizeChanged(decimal? value) => _changed();

    public FontEntry ToEntry() => Entry with { FontFileName = FontFileName, Size = (int)(Size ?? Entry.Size) };
}

/// <summary>
/// MVVM port of the legacy <c>FontsManagerForm</c>: choose the TTF file and size of every font of the
/// "larger fonts" mod, per UI width. Requires that mod to be installed.
/// </summary>
public partial class FontsManagerViewModel : ViewModelBase
{
    private readonly GameInstall _install;
    private readonly IFontsService _fonts;
    private readonly IFileSystemService _files;
    private readonly IDialogService _dialogs;
    private bool _loading;

    public IReadOnlyList<string> ScreenWidths => _fonts.ScreenWidths;
    public ObservableCollection<FontRowViewModel> Rows { get; } = [];

    public bool IsAvailable { get; }

    public string UnavailableMessage =>
        "If you want to manage your fonts, install the \"larger fonts\" tweak from the main window first.";

    [ObservableProperty] private string _selectedWidth;

    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [ObservableProperty] private bool _isDirty;

    [ObservableProperty] private string _status = "";

    public FontsManagerViewModel(
        GameInstall install,
        string gameScreenWidth,
        IFontsService fonts,
        IFileSystemService files,
        IDialogService dialogs)
    {
        _install = install;
        _fonts = fonts;
        _files = files;
        _dialogs = dialogs;
        _selectedWidth = fonts.MapScreenWidth(gameScreenWidth);
        IsAvailable = fonts.IsAvailable(install.InstallDir);

        if (!IsAvailable)
            return;

        try
        {
            fonts.EnsureBackup(install.InstallDir);
        }
        catch (Exception ex)
        {
            Status = $"Could not back up the default fonts: {ex.Message}";
        }
        Load();
    }

    partial void OnSelectedWidthChanged(string value) => Load();

    private void Load()
    {
        if (!IsAvailable)
            return;

        _loading = true;
        try
        {
            Rows.Clear();
            foreach (var entry in _fonts.Read(_install.InstallDir, SelectedWidth))
                Rows.Add(new FontRowViewModel(entry, OnRowChanged));
            IsDirty = false;
        }
        catch (Exception ex)
        {
            Status = $"Could not read the fonts: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnRowChanged()
    {
        if (!_loading)
            IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(IsDirty))]
    private void Save()
    {
        try
        {
            _fonts.Write(_install.InstallDir, SelectedWidth, Rows.Where(r => r.Exists).Select(r => r.ToEntry()));
            Load();
            Status = $"Fonts saved for UI width {SelectedWidth}.";
        }
        catch (Exception ex)
        {
            Status = $"Could not save the fonts: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DefaultsAsync()
    {
        if (!await _dialogs.ConfirmAsync("Restore every font file from the backup made on first use?", "Restore default fonts"))
            return;

        Status = _fonts.RestoreDefaults(_install.InstallDir)
            ? "Default fonts restored."
            : "No backup found — nothing to restore.";
        Load();
    }

    [RelayCommand]
    private async Task BrowseAsync(FontRowViewModel row)
    {
        string? path = await _dialogs.PickFileAsync("Choose a font file", _fonts.GetFontsDirectory(_install.InstallDir));
        if (path is null)
            return;

        row.FontFileName = Path.GetFileName(path);
        row.FontFileFound = File.Exists(GamePaths.Resolve(_fonts.GetFontsDirectory(_install.InstallDir), row.FontFileName));
        if (!row.FontFileFound)
            Status = $"{row.FontFileName} is not in the game's font folder: copy it there, or the game won't find it.";
    }

    [RelayCommand]
    private void OpenFolder() => _files.OpenFolder(_fonts.GetFontsDirectory(_install.InstallDir));

    [RelayCommand]
    private Task ShowWidthHelpAsync() => _dialogs.ShowMessageAsync(
        "The in-game resolution is rarely the width the game uses to pick font sizes. Discovered so far:\n\n" +
        "1024×768 → 1024\n1280×720 → 800\n1366×768 → 1024\n1920×1080 → 1280\n2560×1440 → ?\n4096×2160 → ?",
        "Discovered screen widths");

    [RelayCommand]
    private void Close() => RequestClose();
}
