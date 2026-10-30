using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Platform.Linux;

/// <summary>
/// Discovers Dawn of War installations on Linux by scanning Steam library folders and the
/// Proton compatdata prefixes. Variant detection is shared with the Windows locator
/// (<see cref="GameVariantDetector"/>): Definitive Edition, then the legacy
/// <c>GetCurrentGameEXE()</c> priority (Soulstorm &gt; Dark Crusade &gt; Winter Assault &gt; Original).
/// </summary>
public sealed class LinuxGameInstallLocator : IGameInstallLocator
{
    // Fallback Steam appid <-> variant mapping, used only when no appmanifest names the game
    // folder (the manifest's appid always wins). The appid locates the compatdata/<appid> Proton
    // prefix. Verified against store.steampowered.com:
    //   4570    Dawn of War - Anniversary Edition (formerly GOTY/Gold; folder "Dawn of War Gold")
    //   9310    Winter Assault - a DLC of 4570 installed into the same folder, so it has no prefix
    //           of its own and maps to 4570
    //   4580    Dark Crusade (folder "Dawn of War Dark Crusade")
    //   9450    Soulstorm (folder "Dawn of War Soulstorm")
    //   3556750 Dawn of War - Definitive Edition (folder "Dawn of War Definitive Edition")
    private const int AnniversaryEditionAppId = 4570;
    private const int DarkCrusadeAppId = 4580;
    private const int SoulstormAppId = 9450;

    /// <summary>Steam folders all start with this (matched case-insensitively).</summary>
    private const string GameFolderPattern = "Dawn of War*";

    private readonly Func<IReadOnlyList<string>> _steamRoots;

    public LinuxGameInstallLocator()
        : this(() => SteamPaths.ExistingRoots().ToList())
    {
    }

    /// <summary>Test seam: uses the given Steam client roots (those that exist) instead of $HOME's.</summary>
    internal LinuxGameInstallLocator(IEnumerable<string> steamRoots)
        : this(() => SteamPaths.ExistingRoots(steamRoots).ToList())
    {
    }

    private LinuxGameInstallLocator(Func<IReadOnlyList<string>> steamRoots)
    {
        _steamRoots = steamRoots;
    }

    /// <summary>Maps a variant to its canonical Steam appid for the Proton prefix path.</summary>
    internal static int AppIdForVariant(GameVariant variant) => variant switch
    {
        GameVariant.DefinitiveEdition => GameVariantDetector.DefinitiveEditionAppId,
        GameVariant.Soulstorm => SoulstormAppId,
        GameVariant.DarkCrusade => DarkCrusadeAppId,
        GameVariant.WinterAssault => AnniversaryEditionAppId, // DLC 9310 of the AE app
        GameVariant.Original => AnniversaryEditionAppId,
        _ => AnniversaryEditionAppId
    };

    public Task<IReadOnlyList<GameInstall>> DiscoverAsync(CancellationToken ct = default)
    {
        var results = new List<GameInstall>();
        var steamRoots = _steamRoots();

        foreach (var steamRoot in steamRoots)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var library in SteamPaths.ReadLibraryFolders(steamRoot))
            {
                var commonDir = Path.Combine(library, "steamapps", "common");
                if (!Directory.Exists(commonDir))
                    continue;

                IEnumerable<string> gameDirs;
                try
                {
                    // Steam installs DoW under "Dawn of War Gold" (Anniversary Edition), "Dawn of
                    // War Dark Crusade", "Dawn of War Soulstorm", "Dawn of War Definitive Edition".
                    gameDirs = Directory.EnumerateDirectories(commonDir, GameFolderPattern, new EnumerationOptions
                    {
                        MatchCasing = MatchCasing.CaseInsensitive,
                        IgnoreInaccessible = true
                    }).ToList();
                }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }

                foreach (var gameDir in gameDirs)
                {
                    ct.ThrowIfCancellationRequested();

                    var install = Build(gameDir, Path.Combine(library, "steamapps"), steamRoots);
                    if (install is not null)
                        results.Add(install);
                }
            }
        }

        // De-duplicate by install directory (same library can be reached via several roots).
        var seen = new List<string>();
        IReadOnlyList<GameInstall> deduped = results
            .Where(g =>
            {
                if (seen.Any(s => SteamPaths.SamePath(s, g.InstallDir)))
                    return false;
                seen.Add(g.InstallDir);
                return true;
            })
            .ToList();

        return Task.FromResult(deduped);
    }

    public GameInstall? IdentifyInstall(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;

        // .../steamapps/common/<game> -> .../steamapps
        var steamAppsDir = LocateSteamAppsDir(directory);
        if (steamAppsDir is not null)
            return Build(directory, steamAppsDir, _steamRoots());

        var variant = GameVariantDetector.Detect(directory);
        if (variant is null)
            return null;

        // Outside Steam there is no known Wine prefix: user data stays next to the game.
        var (userData, modsDir) = GameVariantDetector.UserDataFor(variant.Value, directory, appDataDir: null);
        return new GameInstall
        {
            InstallDir = directory,
            ExecutableName = GameVariantDetector.ExecutableFor(variant.Value, directory),
            Variant = variant.Value,
            Distribution = GameVariantDetector.IsGogDefinitiveEdition(variant.Value, directory)
                ? GameDistribution.GoG
                : GameDistribution.Unknown,
            UserDataDir = userData,
            ModsDir = modsDir
        };
    }

    /// <summary>
    /// Builds a Steam install, shared by discovery and manual identification so both resolve the
    /// appid, the Proton prefix and the owning Steam client root the same way.
    /// </summary>
    private static GameInstall? Build(string gameDir, string steamAppsDir, IReadOnlyList<string> steamRoots)
    {
        var manifestAppId = SteamPaths.ReadAppId(steamAppsDir, gameDir);
        var variant = GameVariantDetector.Detect(gameDir, manifestAppId);
        if (variant is null)
            return null;

        var appId = manifestAppId ?? AppIdForVariant(variant.Value);
        var library = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(steamAppsDir)) ?? steamAppsDir;
        var protonPrefix = Path.Combine(steamAppsDir, "compatdata", appId.ToString());
        // The Definitive Edition keeps Local.ini, warnings.log, Profiles and Mods in the prefix's
        // %APPDATA%\Relic Entertainment\Dawn of War; the classic games in their install dir.
        var (userData, modsDir) = GameVariantDetector.UserDataFor(
            variant.Value, gameDir, GameVariantDetector.ProtonAppDataDir(protonPrefix));

        return new GameInstall
        {
            InstallDir = gameDir,
            ExecutableName = GameVariantDetector.ExecutableFor(variant.Value, gameDir),
            Variant = variant.Value,
            Distribution = GameDistribution.Steam,
            SteamAppId = appId,
            // The prefix may not exist yet if the game was never launched; we still surface the
            // canonical path so the launcher can create/use it.
            ProtonPrefix = protonPrefix,
            UserDataDir = userData,
            ModsDir = modsDir,
            // The client root whose libraryfolders.vdf lists this library, not just the first
            // root found: several can coexist (leftover native + Snap/Flatpak).
            SteamRoot = SteamPaths.ResolveClientRoot(library, gameDir, steamRoots)
        };
    }

    /// <summary>Walks up from a game directory to its <c>steamapps</c> folder, if any.</summary>
    private static string? LocateSteamAppsDir(string gameDirectory)
    {
        var dir = new DirectoryInfo(Path.TrimEndingDirectorySeparator(gameDirectory));
        while (dir is not null)
        {
            if (string.Equals(dir.Name, "steamapps", StringComparison.OrdinalIgnoreCase))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
