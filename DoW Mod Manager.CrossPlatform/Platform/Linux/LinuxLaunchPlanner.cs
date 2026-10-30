using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Platform.Linux;

/// <summary>How a <see cref="LaunchPlan"/> starts the game.</summary>
public enum LaunchRoute
{
    /// <summary><c>steam -applaunch &lt;appid&gt; &lt;args&gt;</c> (native or snap Steam).</summary>
    Steam,
    /// <summary><c>flatpak run com.valvesoftware.Steam -applaunch &lt;appid&gt; &lt;args&gt;</c>.</summary>
    FlatpakSteam,
    /// <summary><c>xdg-open steam://run/&lt;appid&gt;//&lt;url-encoded args&gt;/</c> (last-resort Steam route).</summary>
    SteamUrl,
    /// <summary>
    /// <c>proton run &lt;exe&gt; &lt;args&gt;</c> with STEAM_COMPAT_DATA_PATH set, inside the Steam
    /// Linux Runtime when the Proton build requires one.
    /// </summary>
    Proton,
    /// <summary><c>wine &lt;exe&gt; &lt;args&gt;</c> with an optional WINEPREFIX.</summary>
    Wine
}

/// <summary>A fully resolved command: what to start, how, and a human-readable description.</summary>
public sealed class LaunchPlan
{
    public required LaunchRoute Route { get; init; }
    public required ProcessStartInfo StartInfo { get; init; }
    public required string Description { get; init; }

    /// <summary>True when Steam starts the game, so the started process is not the game.</summary>
    public bool ViaSteam => Route is LaunchRoute.Steam or LaunchRoute.FlatpakSteam or LaunchRoute.SteamUrl;
}

/// <summary>
/// Host probes used by <see cref="LinuxLaunchPlanner"/>. Every member defaults to the real
/// environment; tests replace them with fakes so command construction stays a pure function.
/// </summary>
public sealed class LinuxLaunchHost
{
    public const string FlatpakSteamAppId = "com.valvesoftware.Steam";

    public Func<string, string?> GetEnvironmentVariable { get; init; } = Environment.GetEnvironmentVariable;

    /// <summary>Resolves a bare executable name against PATH. Null when not found.</summary>
    public Func<string, string?> FindOnPath { get; init; } = DefaultFindOnPath;

    public Func<string, bool> FileExists { get; init; } = File.Exists;

    public Func<string, bool> DirectoryExists { get; init; } = Directory.Exists;

    /// <summary>True when the Flatpak Steam (<c>com.valvesoftware.Steam</c>) is installed.</summary>
    public Func<bool> IsFlatpakSteamInstalled { get; init; }

    /// <summary>True when a <c>steam://</c> URL handler is registered (xdg-mime).</summary>
    public Func<bool> HasSteamUrlHandler { get; init; } = DefaultHasSteamUrlHandler;

    /// <summary>
    /// Picks the Proton script to run directly for a game (overrides are handled by the planner).
    /// Defaults to <see cref="ProtonToolResolver.Select"/> over the game's and the default Steam roots.
    /// </summary>
    public Func<GameInstall, ProtonSelection?> SelectProton { get; init; }

    /// <summary>
    /// The Steam Linux Runtime a Proton script (first argument) requires, or null when it requires
    /// none. Defaults to the script's toolmanifest and <see cref="ProtonToolResolver.FindRuntimeEntryPoint"/>
    /// over the game's and the default Steam roots.
    /// </summary>
    public Func<string, GameInstall, SteamRuntimeRequirement?> ResolveRuntime { get; init; }

    public string HomeDirectory { get; init; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public LinuxLaunchHost()
    {
        IsFlatpakSteamInstalled = DefaultIsFlatpakSteamInstalled;
        SelectProton = DefaultSelectProton;
        ResolveRuntime = DefaultResolveRuntime;
    }

    private ProtonSelection? DefaultSelectProton(GameInstall game) =>
        ProtonToolResolver.Select(game.SteamAppId, LinuxLaunchPlanner.CompatDataPath(game, this), SteamRoots(game));

    private static SteamRuntimeRequirement? DefaultResolveRuntime(string protonScript, GameInstall game)
    {
        if (Path.GetDirectoryName(protonScript) is not { Length: > 0 } toolDir
            || ProtonToolResolver.ReadRequiredToolAppId(toolDir) is not { } appId)
            return null;
        return new SteamRuntimeRequirement(appId, ProtonToolResolver.FindRuntimeEntryPoint(appId, SteamRoots(game)));
    }

    private static List<string> SteamRoots(GameInstall game)
    {
        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(game.SteamRoot))
            roots.Add(game.SteamRoot!);
        roots.AddRange(SteamPaths.ExistingRoots());
        return roots;
    }

