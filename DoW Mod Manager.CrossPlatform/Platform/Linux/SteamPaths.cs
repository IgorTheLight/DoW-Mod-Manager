using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DoW_Mod_Manager.Platform.Linux;

/// <summary>
/// Steam client locations shared by install discovery and the Proton launcher, so both agree on
/// where Steam lives (native, Flatpak and Snap packagings), plus the small VDF readers they need.
/// </summary>
internal static partial class SteamPaths
{
    /// <summary>Environment variable that points at a non-standard Steam client root.</summary>
    public const string SteamDirEnv = "STEAM_DIR";

    /// <summary>Candidate Steam client install roots (STEAM_COMPAT_CLIENT_INSTALL_PATH).</summary>
    public static IEnumerable<string> DefaultRoots()
        => DefaultRoots(Environment.GetEnvironmentVariable("HOME"), Environment.GetEnvironmentVariable(SteamDirEnv));

    /// <summary>
    /// Candidate roots for an explicit home directory and <c>$STEAM_DIR</c> value (test seam).
    /// <c>$STEAM_DIR</c> comes first; a blank home falls back to the OS user profile.
    /// </summary>
    internal static IEnumerable<string> DefaultRoots(string? home, string? steamDir)
    {
        if (!string.IsNullOrWhiteSpace(steamDir))
            yield return steamDir.Trim();

        if (string.IsNullOrWhiteSpace(home))
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            yield break;

        yield return Path.Combine(home, ".steam", "steam");
        yield return Path.Combine(home, ".steam", "root");
        yield return Path.Combine(home, ".local", "share", "Steam");
        // Flatpak: data/Steam on current builds, .local/share/Steam on some older ones.
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam");
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
        yield return Path.Combine(home, "snap", "steam", "common", ".local", "share", "Steam");
    }

    /// <summary>The default roots that exist on disk, de-duplicated.</summary>
    public static IEnumerable<string> ExistingRoots() => ExistingRoots(DefaultRoots());

    /// <summary>
    /// The given candidate roots that exist on disk. ~/.steam/steam and ~/.steam/root are usually
    /// symlinks to ~/.local/share/Steam, so roots are de-duplicated by their resolved target and the
    /// first spelling wins.
    /// </summary>
    internal static IEnumerable<string> ExistingRoots(IEnumerable<string> candidates)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate))
                continue;
            if (seen.Add(Canonical(candidate)))
                yield return candidate;
        }
    }

    /// <summary>
    /// Reads <c>steamapps/libraryfolders.vdf</c> of a Steam client root and returns every library
    /// root it lists (the client root itself always counts first).
    /// </summary>
    public static IReadOnlyList<string> ReadLibraryFolders(string steamRoot)
    {
        var roots = new List<string> { steamRoot };

        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        string text;
        try { text = File.Exists(vdf) ? File.ReadAllText(vdf) : ""; }
        catch (IOException) { text = ""; }
        catch (UnauthorizedAccessException) { text = ""; }

        foreach (Match m in PathRegex().Matches(text))
        {
            var path = UnescapeVdf(m.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(path))
                roots.Add(path);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        return roots.Where(r => seen.Add(Normalize(r))).ToList();
    }

    /// <summary>
    /// Picks the Steam client root that owns <paramref name="libraryRoot"/> (the folder holding
    /// <c>steamapps</c>): first a root whose libraryfolders.vdf lists that library, else a root the
    /// game path lies under, else the first candidate. Null when there are no candidates.
    /// </summary>
    public static string? ResolveClientRoot(string libraryRoot, string gameDirectory, IReadOnlyList<string> roots)
    {
        if (roots.Count == 0)
            return null;

        foreach (var root in roots)
        {
            if (ReadLibraryFolders(root).Any(l => SamePath(l, libraryRoot)))
                return root;
        }

        var game = Normalize(gameDirectory);
        foreach (var root in roots)
        {
            foreach (var prefix in new[] { Normalize(root), Canonical(root) })
            {
                if (game == prefix || game.StartsWith(prefix + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    return root;
            }
        }

        return roots[0];
    }

    /// <summary>True when two paths name the same directory (literally or through a top-level symlink).</summary>
    internal static bool SamePath(string a, string b)
        => Normalize(a) == Normalize(b) || Canonical(a) == Canonical(b);

    /// <summary>
    /// Reads the real Steam appid of a game from the <c>appmanifest_*.acf</c> whose
    /// <c>installdir</c> matches the game folder name. Null when no manifest matches.
    /// </summary>
    public static int? ReadAppId(string steamAppsDir, string gameDirectory)
    {
        var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(gameDirectory));

        IEnumerable<string> manifests;
        try { manifests = Directory.EnumerateFiles(steamAppsDir, "appmanifest_*.acf").ToList(); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }

        foreach (var manifest in manifests)
        {
            string text;
            try { text = File.ReadAllText(manifest); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            var installDir = InstallDirRegex().Match(text);
            if (!installDir.Success)
                continue;

            // installdir is a bare folder name, but tolerate a path-like value too.
            var declared = UnescapeVdf(installDir.Groups[1].Value).Replace('\\', '/').TrimEnd('/');
            declared = declared[(declared.LastIndexOf('/') + 1)..];
            if (!string.Equals(declared, folderName, StringComparison.OrdinalIgnoreCase))
                continue;

            var appId = AppIdRegex().Match(text);
            if (appId.Success && int.TryParse(appId.Groups[1].Value, out var id))
                return id;

            // Fall back to the id in the manifest's file name (appmanifest_<appid>.acf).
            var nameId = Path.GetFileNameWithoutExtension(manifest)["appmanifest_".Length..];
            if (int.TryParse(nameId, out id))
                return id;
        }

        return null;
    }

    /// <summary>Decodes the backslash escapes of a quoted VDF/KeyValues string.</summary>
    internal static string UnescapeVdf(string value)
    {
        if (value.IndexOf('\\') < 0)
            return value;

        var sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c != '\\' || i == value.Length - 1)
            {
                sb.Append(c);
                continue;
            }

            char next = value[++i];
            sb.Append(next switch
            {
                'n' => '\n',
                't' => '\t',
                _ => next // \\ and \" (and anything unknown) decode to the escaped character
            });
        }
        return sb.ToString();
    }

    private static string Normalize(string path)
    {
        string full;
        try { full = Path.GetFullPath(path); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { full = path; }
        full = Path.TrimEndingDirectorySeparator(full);
        return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
    }

    /// <summary>Full, normalised path with a top-level symlink resolved (e.g. ~/.steam/steam).</summary>
    private static string Canonical(string path)
    {
        try
        {
            var target = new DirectoryInfo(Path.TrimEndingDirectorySeparator(path)).ResolveLinkTarget(returnFinalTarget: true);
            if (target is not null)
                return Normalize(target.FullName);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return Normalize(path);
    }

    [GeneratedRegex("\"path\"\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex PathRegex();

    [GeneratedRegex("\"installdir\"\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex InstallDirRegex();

    [GeneratedRegex("\"appid\"\\s*\"(\\d+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex AppIdRegex();
}
