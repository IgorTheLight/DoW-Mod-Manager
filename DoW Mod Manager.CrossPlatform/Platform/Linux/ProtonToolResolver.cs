using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DoW_Mod_Manager.Platform.Linux;

/// <summary>An installed Proton build that can be run directly.</summary>
public sealed class ProtonTool
{
    /// <summary>Steam's internal compat-tool name (CompatToolMapping "name"), e.g. "proton_9" or "GE-Proton9-20".</summary>
    public required string Name { get; init; }

    public required string Directory { get; init; }

    public string ScriptPath => Path.Combine(Directory, "proton");

    /// <summary><c>CURRENT_PREFIX_VERSION</c> declared by the proton script (what it writes to compatdata/&lt;appid&gt;/version).</summary>
    public string? PrefixVersion { get; init; }

    public override string ToString() => $"{Name} ({Path.GetFileName(Directory)})";
}

/// <summary>The Proton script chosen for a game, with a human-readable reason for Detail.</summary>
public sealed record ProtonSelection(string ScriptPath, string Reason);

/// <summary>
/// The Steam Linux Runtime (Steam app <see cref="AppId"/>, e.g. 1628350 "sniper") a Proton build
/// must run in; <see cref="EntryPoint"/> is its <c>_v2-entry-point</c>, null when not installed.
/// </summary>
public sealed record SteamRuntimeRequirement(int AppId, string? EntryPoint);