    // ---- real-environment defaults ----------------------------------------------------------

    public static string? DefaultFindOnPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private bool DefaultIsFlatpakSteamInstalled()
    {
        if (FindOnPath("flatpak") is not { } flatpak)
            return false;

        // Cheap check first: the per-app data dir exists once Flatpak Steam has been run.
        if (DirectoryExists(Path.Combine(HomeDirectory, ".var", "app", FlatpakSteamAppId)))
            return true;

        return RunQuietly(flatpak, ["info", FlatpakSteamAppId], out _) == 0;
    }

    private static bool DefaultHasSteamUrlHandler()
    {
        if (DefaultFindOnPath("xdg-open") is null || DefaultFindOnPath("xdg-mime") is not { } xdgMime)
            return false;

        return RunQuietly(xdgMime, ["query", "default", "x-scheme-handler/steam"], out var stdout) == 0
               && !string.IsNullOrWhiteSpace(stdout);
    }

    /// <summary>Runs a short probe command; returns its exit code, or -1 on failure / timeout.</summary>
    private static int RunQuietly(string fileName, IEnumerable<string> args, out string stdout)
    {
        stdout = "";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var a in args)
                psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p is null)
                return -1;
            var output = p.StandardOutput.ReadToEndAsync();
            _ = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(5000))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return -1;
            }
            stdout = output.Result;
            return p.ExitCode;
        }
        catch
        {
            return -1;
        }
    }
}

/// <summary>
/// Pure command construction for <see cref="LinuxGameLauncher"/>: given a game, its argument
/// string and the host probes, decides the launch route and builds the
/// <see cref="ProcessStartInfo"/>. No process is started here. See <see cref="LinuxGameLauncher"/>
/// for the route order and the environment overrides.
/// </summary>
public static class LinuxLaunchPlanner
{
    public const string ProtonOverrideEnv = "DOWMM_PROTON";         // full path to a `proton` script
    public const string WineOverrideEnv = "DOWMM_WINE";             // wine binary name or path
    public const string WinePrefixOverrideEnv = "DOWMM_WINEPREFIX"; // WINEPREFIX for the Wine route
    public const string WinePrefixEnv = "WINEPREFIX";
    public const string DllOverridesEnv = "WINEDLLOVERRIDES";

    public const string SteamConfirmationHint =
        "Steam may ask you to confirm the launch arguments the first time: check the Steam window if the game does not start.";

    /// <summary>Default appid for Proton prefixes when the install has none (Soulstorm).</summary>
    private const int FallbackAppId = 9450;

    /// <summary>
    /// Builds the plan that starts <paramref name="game"/>. Returns null (with
    /// <paramref name="error"/> set) when no route is available. <paramref name="dllOverrides"/>
    /// (e.g. <c>d3d9,d3d8=n,b</c> when DXVK or dgVoodoo2 sits in the game folder) is set as
    /// <c>WINEDLLOVERRIDES</c> on the Proton and Wine routes. Steam does not pass our environment to
    /// the game; its Proton already loads DXVK's DLLs as native, which picks the game-folder copies.
    /// </summary>
    public static LaunchPlan? BuildGameLaunchPlan(GameInstall game, string arguments, LinuxLaunchHost host, out string? error,
        string? dllOverrides = null)
    {
        error = null;
        var workingDir = host.DirectoryExists(game.InstallDir) ? game.InstallDir : Environment.CurrentDirectory;
        var notes = new List<string>();

        // An explicit DOWMM_PROTON override always wins over going through Steam.
        var protonOverride = ResolveProtonOverride(host, notes);
        List<string> steamNotes = dllOverrides is null
            ? notes
            : [.. notes, $"if the game-folder Direct3D DLLs are ignored, set the game's Steam launch options to: {DllOverridesEnv}=\"{dllOverrides}\" %command%"];

        if (protonOverride is null && game.Distribution == GameDistribution.Steam && game.SteamAppId is { } appId)
        {
            if (FindNativeSteam(host) is { } steam)
                return SteamPlan(LaunchRoute.Steam, steam, [], appId, arguments, workingDir, steamNotes);

            if (host.FindOnPath("flatpak") is { } flatpak && host.IsFlatpakSteamInstalled())
                return SteamPlan(LaunchRoute.FlatpakSteam, flatpak, ["run", LinuxLaunchHost.FlatpakSteamAppId],
                                 appId, arguments, workingDir, steamNotes);

            if (host.FindOnPath("xdg-open") is { } xdgOpen && host.HasSteamUrlHandler())
                return SteamUrlPlan(xdgOpen, appId, arguments, workingDir, steamNotes);
        }

        var proton = ResolveProton(game, protonOverride, host, notes);
        if (proton is not null)
            return WithDllOverrides(ProtonPlan(game, game.ExecutablePath, proton, SplitArguments(arguments), arguments, workingDir, host, notes),
                                    dllOverrides, host);

        var wine = ResolveWine(host);
        if (wine is not null)
            return WithDllOverrides(WinePlan(game, game.ExecutablePath, wine, SplitArguments(arguments), arguments, workingDir, host, notes),
                                    dllOverrides, host);

        error = "No Steam client, Proton runtime or Wine binary found. "
              + $"Set {ProtonOverrideEnv} to a proton script or {WineOverrideEnv} to a wine binary."
              + AppendNotes(notes);
        return null;
    }

