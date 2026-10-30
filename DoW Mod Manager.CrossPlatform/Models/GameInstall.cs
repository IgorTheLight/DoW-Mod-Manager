using System.IO;

namespace DoW_Mod_Manager.Models;

/// <summary>The supported Dawn of War variants.</summary>
public enum GameVariant
{
    Original,          // W40k.exe
    WinterAssault,     // W40kWA.exe
    DarkCrusade,       // DarkCrusade.exe
    Soulstorm,         // Soulstorm.exe
    DefinitiveEdition  // W40k.exe (64-bit) next to DoWDE.module
}

/// <summary>How the game was distributed — affects LAA patching and Proton appids.</summary>
public enum GameDistribution
{
    Unknown,
    Steam,
    GoG
}

/// <summary>
/// A discovered Dawn of War installation. Produced by <see cref="Abstractions.IGameInstallLocator"/>
/// and consumed by every platform service. Immutable.
/// </summary>
public sealed class GameInstall
{
    /// <summary>Directory that contains the game executable and the *.module files.</summary>
    public required string InstallDir { get; init; }

    /// <summary>Executable file name, e.g. "Soulstorm.exe".</summary>
    public required string ExecutableName { get; init; }

    public GameVariant Variant { get; init; }

    public GameDistribution Distribution { get; init; } = GameDistribution.Unknown;

    /// <summary>Steam appid, when discovered through a Steam/Proton prefix. Null otherwise.</summary>
    public int? SteamAppId { get; init; }

    /// <summary>
    /// On Linux: the Proton prefix directory (STEAM_COMPAT_DATA_PATH), i.e.
    /// <c>~/.steam/steam/steamapps/compatdata/&lt;appid&gt;</c>. Null on Windows / native.
    /// </summary>
    public string? ProtonPrefix { get; init; }

    /// <summary>Steam root install path (STEAM_COMPAT_CLIENT_INSTALL_PATH). Null on Windows.</summary>
    public string? SteamRoot { get; init; }

    private readonly string? _userDataDir;

    /// <summary>
    /// Per-user game data: <c>Local.ini</c>, <c>warnings.log</c>, <c>Profiles/</c>. The classic games
    /// keep it in <see cref="InstallDir"/> (the default); the Definitive Edition uses
    /// <c>%APPDATA%\Relic Entertainment\Dawn of War</c> (inside the Proton prefix on Linux).
    /// </summary>
    public string UserDataDir
    {
        get => _userDataDir ?? InstallDir;
        init => _userDataDir = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Extra directory scanned for mods besides <see cref="InstallDir"/>: the Definitive Edition's
    /// <c>&lt;UserDataDir&gt;/Mods</c>. Null for the classic games.
    /// </summary>
    public string? ModsDir { get; init; }

    /// <summary>
    /// Definitive Edition only: a Soulstorm install whose <c>*.module</c> files are also listed, so
    /// its mods can be used without copying them (the <c>SoulstormDir</c> setting). Null otherwise.
    /// </summary>
    public string? SoulstormModsDir { get; init; }

    /// <summary>A copy of this install with <see cref="SoulstormModsDir"/> replaced.</summary>
    public GameInstall WithSoulstormModsDir(string? directory) => new()
    {
        InstallDir = InstallDir,
        ExecutableName = ExecutableName,
        Variant = Variant,
        Distribution = Distribution,
        SteamAppId = SteamAppId,
        ProtonPrefix = ProtonPrefix,
        SteamRoot = SteamRoot,
        UserDataDir = _userDataDir ?? "",
        ModsDir = ModsDir,
        SoulstormModsDir = string.IsNullOrWhiteSpace(directory) ? null : directory
    };

    public string ExecutablePath => Path.Combine(InstallDir, ExecutableName);

    /// <summary>
    /// Winter Assault and the Original game have no "Playable" flag in *.module files. The
    /// Definitive Edition also ships a W40k.exe, but its modules do carry the flag.
    /// </summary>
    public bool IsOldGame => Variant is GameVariant.Original or GameVariant.WinterAssault;

    /// <summary>Only Soulstorm supports the in-memory fog removal hack.</summary>
    public bool SupportsNoFog => Variant == GameVariant.Soulstorm;

    /// <summary>The Definitive Edition executable is 64-bit (Large-Address-Aware is meaningless).</summary>
    public bool Is64Bit => Variant == GameVariant.DefinitiveEdition;

    /// <summary>
    /// Module launched by "start base game": the Definitive Edition boots its own DoWDE module,
    /// the classic games W40k.
    /// </summary>
    public string BaseGameModuleName => Variant == GameVariant.DefinitiveEdition ? "DoWDE" : "W40k";

    public override string ToString() => $"{Variant} ({Distribution}) @ {InstallDir}";
}
