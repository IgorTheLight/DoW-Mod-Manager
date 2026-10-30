using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using DoW_Mod_Manager.ViewModels;
using DoW_Mod_Manager.Views.About;
using DoW_Mod_Manager.Views.Dialogs;
using DoW_Mod_Manager.Views.ModDownloader;
using DoW_Mod_Manager.Views.ModMerger;
using DoW_Mod_Manager.Views.Settings;

namespace DoW_Mod_Manager.Views;

/// <summary>Avalonia implementation of <see cref="IDialogService"/>.</summary>
public sealed class DialogService : IDialogService
{
    private readonly Dictionary<Type, Window> _open = [];

    private static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    /// <summary>The window the user is interacting with: the last active one, else the main window.</summary>
    private Window? ActiveWindow => _open.Values.FirstOrDefault(w => w.IsActive) ?? MainWindow;

    public async Task ShowMessageAsync(string message, string title = "")
    {
        if (ActiveWindow is { } owner)
            await new MessageWindow(message, title, withCancel: false).ShowDialog<bool>(owner);
    }

    public async Task<bool> ConfirmAsync(string message, string title = "") =>
        ActiveWindow is { } owner && await new MessageWindow(message, title, withCancel: true).ShowDialog<bool>(owner);

    public async Task OpenUrlAsync(string url)
    {
        if (ActiveWindow is { } owner && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            await owner.Launcher.LaunchUriAsync(uri);
    }

    public async Task<string?> PickFileAsync(string title, string? startDirectory = null)
    {
        if (ActiveWindow is not { } owner)
            return null;

        var storage = owner.StorageProvider;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = startDirectory is null ? null : await storage.TryGetFolderFromPathAsync(startDirectory),
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public void ShowWindow(ViewModelBase viewModel)
    {
        var type = viewModel.GetType();
        if (_open.TryGetValue(type, out var existing))
        {
            existing.Activate();
            return;
        }

        Window window = viewModel switch
        {
            ModMergerViewModel => new ModMergerWindow(),
            ModDownloaderViewModel => new ModDownloaderWindow(),
            SettingsViewModel => new SettingsWindow(),
            SystemPerformanceViewModel => new SystemPerformanceWindow(),
            FontsManagerViewModel => new FontsManagerWindow(),
            AboutViewModel => new AboutWindow(),
            _ => throw new ArgumentException($"No window registered for {type.Name}.", nameof(viewModel)),
        };

        window.DataContext = viewModel;
        EventHandler close = (_, _) => window.Close();
        viewModel.CloseRequested += close;
        window.Closed += (_, _) =>
        {
            viewModel.CloseRequested -= close;
            _open.Remove(type);
            viewModel.OnClosed();
        };

        _open[type] = window;
        if (MainWindow is { } owner)
            window.Show(owner);
        else
            window.Show();
    }
}