    /// <summary>
    /// Builds the plan that runs a Windows program (e.g. a mod's Setup.exe) inside the game's
    /// prefix: Proton against the game's compatdata (see <see cref="ProtonPlan"/>), or Wine with the resolved prefix.
    /// <c>DOWMM_WINEPREFIX</c> forces the Wine route with that prefix.
    /// </summary>
    public static LaunchPlan? BuildPrefixRunPlan(GameInstall game, string exePath, LinuxLaunchHost host, out string? error)
    {
        error = null;
        var dir = Path.GetDirectoryName(exePath);
        var workingDir = !string.IsNullOrEmpty(dir) && host.DirectoryExists(dir) ? dir : Environment.CurrentDirectory;
        var notes = new List<string>();

        var winePrefixForced = !string.IsNullOrWhiteSpace(host.GetEnvironmentVariable(WinePrefixOverrideEnv));
        var protonOverride = ResolveProtonOverride(host, notes);

        if (!winePrefixForced && (game.ProtonPrefix is not null || game.Distribution == GameDistribution.Steam))
        {
            var proton = ResolveProton(game, protonOverride, host, notes);
            if (proton is not null)
                return ProtonPlan(game, exePath, proton, [], "", workingDir, host, notes);
        }

        var wine = ResolveWine(host);
        if (wine is not null)
            return WinePlan(game, exePath, wine, [], "", workingDir, host, notes);

        error = "No Proton runtime or Wine binary found to run the installer. "
              + $"Set {ProtonOverrideEnv} to a proton script or {WineOverrideEnv} to a wine binary."
              + AppendNotes(notes);
        return null;
    }

    // ---- Steam ------------------------------------------------------------------------------

    /// <summary>"steam" on PATH, else the snap launcher (/snap/bin is not always on PATH).</summary>
    private static string? FindNativeSteam(LinuxLaunchHost host)
    {
        if (host.FindOnPath("steam") is { } onPath)
            return onPath;
        const string snapSteam = "/snap/bin/steam";
        return host.FileExists(snapSteam) ? snapSteam : null;
    }

    /// <summary>
    /// <c>[flatpak run com.valvesoftware.Steam |steam] -applaunch &lt;appid&gt; &lt;args&gt;</c>:
    /// Steam picks the per-game compat tool (CompatToolMapping), wraps it in the Steam Linux
    /// Runtime and handles DRM. Invoking Proton ourselves would pick an arbitrary version and could
    /// upgrade the game's prefix.
    /// </summary>
    private static LaunchPlan SteamPlan(LaunchRoute route, string fileName, string[] prefixArgs, int appId,
        string arguments, string workingDir, List<string> notes)
    {
        var psi = NewStartInfo(fileName, workingDir);
        foreach (var a in prefixArgs)
            psi.ArgumentList.Add(a);
        psi.ArgumentList.Add("-applaunch");
        psi.ArgumentList.Add(appId.ToString());
        foreach (var arg in SplitArguments(arguments))
            psi.ArgumentList.Add(arg);

        var label = route == LaunchRoute.FlatpakSteam ? "Flatpak Steam" : "Steam";
        var prefix = prefixArgs.Length > 0 ? " " + string.Join(' ', prefixArgs) : "";
        var description = $"{label}: \"{fileName}\"{prefix} -applaunch {appId} {arguments}. {SteamConfirmationHint}"
                        + AppendNotes(notes);
        return new LaunchPlan { Route = route, StartInfo = psi, Description = description };
    }

