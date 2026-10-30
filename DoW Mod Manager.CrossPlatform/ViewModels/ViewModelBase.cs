using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DoW_Mod_Manager.ViewModels;

/// <summary>
/// Base class for every ViewModel. Built on CommunityToolkit.Mvvm
/// (<see cref="ObservableObject"/>) — use <c>[ObservableProperty]</c> and
/// <c>[RelayCommand]</c> in derived types. Chosen over ReactiveUI in WS4 for its
/// source-generator ergonomics and zero ceremony.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>Raised when the ViewModel asks its window to close (e.g. a CLOSE button).</summary>
    public event EventHandler? CloseRequested;

    protected void RequestClose() => CloseRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Called by the view layer once the hosting window has closed. Release timers etc. here.</summary>
    public virtual void OnClosed()
    {
    }
}
