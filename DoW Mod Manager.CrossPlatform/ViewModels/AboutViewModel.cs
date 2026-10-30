using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using DoW_Mod_Manager.Abstractions;

namespace DoW_Mod_Manager.ViewModels;

/// <summary>
/// Port of the legacy <c>AboutForm</c>. The .NET Framework JIT/NGEN options and the in-place .exe
/// self-update are gone (meaningless on .NET 10 / Linux); "check for updates" opens the releases page.
/// </summary>
public partial class AboutViewModel : ViewModelBase
{
    public const string OriginalProjectUrl = "https://github.com/IgorTheLight/DoW-Mod-Manager";

    /// <summary>Project page, from <c>DmmHomePageUrl</c> in Branding.props.</summary>
    public string HomePageUrl { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "HomePageUrl")?.Value ?? OriginalProjectUrl;

    public string ReleasesUrl => HomePageUrl.TrimEnd('/') + "/releases";

    /// <summary>True in a fork: the About window then credits the upstream project it is based on.</summary>
    public bool IsFork => !string.Equals(HomePageUrl.TrimEnd('/'), OriginalProjectUrl, StringComparison.OrdinalIgnoreCase);

    private readonly IDialogService _dialogs;

    public string Version { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "?";

    /// <summary>The upstream release a fork is built on: the version without its "-linux.N" suffix.</summary>
    public string UpstreamBase => Version.Split('-')[0];

    public string Platform { get; }

    public AboutViewModel(IPlatformService platform, IDialogService dialogs)
    {
        _dialogs = dialogs;
        Platform = platform.IsWindows ? "Windows" : "Linux (game launched through Proton / Wine)";
    }

    [RelayCommand]
    private Task OpenUrlAsync(string url) => _dialogs.OpenUrlAsync(url);

    [RelayCommand]
    private void Close() => RequestClose();
}