    /// <summary>
    /// <c>xdg-open steam://run/&lt;appid&gt;//&lt;args&gt;/</c>. The Valve "Steam browser protocol"
    /// documents <c>steam://run/&lt;id&gt;//&lt;args&gt;/</c> (the empty segment is the optional
    /// language); arguments are URL-encoded and Steam shows a confirmation prompt for them.
    /// </summary>
    private static LaunchPlan SteamUrlPlan(string xdgOpen, int appId, string arguments, string workingDir, List<string> notes)
    {
        var url = BuildSteamRunUrl(appId, arguments);
        var psi = NewStartInfo(xdgOpen, workingDir);
        psi.ArgumentList.Add(url);

        return new LaunchPlan
        {
            Route = LaunchRoute.SteamUrl,
            StartInfo = psi,
            Description = $"Steam URL: \"{xdgOpen}\" {url}. {SteamConfirmationHint}" + AppendNotes(notes)
        };
    }

    public static string BuildSteamRunUrl(int appId, string arguments) =>
        string.IsNullOrWhiteSpace(arguments)
            ? $"steam://run/{appId}"
            : $"steam://run/{appId}//{Uri.EscapeDataString(arguments.Trim())}/";

    // ---- Proton -----------------------------------------------------------------------------

    private static string? ResolveProtonOverride(LinuxLaunchHost host, List<string> notes)
    {
        var overridePath = host.GetEnvironmentVariable(ProtonOverrideEnv);
        if (string.IsNullOrWhiteSpace(overridePath))
            return null;
        if (host.FileExists(overridePath))
            return overridePath;

        notes.Add($"{ProtonOverrideEnv}=\"{overridePath}\" does not exist and was ignored");
        return null;
    }

    /// <summary>The override, else the Steam-consistent tool choice (its reason is added to <paramref name="notes"/>).</summary>
    private static string? ResolveProton(GameInstall game, string? protonOverride, LinuxLaunchHost host, List<string> notes)
    {
        if (protonOverride is not null)
        {
            notes.Add($"Proton from {ProtonOverrideEnv}");
            return protonOverride;
        }

        var selection = host.SelectProton(game);
        if (selection is null)
            return null;
        notes.Add(selection.Reason);
        return selection.ScriptPath;
    }

    /// <summary>Compatdata dir (STEAM_COMPAT_DATA_PATH) of <paramref name="game"/>.</summary>
    public static string CompatDataPath(GameInstall game, LinuxLaunchHost host) =>
        !string.IsNullOrWhiteSpace(game.ProtonPrefix)
            ? game.ProtonPrefix!
            : Path.Combine(SteamClientRoot(game, host), "steamapps", "compatdata",
                           (game.SteamAppId ?? FallbackAppId).ToString());

    private static string SteamClientRoot(GameInstall game, LinuxLaunchHost host) =>
        !string.IsNullOrWhiteSpace(game.SteamRoot)
            ? game.SteamRoot!
            : Path.Combine(host.HomeDirectory, ".steam", "steam");