/// <summary>
/// Picks the Proton build to run directly for a game, mirroring what Steam would use:
/// <list type="number">
/// <item>the per-game <c>CompatToolMapping</c> entry of <c>&lt;steamroot&gt;/config/config.vdf</c>;</item>
/// <item>the tool whose <c>CURRENT_PREFIX_VERSION</c> created the existing prefix
/// (<c>compatdata/&lt;appid&gt;/version</c>), then one of the same major.minor;</item>
/// <item>the default mapping (<c>CompatToolMapping/"0"</c>);</item>
/// <item>the newest installed tool.</item>
/// </list>
/// Tool names: third-party tools declare theirs in <c>compatibilitytool.vdf</c>
/// (<c>compat_tools/&lt;name&gt;/install_path</c>). Valve's builds in
/// <c>steamapps/common/Proton*</c> have only a <c>toolmanifest.vdf</c> without a name (Steam keeps
/// the mapping in its binary appinfo cache), so their names are derived from the folder:
/// "Proton 9.0 (Beta)" → proton_9, "Proton 5.13" → proton_513, "Proton - Experimental" →
/// proton_experimental, "Proton Hotfix" → proton_hotfix.
/// </summary>
public static partial class ProtonToolResolver
{
    public static ProtonSelection? Select(int? appId, string? compatDataPath, IEnumerable<string> steamRoots)
    {
        var roots = steamRoots.Where(System.IO.Directory.Exists).Distinct(StringComparer.Ordinal).ToList();
        var tools = DiscoverTools(roots);
        if (tools.Count == 0)
            return null;

        var mapping = ReadCompatToolMapping(roots);
        var prefixVersion = ReadPrefixVersion(compatDataPath);
        var notes = new List<string>();

        // 1. Per-game choice made in Steam.
        if (appId is { } id && mapping.TryGetValue(id.ToString(), out var gameToolName))
        {
            if (FindByName(tools, gameToolName) is { } gameTool)
                return Choose(gameTool, $"Steam compat tool for app {id}", prefixVersion, notes);
            notes.Add($"Steam maps app {id} to \"{gameToolName}\", which is not installed");
        }

        // 2. The build that created the existing prefix.
        if (prefixVersion is not null)
        {
            var exact = tools.FirstOrDefault(t => string.Equals(t.PrefixVersion, prefixVersion, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
                return Choose(exact, $"matches prefix version {prefixVersion}", prefixVersion, notes);

            var prefixKey = VersionKey(prefixVersion);
            var sameMajorMinor = tools
                .Where(t => t.PrefixVersion is not null && SameMajorMinor(VersionKey(t.PrefixVersion), prefixKey))
                .OrderByDescending(t => VersionKey(t.PrefixVersion), VersionComparer.Instance)
                .FirstOrDefault();
            if (sameMajorMinor is not null)
                return Choose(sameMajorMinor, $"same Proton series as prefix version {prefixVersion}", prefixVersion, notes);
        }

        // 3. Steam's default compat tool.
        if (mapping.TryGetValue("0", out var defaultName))
        {
            if (FindByName(tools, defaultName) is { } defaultTool)
                return Choose(defaultTool, "Steam default compat tool", prefixVersion, notes);
            notes.Add($"Steam's default compat tool \"{defaultName}\" is not installed");
        }

        // 4. Newest installed build.
        var newest = tools.OrderByDescending(ToolVersionKey, VersionComparer.Instance)
                          .ThenByDescending(t => t.Directory, StringComparer.OrdinalIgnoreCase)
                          .First();
        notes.Add("no Steam mapping or prefix match, using the newest installed Proton");
        return Choose(newest, "newest installed", prefixVersion, notes);
    }

    private static ProtonSelection Choose(ProtonTool tool, string why, string? prefixVersion, List<string> notes)
    {
        var reason = new StringBuilder($"Proton {tool}: {why}");
        if (prefixVersion is not null && tool.PrefixVersion is not null
            && !string.Equals(tool.PrefixVersion, prefixVersion, StringComparison.OrdinalIgnoreCase))
        {
            var cmp = VersionComparer.Instance.Compare(VersionKey(tool.PrefixVersion), VersionKey(prefixVersion));
            reason.Append(cmp > 0
                ? $"; the prefix was created by {prefixVersion}, this newer build ({tool.PrefixVersion}) will upgrade it"
                : $"; the prefix was created by {prefixVersion}, this build is {tool.PrefixVersion}");
        }
        foreach (var n in notes)
            reason.Append("; ").Append(n);
        return new ProtonSelection(tool.ScriptPath, reason.ToString());
    }

    private static ProtonTool? FindByName(List<ProtonTool> tools, string name) =>
        tools.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    // ---- discovery --------------------------------------------------------------------------

    public static List<ProtonTool> DiscoverTools(IEnumerable<string> steamRoots)
    {
        var tools = new List<ProtonTool>();
        var seenDirs = new HashSet<string>(StringComparer.Ordinal);

        void Add(string name, string dir)
        {
            dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir));
            if (!File.Exists(Path.Combine(dir, "proton")) || !seenDirs.Add(dir))
                return;
            tools.Add(new ProtonTool { Name = name, Directory = dir, PrefixVersion = ReadScriptPrefixVersion(Path.Combine(dir, "proton")) });
        }

        foreach (var root in steamRoots)
        {
            // Third-party tools (GE-Proton...): compatibilitytools.d/<dir>/compatibilitytool.vdf, or
            // a manifest placed directly in compatibilitytools.d.
            var custom = Path.Combine(root, "compatibilitytools.d");
            foreach (var manifest in SafeEnumerate(() => System.IO.Directory.EnumerateFiles(custom, "*.vdf")))
                AddFromManifest(manifest, Add);
            foreach (var dir in SafeEnumerate(() => System.IO.Directory.EnumerateDirectories(custom)))
            {
                var manifest = Path.Combine(dir, "compatibilitytool.vdf");
                if (File.Exists(manifest))
                    AddFromManifest(manifest, Add);
                Add(Path.GetFileName(dir), dir); // no-op when the manifest already registered it
            }

            // Valve builds.
            foreach (var dir in SafeEnumerate(() => System.IO.Directory.EnumerateDirectories(Path.Combine(root, "steamapps", "common"), "Proton*")))
            {
                if (ValveToolName(Path.GetFileName(dir)) is { } name)
                    Add(name, dir);
            }
        }

        return tools;
    }

    private static void AddFromManifest(string manifestPath, Action<string, string> add)
    {
        var vdf = TextVdf.TryParseFile(manifestPath);
        if (vdf?.Find("compat_tools") is not { } compatTools)
            return;

        var baseDir = Path.GetDirectoryName(manifestPath)!;
        foreach (var (name, node) in compatTools.Children)
        {
            var installPath = node.GetString("install_path") ?? ".";
            add(name, Path.IsPathRooted(installPath) ? installPath : Path.Combine(baseDir, installPath));
        }
    }

