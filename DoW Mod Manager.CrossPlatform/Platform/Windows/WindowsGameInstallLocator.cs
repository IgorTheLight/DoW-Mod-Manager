using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Platform.Windows;

/// <summary>
/// Identifies which Dawn of War variant lives in a directory. Mirrors the legacy
/// <c>ModManagerForm.GetCurrentGameEXE()</c> priority order:
/// Soulstorm &gt; Dark Crusade &gt; Winter Assault &gt; Original. The first executable found wins.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsGameInstallLocator : IGameInstallLocator
{
    private readonly string? _appDataDir;

    public WindowsGameInstallLocator()
        : this(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData))
    {
    }

    /// <summary>Uses <paramref name="appDataDir"/> as %APPDATA% (Definitive Edition user data).</summary>
    internal WindowsGameInstallLocator(string? appDataDir)
    {
        _appDataDir = appDataDir;
    }

    public GameInstall? IdentifyInstall(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return null;

        // Priority order mirrors GetCurrentGameEXE(): a folder with both Original and Winter
        // Assault resolves to Winter Assault, and a full Soulstorm install resolves to Soulstorm.
        // A Definitive Edition folder (W40k.exe + DoWDE.module) is recognised before the Original.
        // Shared with the Linux locator so both platforms agree.
        GameVariant? variant = GameVariantDetector.Detect(directory);
        if (variant is null)
            return null;

        return Build(directory, GameVariantDetector.ExecutableFor(variant.Value, directory), variant.Value);
    }

    /// <summary>
    /// Minimal discovery: the classic Windows usage is "manager sits in the game folder", so we
    /// probe the current working directory. A full Steam/registry scan can be added later; the
    /// frozen contract only requires we return whatever installs we can confidently identify.
    /// </summary>
    public Task<IReadOnlyList<GameInstall>> DiscoverAsync(CancellationToken ct = default)
    {
        var results = new List<GameInstall>();

        GameInstall? cwd = IdentifyInstall(Directory.GetCurrentDirectory());
        if (cwd != null)
            results.Add(cwd);

        return Task.FromResult<IReadOnlyList<GameInstall>>(results);
    }

    GameInstall Build(string directory, string executable, GameVariant variant)
    {
        // The Definitive Edition keeps Local.ini, warnings.log, Profiles and Mods in
        // %APPDATA%\Relic Entertainment\Dawn of War; the classic games in their install dir.
        var (userData, modsDir) = GameVariantDetector.UserDataFor(variant, directory, _appDataDir);

        return new GameInstall
        {
            InstallDir = directory,
            ExecutableName = executable,
            Variant = variant,
            // The Steam/GoG distinction is a user setting in the legacy app; default to Steam here,
            // except for the Definitive Edition, whose GOG build has its own exe name.
            Distribution = GameVariantDetector.IsGogDefinitiveEdition(variant, directory)
                ? GameDistribution.GoG
                : GameDistribution.Steam,
            UserDataDir = userData,
            ModsDir = modsDir
        };
    }
}