    /// <summary>
    /// <c>proton run &lt;exe&gt; &lt;args...&gt;</c>, or, when the build requires a Steam Linux
    /// Runtime (toolmanifest <c>require_tool_appid</c>), what Steam runs:
    /// <c>_v2-entry-point --verb=waitforexitandrun -- proton waitforexitandrun &lt;exe&gt; &lt;args...&gt;</c>.
    /// Outside its runtime, GE-Proton and Proton 9+ miss libraries on many hosts (32-bit FreeType:
    /// NSIS installers fail). <c>proton</c> is a Python script with a shebang, so it is executed directly.
    /// </summary>
    private static LaunchPlan ProtonPlan(GameInstall game, string exePath, string protonPath,
        IEnumerable<string> args, string arguments, string workingDir, LinuxLaunchHost host, List<string> notes)
    {
        var compatData = CompatDataPath(game, host);
        var clientInstall = SteamClientRoot(game, host);
        var runtime = host.ResolveRuntime(protonPath, game);
        var entryPoint = runtime?.EntryPoint;
        if (runtime is { EntryPoint: null })
            notes.Add($"this Proton needs the Steam Linux Runtime (Steam app {runtime.AppId}), which is not installed; "
                    + "running Proton without it, 32-bit programs may fail. Install the runtime from Steam");

        var psi = NewStartInfo(entryPoint ?? protonPath, workingDir);
        if (entryPoint is not null)
        {
            psi.ArgumentList.Add("--verb=waitforexitandrun");
            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add(protonPath);
            psi.ArgumentList.Add("waitforexitandrun");
        }
        else
            psi.ArgumentList.Add("run");
        psi.ArgumentList.Add(exePath);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        psi.Environment["STEAM_COMPAT_DATA_PATH"] = compatData;
        psi.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = clientInstall;
        var steamHome = ProtonHome(clientInstall, host);
        if (steamHome is not null)
            psi.Environment["HOME"] = steamHome;
        if (entryPoint is not null)
        {
            // What Steam sets so the container also sees folders outside its default shares.
            psi.Environment["STEAM_COMPAT_INSTALL_PATH"] = game.InstallDir;
            psi.Environment["STEAM_COMPAT_TOOL_PATHS"] = $"{Path.GetDirectoryName(protonPath)}:{Path.GetDirectoryName(entryPoint)}";
            psi.Environment["STEAM_COMPAT_MOUNTS"] = $"{Path.GetDirectoryName(exePath)}:{compatData}";
        }

        var command = entryPoint is not null
            ? $"\"{entryPoint}\" --verb=waitforexitandrun -- \"{protonPath}\" waitforexitandrun"
            : $"\"{protonPath}\" run";
        var description = $"Proton: {command} \"{exePath}\" {arguments}".TrimEnd()
                        + $" [STEAM_COMPAT_DATA_PATH={compatData}, STEAM_COMPAT_CLIENT_INSTALL_PATH={clientInstall}"
                        + (steamHome is not null ? $", HOME={steamHome}]" : "]")
                        + AppendNotes(notes);
        return new LaunchPlan { Route = LaunchRoute.Proton, StartInfo = psi, Description = description };
    }

    /// <summary>
    /// HOME for Proton, or null to keep ours. Proton's Steam bridge (lsteamclient) loads
    /// <c>$HOME/.steam/sdk64/steamclient.so</c>; snap and Flatpak Steam keep that link in their own
    /// home (the folder holding <c>.local/share/Steam</c>), which Steam passes to Proton as HOME.
    /// Without it, Proton's steam.exe crashes before starting the program.
    /// </summary>
    private static string? ProtonHome(string clientInstall, LinuxLaunchHost host)
    {
        var bridge = Path.Combine(".steam", "sdk64", "steamclient.so");
        var clientSuffix = Path.DirectorySeparatorChar + Path.Combine(".local", "share", "Steam");
        var root = Path.TrimEndingDirectorySeparator(clientInstall);
        if (!root.EndsWith(clientSuffix, StringComparison.Ordinal))
            return null;

        var steamHome = root[..^clientSuffix.Length];
        return steamHome != Path.TrimEndingDirectorySeparator(host.HomeDirectory)
               && !host.FileExists(Path.Combine(host.HomeDirectory, bridge))
               && host.FileExists(Path.Combine(steamHome, bridge))
            ? steamHome
            : null;
    }

    // ---- Wine -------------------------------------------------------------------------------

    /// <summary>Resolves the wine binary: <c>DOWMM_WINE</c> override, else "wine" on PATH.</summary>
    private static string? ResolveWine(LinuxLaunchHost host)
    {
        var overrideWine = host.GetEnvironmentVariable(WineOverrideEnv);
        if (!string.IsNullOrWhiteSpace(overrideWine))
        {
            if (host.FileExists(overrideWine))
                return overrideWine;
            if (host.FindOnPath(overrideWine) is { } resolved)
                return resolved;
        }

        return host.FindOnPath("wine");
    }