    /// <summary>Steam's internal name for a Valve Proton folder, or null for non-Proton runtimes.</summary>
    public static string? ValveToolName(string folderName)
    {
        var n = ParenthesizedRegex().Replace(folderName, "").Trim();
        if (n.Equals("Proton - Experimental", StringComparison.OrdinalIgnoreCase) || n.Equals("Proton Experimental", StringComparison.OrdinalIgnoreCase))
            return "proton_experimental";
        if (n.Equals("Proton Hotfix", StringComparison.OrdinalIgnoreCase))
            return "proton_hotfix";

        var m = ValveVersionRegex().Match(n);
        if (!m.Success)
            return null; // e.g. "Proton EasyAntiCheat Runtime"
        var major = m.Groups[1].Value;
        var minor = m.Groups[2].Success ? m.Groups[2].Value : "0";
        return minor == "0" ? $"proton_{major}" : $"proton_{major}{minor}";
    }

    // ---- Steam Linux Runtime ----------------------------------------------------------------

    /// <summary>
    /// <c>require_tool_appid</c> of a tool's <c>toolmanifest.vdf</c>: the Steam Linux Runtime Steam
    /// starts the tool in. Null when the tool declares none (old Proton builds).
    /// </summary>
    public static int? ReadRequiredToolAppId(string toolDirectory) =>
        int.TryParse(TextVdf.TryParseFile(Path.Combine(toolDirectory, "toolmanifest.vdf"))
                            ?.Find("manifest")?.GetString("require_tool_appid"), out var appId)
            ? appId
            : null;

    /// <summary>
    /// The <c>_v2-entry-point</c> of the runtime installed as Steam app <paramref name="appId"/>,
    /// found through its <c>appmanifest_&lt;appid&gt;.acf</c> in any library of
    /// <paramref name="steamRoots"/>. Null when it is not installed or has no entry point (scout).
    /// </summary>
    public static string? FindRuntimeEntryPoint(int appId, IEnumerable<string> steamRoots)
    {
        foreach (var library in steamRoots.SelectMany(SteamPaths.ReadLibraryFolders).Distinct(StringComparer.Ordinal))
        {
            var steamApps = Path.Combine(library, "steamapps");
            var installDir = TextVdf.TryParseFile(Path.Combine(steamApps, $"appmanifest_{appId}.acf"))
                                    ?.Find("AppState")?.GetString("installdir");
            if (string.IsNullOrWhiteSpace(installDir))
                continue;
            var entryPoint = Path.Combine(steamApps, "common", installDir, "_v2-entry-point");
            if (File.Exists(entryPoint))
                return entryPoint;
        }
        return null;
    }

    // ---- Steam config -----------------------------------------------------------------------

