using System;
using System.IO;
using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Services;

namespace DoW_Mod_Manager.Platform;

/// <summary>
/// Identifies which Dawn of War variant lives in a directory, shared by the Windows and Linux
/// locators so both platforms agree. Mirrors the legacy <c>GetCurrentGameEXE()</c> priority
/// (Soulstorm &gt; Dark Crusade &gt; Winter Assault &gt; Original), with the Definitive Edition
/// checked first: it ships a W40k.exe that would otherwise be mistaken for the Original game.
/// </summary>
public static class GameVariantDetector
{
    public const string OriginalExe = "W40k.exe";
    public const string WinterAssaultExe = "W40kWA.exe";
    public const string DarkCrusadeExe = "DarkCrusade.exe";
    public const string SoulstormExe = "Soulstorm.exe";

    /// <summary>The GOG build of the Definitive Edition names its executable differently.</summary>
    public const string DefinitiveEditionGogExe = "W40k_gog.exe";

    /// <summary>The Definitive Edition's own base module, absent from every classic install.</summary>
    public const string DefinitiveEditionModule = "DoWDE.module";

    /// <summary>Steam appid of Warhammer 40,000: Dawn of War - Definitive Edition.</summary>
    public const int DefinitiveEditionAppId = 3556750;

    /// <summary>
    /// Detects the variant from the files present. <paramref name="steamAppId"/> (from the Steam
    /// app manifest, when known) also identifies a Definitive Edition install.
    /// </summary>
    public static GameVariant? Detect(string directory, int? steamAppId = null)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return null;

        bool hasW40k = File.Exists(Path.Combine(directory, OriginalExe));
        bool hasDefinitiveExe = hasW40k || File.Exists(Path.Combine(directory, DefinitiveEditionGogExe));

        if (hasDefinitiveExe && IsDefinitiveEdition(directory, steamAppId))
            return GameVariant.DefinitiveEdition;

        if (File.Exists(Path.Combine(directory, SoulstormExe))) return GameVariant.Soulstorm;
        if (File.Exists(Path.Combine(directory, DarkCrusadeExe))) return GameVariant.DarkCrusade;
        if (File.Exists(Path.Combine(directory, WinterAssaultExe))) return GameVariant.WinterAssault;
        if (hasW40k) return GameVariant.Original;
        return null;
    }

    /// <summary>
    /// The executable launched for a variant installed in <paramref name="directory"/>: as
    /// <see cref="ExecutableFor(GameVariant)"/>, except a GOG Definitive Edition (only
    /// <c>W40k_gog.exe</c> present) gets its own exe name.
    /// </summary>
    public static string ExecutableFor(GameVariant variant, string directory) =>
        IsGogDefinitiveEdition(variant, directory) ? DefinitiveEditionGogExe : ExecutableFor(variant);

    /// <summary>True for a Definitive Edition folder holding the GOG executable and not the Steam one.</summary>
    public static bool IsGogDefinitiveEdition(GameVariant variant, string directory) =>
        variant == GameVariant.DefinitiveEdition
        && !File.Exists(Path.Combine(directory, OriginalExe))
        && File.Exists(Path.Combine(directory, DefinitiveEditionGogExe));

    /// <summary>The executable launched for a variant.</summary>
    public static string ExecutableFor(GameVariant variant) => variant switch
    {
        GameVariant.Soulstorm => SoulstormExe,
        GameVariant.DarkCrusade => DarkCrusadeExe,
        GameVariant.WinterAssault => WinterAssaultExe,
        GameVariant.Original => OriginalExe,
        GameVariant.DefinitiveEdition => OriginalExe,
        _ => OriginalExe
    };

    /// <summary>
    /// Per-user data directory and extra mods directory of an install. The classic games keep their
    /// data in the install dir; the Definitive Edition uses <c>&lt;AppData&gt;\Relic Entertainment\Dawn
    /// of War</c> and its <c>Mods</c> subfolder. <paramref name="appDataDir"/> is the roaming AppData
    /// directory as the game sees it (null when unknown: the install dir is then used).
    /// </summary>
    public static (string UserDataDir, string? ModsDir) UserDataFor(GameVariant variant, string installDir, string? appDataDir)
    {
        if (variant != GameVariant.DefinitiveEdition)
            return (installDir, null);

        string userData = string.IsNullOrEmpty(appDataDir)
            ? installDir
            : GamePaths.Resolve(appDataDir, "Relic Entertainment", "Dawn of War");
        return (userData, GamePaths.Resolve(userData, "Mods"));
    }

    /// <summary>The roaming AppData directory inside a Proton prefix (<c>compatdata/&lt;appid&gt;</c>).</summary>
    public static string ProtonAppDataDir(string protonPrefix) =>
        GamePaths.Resolve(protonPrefix, "pfx", "drive_c", "users", "steamuser", "AppData", "Roaming");

    private static bool IsDefinitiveEdition(string directory, int? steamAppId)
    {
        if (steamAppId == DefinitiveEditionAppId)
            return true;

        if (File.Exists(Path.Combine(directory, DefinitiveEditionModule)))
            return true;

        // The Steam build also ships steam_appid.txt next to the executable.
        try
        {
            string appIdFile = Path.Combine(directory, "steam_appid.txt");
            return File.Exists(appIdFile)
                && int.TryParse(File.ReadAllText(appIdFile).Trim(), out int id)
                && id == DefinitiveEditionAppId;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
