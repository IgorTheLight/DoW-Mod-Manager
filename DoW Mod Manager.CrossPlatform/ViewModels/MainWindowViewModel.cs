namespace DoW_Mod_Manager.ViewModels;

/// <summary>
/// Shell ViewModel. Hosts the ModManager screen as its main content. WS5 extends this with
/// navigation to the other feature windows (Mod Merger, Settings, Performance, Downloader…).
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    public ModManagerViewModel ModManager { get; }

    public MainWindowViewModel(ModManagerViewModel modManager)
    {
        ModManager = modManager;
    }
}