    /// <summary>
    /// WINEPREFIX precedence: <c>DOWMM_WINEPREFIX</c>, then the Proton prefix's <c>pfx</c> (where
    /// Proton keeps the actual Wine prefix), then an inherited <c>WINEPREFIX</c> (Lutris, Bottles,
    /// Heroic, a shell export...), else Wine's default <c>~/.wine</c> (null).
    /// </summary>
    public static string? ResolveWinePrefix(GameInstall game, LinuxLaunchHost host, out string source)
    {
        var forced = host.GetEnvironmentVariable(WinePrefixOverrideEnv);
        if (!string.IsNullOrWhiteSpace(forced))
        {
            source = WinePrefixOverrideEnv;
            return forced;
        }

        if (!string.IsNullOrWhiteSpace(game.ProtonPrefix))
        {
            var pfx = Path.Combine(game.ProtonPrefix!, "pfx");
            source = "Proton prefix";
            return host.DirectoryExists(pfx) ? pfx : game.ProtonPrefix;
        }

        var inherited = host.GetEnvironmentVariable(WinePrefixEnv);
        if (!string.IsNullOrWhiteSpace(inherited))
        {
            source = "inherited " + WinePrefixEnv;
            return inherited;
        }

        source = "default";
        return null;
    }

    private static LaunchPlan WinePlan(GameInstall game, string exePath, string winePath,
        IEnumerable<string> args, string arguments, string workingDir, LinuxLaunchHost host, List<string> notes)
    {
        var psi = NewStartInfo(winePath, workingDir);
        psi.ArgumentList.Add(exePath);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        var winePrefix = ResolveWinePrefix(game, host, out var source);
        if (winePrefix is not null)
            psi.Environment[WinePrefixEnv] = winePrefix;
        else
            psi.Environment.Remove(WinePrefixEnv);

        var description = $"Wine: \"{winePath}\" \"{exePath}\" {arguments}".TrimEnd()
                        + (winePrefix is not null ? $" [WINEPREFIX={winePrefix} ({source})]" : " [default WINEPREFIX ~/.wine]")
                        + AppendNotes(notes);
        return new LaunchPlan { Route = LaunchRoute.Wine, StartInfo = psi, Description = description };
    }

    // ---- helpers ----------------------------------------------------------------------------

    /// <summary>
    /// Adds <paramref name="dllOverrides"/> to the plan's <c>WINEDLLOVERRIDES</c>. An inherited value
    /// that already mentions one of those DLLs is the user's choice and is left alone.
    /// </summary>
    private static LaunchPlan WithDllOverrides(LaunchPlan plan, string? dllOverrides, LinuxLaunchHost host)
    {
        if (dllOverrides is null)
            return plan;

        var inherited = host.GetEnvironmentVariable(DllOverridesEnv);
        string value;
        if (string.IsNullOrWhiteSpace(inherited))
            value = dllOverrides;
        else if (dllOverrides.Split('=')[0].Split(',').Any(dll => inherited.Contains(dll, StringComparison.OrdinalIgnoreCase)))
            return plan;
        else
            value = inherited + ";" + dllOverrides;

        plan.StartInfo.Environment[DllOverridesEnv] = value;
        return new LaunchPlan
        {
            Route = plan.Route,
            StartInfo = plan.StartInfo,
            Description = plan.Description + $" [{DllOverridesEnv}={value}]"
        };
    }

    private static ProcessStartInfo NewStartInfo(string fileName, string workingDir) => new()
    {
        FileName = fileName,
        WorkingDirectory = workingDir,
        UseShellExecute = false
    };

    private static string AppendNotes(List<string> notes) =>
        notes.Count > 0 ? " | " + string.Join("; ", notes) : "";

    /// <summary>
    /// Splits a command-line string into individual arguments on whitespace, honouring double
    /// quotes (which are removed). Wine/Proton/Steam rebuild the Windows command line by joining
    /// the arguments with spaces and quoting those that contain whitespace, so:
    /// <list type="bullet">
    /// <item><c>-modname My Mod</c> (the unquoted legacy form) becomes three arguments and the game
    /// sees <c>-modname My Mod</c>, exactly what the Windows build passes;</item>
    /// <item><c>-modname "My Mod"</c> becomes two arguments and the game sees
    /// <c>-modname "My Mod"</c>.</item>
    /// </list>
    /// Embedded/escaped quotes are not supported; DoW argument strings never contain them.
    /// </summary>
    public static IEnumerable<string> SplitArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            yield break;

        var current = new StringBuilder();
        var inQuotes = false;
        var hasToken = false; // so that "" yields an empty argument

        foreach (var c in arguments)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                hasToken = true;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (hasToken)
                {
                    yield return current.ToString();
                    current.Clear();
                    hasToken = false;
                }
            }
            else
            {
                current.Append(c);
                hasToken = true;
            }
        }

        if (hasToken)
            yield return current.ToString();
    }
}