    /// <summary>appid ("0" = default) → compat tool name, merged across Steam roots (first wins).</summary>
    public static Dictionary<string, string> ReadCompatToolMapping(IEnumerable<string> steamRoots)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var root in steamRoots)
        {
            var mapping = TextVdf.TryParseFile(Path.Combine(root, "config", "config.vdf"))?.Find("CompatToolMapping");
            if (mapping is null)
                continue;
            foreach (var (key, node) in mapping.Children)
            {
                if (node.GetString("name") is { Length: > 0 } name)
                    result.TryAdd(key, name);
            }
        }
        return result;
    }

    private static string? ReadPrefixVersion(string? compatDataPath)
    {
        if (string.IsNullOrEmpty(compatDataPath))
            return null;
        try
        {
            var file = Path.Combine(compatDataPath, "version");
            if (!File.Exists(file))
                return null;
            var v = File.ReadAllText(file).Trim();
            return v.Length > 0 ? v : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static string? ReadScriptPrefixVersion(string script)
    {
        try
        {
            foreach (var line in File.ReadLines(script).Take(300))
            {
                var m = PrefixVersionRegex().Match(line);
                if (m.Success)
                    return m.Groups[1].Value;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    private static IEnumerable<string> SafeEnumerate(Func<IEnumerable<string>> enumerate)
    {
        try { return enumerate().ToList(); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    // ---- versions ---------------------------------------------------------------------------

    /// <summary>All digit groups of a version string: "9.0-203" → [9,0,203], "GE-Proton9-20" → [9,20].</summary>
    public static int[] VersionKey(string? version) =>
        version is null ? [] : DigitsRegex().Matches(version).Select(m => int.TryParse(m.Value, out var v) ? v : 0).ToArray();

    private static int[] ToolVersionKey(ProtonTool t) =>
        t.PrefixVersion is not null ? VersionKey(t.PrefixVersion) : VersionKey(Path.GetFileName(t.Directory));

    private static bool SameMajorMinor(int[] a, int[] b) =>
        a.Length >= 2 && b.Length >= 2 && a[0] == b[0] && a[1] == b[1];

    private sealed class VersionComparer : IComparer<int[]>
    {
        public static readonly VersionComparer Instance = new();

        public int Compare(int[]? x, int[]? y)
        {
            x ??= []; y ??= [];
            for (var i = 0; i < Math.Max(x.Length, y.Length); i++)
            {
                var c = (i < x.Length ? x[i] : 0).CompareTo(i < y.Length ? y[i] : 0);
                if (c != 0)
                    return c;
            }
            return 0;
        }
    }

    [GeneratedRegex(@"\s*\([^)]*\)")]
    private static partial Regex ParenthesizedRegex();

    [GeneratedRegex(@"^Proton\s+(\d+)(?:\.(\d+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex ValveVersionRegex();

    [GeneratedRegex("^\\s*CURRENT_PREFIX_VERSION\\s*=\\s*[\"']([^\"']+)[\"']")]
    private static partial Regex PrefixVersionRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex();
}

/// <summary>Minimal reader for Valve's text KeyValues (VDF) files: quoted/unquoted tokens, braces, // comments.</summary>
public sealed class TextVdf
{
    public string? Value { get; private init; }

    public List<(string Key, TextVdf Node)> Children { get; } = [];

    public string? GetString(string key) =>
        Children.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)).Node?.Value;

    /// <summary>Depth-first search for the first object named <paramref name="key"/> (case-insensitive).</summary>
    public TextVdf? Find(string key)
    {
        foreach (var (k, node) in Children)
        {
            if (node.Value is null && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                return node;
            if (node.Find(key) is { } found)
                return found;
        }
        return null;
    }

    public static TextVdf? TryParseFile(string path)
    {
        try { return File.Exists(path) ? Parse(File.ReadAllText(path)) : null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public static TextVdf Parse(string text)
    {
        var tokens = Tokenize(text).GetEnumerator();
        var root = new TextVdf();
        ParseInto(root, tokens);
        return root;
    }

    private static void ParseInto(TextVdf node, IEnumerator<(string Text, bool IsBrace)> tokens)
    {
        while (tokens.MoveNext())
        {
            var (key, keyIsBrace) = tokens.Current;
            if (keyIsBrace)
            {
                if (key == "}") return;
                continue; // stray "{"
            }
            if (!tokens.MoveNext())
                return;

            var (value, valueIsBrace) = tokens.Current;
            if (valueIsBrace && value == "{")
            {
                var child = new TextVdf();
                ParseInto(child, tokens);
                node.Children.Add((key, child));
            }
            else if (!valueIsBrace)
            {
                node.Children.Add((key, new TextVdf { Value = value }));
            }
            else
            {
                return; // "}" right after a key: malformed, stop this level
            }
        }
    }

    private static IEnumerable<(string Text, bool IsBrace)> Tokenize(string s)
    {
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n') i++;
                continue;
            }
            if (c is '{' or '}') { yield return (c.ToString(), true); i++; continue; }
            if (c == '[') // conditional like [$WIN32]: skip
            {
                while (i < s.Length && s[i] != ']') i++;
                i++;
                continue;
            }

            var sb = new StringBuilder();
            if (c == '"')
            {
                i++;
                while (i < s.Length && s[i] != '"')
                {
                    if (s[i] == '\\' && i + 1 < s.Length)
                    {
                        i++;
                        sb.Append(s[i] switch { 'n' => '\n', 't' => '\t', _ => s[i] });
                    }
                    else
                    {
                        sb.Append(s[i]);
                    }
                    i++;
                }
                i++; // closing quote
            }
            else
            {
                while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] is not ('{' or '}' or '"'))
                    sb.Append(s[i++]);
            }
            yield return (sb.ToString(), false);
        }
    }
}
