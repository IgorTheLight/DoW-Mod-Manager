using System.Threading.Tasks;

namespace DoW_Mod_Manager.ViewModels;

/// <summary>
/// UI services the ViewModels need without referencing Avalonia windows directly: message boxes,
/// file pickers, opening URLs and showing the feature windows. Replaces the legacy
/// <c>ThemedMessageBox</c> / <c>ThemedDialogueBox</c> and the <c>new XxxForm(this).Show()</c> calls.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows a modal message with an OK button (legacy <c>ThemedMessageBox.Show</c>).</summary>
    Task ShowMessageAsync(string message, string title = "");

    /// <summary>Shows a modal OK/Cancel question. Returns true on OK.</summary>
    Task<bool> ConfirmAsync(string message, string title = "");

    /// <summary>Opens <paramref name="url"/> in the default browser.</summary>
    Task OpenUrlAsync(string url);

    /// <summary>Lets the user pick one existing file. Returns its local path, or null when cancelled.</summary>
    Task<string?> PickFileAsync(string title, string? startDirectory = null);

    /// <summary>
    /// Shows the window bound to <paramref name="viewModel"/> (non-modal, owned by the main window).
    /// If a window for the same ViewModel type is already open it is brought to front instead.
    /// </summary>
    void ShowWindow(ViewModelBase viewModel);
}
