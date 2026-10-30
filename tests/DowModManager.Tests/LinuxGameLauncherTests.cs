using System.Diagnostics;
using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Platform.Linux;
using Xunit;

namespace DowModManager.Tests
{
    /// <summary>
    /// Command construction and game-pid discovery of the Linux launcher. Everything goes through
    /// fakes (<see cref="LinuxLaunchHost"/>, a fake /proc tree), so these run on any OS.
    /// </summary>
    public class LinuxGameLauncherTests : IDisposable
    {
        private const string Home = "/home/tester";
        private readonly string _root = Path.Combine(Path.GetTempPath(), "dmm_launcher_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        private static GameInstall SteamGame(string? protonPrefix = "/steam/steamapps/compatdata/9450") => new()
        {
            InstallDir = "/steam/steamapps/common/Dawn of War Soulstorm",
            ExecutableName = "Soulstorm.exe",
            Variant = GameVariant.Soulstorm,
            Distribution = GameDistribution.Steam,
            SteamAppId = 9450,
            ProtonPrefix = protonPrefix,
            SteamRoot = "/steam"
        };

        private static GameInstall GogGame() => new()
        {
            InstallDir = "/games/gog/Soulstorm",
            ExecutableName = "Soulstorm.exe",
            Variant = GameVariant.Soulstorm,
            Distribution = GameDistribution.GoG
        };

        private static LinuxLaunchHost Host(
            Dictionary<string, string>? path = null,
            Dictionary<string, string>? env = null,
            string[]? files = null,
            string[]? dirs = null,
            bool flatpakSteam = false,
            bool steamUrlHandler = false,
            string? installedProton = null,
            SteamRuntimeRequirement? runtime = null) => new()
        {
            GetEnvironmentVariable = name => env is not null && env.TryGetValue(name, out var v) ? v : null,
            FindOnPath = name => path is not null && path.TryGetValue(name, out var v) ? v : null,
            FileExists = f => files?.Contains(f) == true,
            DirectoryExists = d => dirs?.Contains(d) == true,
            IsFlatpakSteamInstalled = () => flatpakSteam,
            HasSteamUrlHandler = () => steamUrlHandler,
            SelectProton = _ => installedProton is null ? null : new ProtonSelection(installedProton, "test pick"),
            ResolveRuntime = (_, _) => runtime,
            HomeDirectory = Home
        };

        private static LaunchPlan Plan(GameInstall game, LinuxLaunchHost host, string args = "-modname W40k -nomovies")
        {
            var plan = LinuxLaunchPlanner.BuildGameLaunchPlan(game, args, host, out var error);
            Assert.True(plan is not null, error);
            return plan!;
        }

        // ---- route selection / command construction ----------------------------------------

        [Fact]
        public void NativeSteamOnPath_UsesApplaunch()
        {
            var plan = Plan(SteamGame(), Host(path: new() { ["steam"] = "/usr/bin/steam" }, installedProton: "/p/proton"));

            Assert.Equal(LaunchRoute.Steam, plan.Route);
            Assert.True(plan.ViaSteam);
            Assert.Equal("/usr/bin/steam", plan.StartInfo.FileName);
            Assert.Equal(["-applaunch", "9450", "-modname", "W40k", "-nomovies"], plan.StartInfo.ArgumentList);
            Assert.Contains("confirm the launch arguments", plan.Description);
        }

        [Fact]
        public void SnapSteamNotOnPath_UsesSnapBin()
        {
            var plan = Plan(SteamGame(), Host(files: ["/snap/bin/steam"]));

            Assert.Equal(LaunchRoute.Steam, plan.Route);
            Assert.Equal("/snap/bin/steam", plan.StartInfo.FileName);
        }

        [Fact]
        public void FlatpakSteam_UsesFlatpakRun()
        {
            var plan = Plan(SteamGame(), Host(path: new() { ["flatpak"] = "/usr/bin/flatpak" }, flatpakSteam: true,
                                               installedProton: "/p/proton"));

            Assert.Equal(LaunchRoute.FlatpakSteam, plan.Route);
            Assert.Equal("/usr/bin/flatpak", plan.StartInfo.FileName);
            Assert.Equal(["run", "com.valvesoftware.Steam", "-applaunch", "9450", "-modname", "W40k", "-nomovies"],
                         plan.StartInfo.ArgumentList);
            Assert.Contains("confirm the launch arguments", plan.Description);
        }

        [Fact]
        public void FlatpakBinaryWithoutSteamApp_IsNotUsed()
        {
            var plan = Plan(SteamGame(), Host(path: new() { ["flatpak"] = "/usr/bin/flatpak" }, installedProton: "/p/proton"));

            Assert.Equal(LaunchRoute.Proton, plan.Route);
        }

        [Fact]
        public void DefaultFlatpakProbe_DetectsVarAppDirectory()
        {
            var host = new LinuxLaunchHost
            {
                FindOnPath = name => name == "flatpak" ? "/usr/bin/flatpak" : null,
                DirectoryExists = d => d == Path.Combine(Home, ".var", "app", "com.valvesoftware.Steam"),
                HomeDirectory = Home
            };

            Assert.True(host.IsFlatpakSteamInstalled());
        }

        [Fact]
        public void SteamUrlHandler_IsLastSteamResort()
        {
            var plan = Plan(SteamGame(), Host(path: new() { ["xdg-open"] = "/usr/bin/xdg-open" }, steamUrlHandler: true,
                                               installedProton: "/p/proton"));

            Assert.Equal(LaunchRoute.SteamUrl, plan.Route);
            Assert.Equal(["steam://run/9450//-modname%20W40k%20-nomovies/"], plan.StartInfo.ArgumentList);
        }

        [Fact]
        public void ProtonOverride_BypassesSteam()
        {
            var host = Host(path: new() { ["steam"] = "/usr/bin/steam" },
                            env: new() { ["DOWMM_PROTON"] = "/opt/GE-Proton9/proton" },
                            files: ["/opt/GE-Proton9/proton"],
                            installedProton: "/p/proton");

            var plan = Plan(SteamGame(), host);

            Assert.Equal(LaunchRoute.Proton, plan.Route);
            Assert.False(plan.ViaSteam);
            Assert.Equal("/opt/GE-Proton9/proton", plan.StartInfo.FileName);
            Assert.Equal(["run", SteamGame().ExecutablePath, "-modname", "W40k", "-nomovies"], plan.StartInfo.ArgumentList);
            Assert.Equal("/steam/steamapps/compatdata/9450", plan.StartInfo.Environment["STEAM_COMPAT_DATA_PATH"]);
            Assert.Equal("/steam", plan.StartInfo.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"]);
        }

        [Fact]
        public void MissingProtonOverride_IsIgnoredWithNote()
        {
            var host = Host(path: new() { ["steam"] = "/usr/bin/steam" },
                            env: new() { ["DOWMM_PROTON"] = "/nope/proton" });

            var plan = Plan(SteamGame(), host);

            Assert.Equal(LaunchRoute.Steam, plan.Route);
            Assert.Contains("DOWMM_PROTON", plan.Description);
        }

        [Fact]
        public void NoSteamClient_FallsBackToInstalledProton()
        {
            var plan = Plan(SteamGame(protonPrefix: null), Host(installedProton: "/p/proton"));

            Assert.Equal(LaunchRoute.Proton, plan.Route);
            Assert.Equal(Path.Combine("/steam", "steamapps", "compatdata", "9450"),
                         plan.StartInfo.Environment["STEAM_COMPAT_DATA_PATH"]);
        }

        [Fact]
        public void GogInstall_NeverUsesSteam()
        {
            var plan = Plan(GogGame(), Host(path: new() { ["steam"] = "/usr/bin/steam", ["wine"] = "/usr/bin/wine" }));

            Assert.Equal(LaunchRoute.Wine, plan.Route);
        }

        [Fact]
        public void Wine_InheritedWinePrefix_IsRespected()
        {
            var plan = Plan(GogGame(), Host(path: new() { ["wine"] = "/usr/bin/wine" },
                                            env: new() { ["WINEPREFIX"] = "/home/tester/Games/heroic/Prefixes/dow" }));

            Assert.Equal(LaunchRoute.Wine, plan.Route);
            Assert.Equal("/usr/bin/wine", plan.StartInfo.FileName);
            Assert.Equal([GogGame().ExecutablePath, "-modname", "W40k", "-nomovies"], plan.StartInfo.ArgumentList);
            Assert.Equal("/home/tester/Games/heroic/Prefixes/dow", plan.StartInfo.Environment["WINEPREFIX"]);
        }

        [Fact]
        public void Wine_DowmmWinePrefix_WinsOverEverything()
        {
            var host = Host(path: new() { ["wine"] = "/usr/bin/wine" },
                            env: new() { ["WINEPREFIX"] = "/inherited", ["DOWMM_WINEPREFIX"] = "/forced" },
                            dirs: [Path.Combine("/steam/steamapps/compatdata/9450", "pfx")]);

            var plan = Plan(SteamGame(), host);

            Assert.Equal(LaunchRoute.Wine, plan.Route);
            Assert.Equal("/forced", plan.StartInfo.Environment["WINEPREFIX"]);
            Assert.Contains("DOWMM_WINEPREFIX", plan.Description);
        }

        [Fact]
        public void Wine_ProtonPrefixPfx_WinsOverInheritedWinePrefix()
        {
            var pfx = Path.Combine("/steam/steamapps/compatdata/9450", "pfx");
            var host = Host(path: new() { ["wine"] = "/usr/bin/wine" },
                            env: new() { ["WINEPREFIX"] = "/inherited" },
                            dirs: [pfx]);

            var plan = Plan(SteamGame(), host);

            Assert.Equal(pfx, plan.StartInfo.Environment["WINEPREFIX"]);
        }

        [Fact]
        public void Wine_DowmmWineOverride_IsUsed()
        {
            var plan = Plan(GogGame(), Host(env: new() { ["DOWMM_WINE"] = "/opt/wine-ge/bin/wine" },
                                            files: ["/opt/wine-ge/bin/wine"]));

            Assert.Equal("/opt/wine-ge/bin/wine", plan.StartInfo.FileName);
        }

        [Fact]
        public void NothingAvailable_ReturnsErrorMentioningOverrides()
        {
            var plan = LinuxLaunchPlanner.BuildGameLaunchPlan(GogGame(), "-modname W40k", Host(), out var error);

            Assert.Null(plan);
            Assert.Contains("DOWMM_PROTON", error);
            Assert.Contains("DOWMM_WINE", error);
        }

        // ---- graphics wrapper DLL overrides -------------------------------------------------

        [Fact]
        public void DllOverrides_WineRoute_SetsWineDllOverrides()
        {
            var plan = LinuxLaunchPlanner.BuildGameLaunchPlan(GogGame(), "-modname W40k",
                Host(path: new() { ["wine"] = "/usr/bin/wine" }), out _, "d3d9,d3d8=n,b");

            Assert.Equal("d3d9,d3d8=n,b", plan!.StartInfo.Environment["WINEDLLOVERRIDES"]);
            Assert.Contains("WINEDLLOVERRIDES=d3d9,d3d8=n,b", plan.Description);
        }

        [Fact]
        public void DllOverrides_ProtonRoute_AppendsToInheritedOverrides()
        {
            var plan = LinuxLaunchPlanner.BuildGameLaunchPlan(SteamGame(), "-modname W40k",
                Host(env: new() { ["WINEDLLOVERRIDES"] = "dinput8=n,b" }, installedProton: "/p/proton"), out _, "d3d9=n,b");

            Assert.Equal(LaunchRoute.Proton, plan!.Route);
            Assert.Equal("dinput8=n,b;d3d9=n,b", plan.StartInfo.Environment["WINEDLLOVERRIDES"]);
        }

        [Fact]
        public void DllOverrides_InheritedValueForSameDll_IsLeftAlone()
        {
            var plan = LinuxLaunchPlanner.BuildGameLaunchPlan(GogGame(), "-modname W40k",
                Host(path: new() { ["wine"] = "/usr/bin/wine" }, env: new() { ["WINEDLLOVERRIDES"] = "d3d9=b" }), out _, "d3d9=n,b");

            Assert.DoesNotContain("WINEDLLOVERRIDES", plan!.Description);
        }

        [Fact]
        public void DllOverrides_SteamRoute_ExplainsLaunchOptions()
        {
            var plan = LinuxLaunchPlanner.BuildGameLaunchPlan(SteamGame(), "-modname W40k",
                Host(path: new() { ["steam"] = "/usr/bin/steam" }), out _, "d3d9,d3d8=n,b");

            Assert.Equal(LaunchRoute.Steam, plan!.Route);
            Assert.Contains("WINEDLLOVERRIDES=\"d3d9,d3d8=n,b\" %command%", plan.Description);
        }

        // ---- run in prefix ------------------------------------------------------------------

        [Fact]
        public void PrefixRun_SteamGame_UsesProtonWithCompatData()
        {
            var plan = LinuxLaunchPlanner.BuildPrefixRunPlan(SteamGame(), "/dl/Setup.exe",
                Host(path: new() { ["steam"] = "/usr/bin/steam" }, installedProton: "/p/proton"), out _);

            Assert.NotNull(plan);
            Assert.Equal(LaunchRoute.Proton, plan!.Route);
            Assert.Equal(["run", "/dl/Setup.exe"], plan.StartInfo.ArgumentList);
            Assert.Equal("/steam/steamapps/compatdata/9450", plan.StartInfo.Environment["STEAM_COMPAT_DATA_PATH"]);
        }

        private const string Sniper = "/steam/steamapps/common/SteamLinuxRuntime_sniper/_v2-entry-point";

        [Fact]
        public void PrefixRun_ProtonNeedingRuntime_RunsInsideIt()
        {
            var plan = LinuxLaunchPlanner.BuildPrefixRunPlan(SteamGame(), "/dl/Setup.exe",
                Host(installedProton: "/p/proton", runtime: new SteamRuntimeRequirement(1628350, Sniper)), out _);

            Assert.NotNull(plan);
            Assert.Equal(LaunchRoute.Proton, plan!.Route);
            Assert.Equal(Sniper, plan.StartInfo.FileName);
            Assert.Equal(["--verb=waitforexitandrun", "--", "/p/proton", "waitforexitandrun", "/dl/Setup.exe"],
                         plan.StartInfo.ArgumentList);
            Assert.Equal("/steam/steamapps/compatdata/9450", plan.StartInfo.Environment["STEAM_COMPAT_DATA_PATH"]);
            Assert.Equal($"{Path.GetDirectoryName("/p/proton")}:{Path.GetDirectoryName(Sniper)}", plan.StartInfo.Environment["STEAM_COMPAT_TOOL_PATHS"]);
            Assert.Equal($"{Path.GetDirectoryName("/dl/Setup.exe")}:/steam/steamapps/compatdata/9450", plan.StartInfo.Environment["STEAM_COMPAT_MOUNTS"]);
            Assert.Equal(SteamGame().InstallDir, plan.StartInfo.Environment["STEAM_COMPAT_INSTALL_PATH"]);
            Assert.Contains(Sniper, plan.Description);
        }

        [Fact]
        public void ProtonOverride_NeedingRuntime_LaunchesGameInsideIt()
        {
            var host = Host(env: new() { ["DOWMM_PROTON"] = "/opt/GE-Proton9/proton" },
                            files: ["/opt/GE-Proton9/proton"],
                            runtime: new SteamRuntimeRequirement(1628350, Sniper));

            var plan = Plan(SteamGame(), host);

            Assert.Equal(Sniper, plan.StartInfo.FileName);
            Assert.Equal(["--verb=waitforexitandrun", "--", "/opt/GE-Proton9/proton", "waitforexitandrun",
                          SteamGame().ExecutablePath, "-modname", "W40k", "-nomovies"],
                         plan.StartInfo.ArgumentList);
        }

        [Fact]
        public void ProtonNeedingMissingRuntime_RunsDirectlyWithNote()
        {
            var plan = LinuxLaunchPlanner.BuildPrefixRunPlan(SteamGame(), "/dl/Setup.exe",
                Host(installedProton: "/p/proton", runtime: new SteamRuntimeRequirement(1628350, null)), out _);

            Assert.NotNull(plan);
            Assert.Equal("/p/proton", plan!.StartInfo.FileName);
            Assert.Equal(["run", "/dl/Setup.exe"], plan.StartInfo.ArgumentList);
            Assert.False(plan.StartInfo.Environment.ContainsKey("STEAM_COMPAT_TOOL_PATHS"));
            Assert.Contains("Steam app 1628350", plan.Description);
        }

        private const string SnapHome = Home + "/snap/steam/common";

        private static GameInstall SnapSteamGame() => new()
        {
            InstallDir = "/lib/steamapps/common/Dawn of War Definitive Edition",
            ExecutableName = "W40k.exe",
            Variant = GameVariant.DefinitiveEdition,
            Distribution = GameDistribution.Steam,
            SteamAppId = 3556750,
            ProtonPrefix = "/lib/steamapps/compatdata/3556750",
            SteamRoot = SnapHome + "/.local/share/Steam"
        };

        [Fact]
        public void PrefixRun_SnapSteam_GivesProtonTheSnapHome()
        {
            if (OperatingSystem.IsWindows())
                return; // Linux paths; Windows has no HOME
            var plan = LinuxLaunchPlanner.BuildPrefixRunPlan(SnapSteamGame(), "/dl/Setup.exe",
                Host(files: [SnapHome + "/.steam/sdk64/steamclient.so"], installedProton: "/p/proton"), out _);

            Assert.NotNull(plan);
            Assert.Equal(SnapHome, plan!.StartInfo.Environment["HOME"]);
            Assert.Contains("HOME=" + SnapHome, plan.Description);
        }

        [Fact]
        public void PrefixRun_SteamBridgeInOwnHome_KeepsHome()
        {
            if (OperatingSystem.IsWindows())
                return; // Linux paths; Windows has no HOME
            var plan = LinuxLaunchPlanner.BuildPrefixRunPlan(SnapSteamGame(), "/dl/Setup.exe",
                Host(files: [SnapHome + "/.steam/sdk64/steamclient.so", Home + "/.steam/sdk64/steamclient.so"],
                     installedProton: "/p/proton"), out _);

            Assert.NotNull(plan);
            Assert.NotEqual(SnapHome, plan!.StartInfo.Environment["HOME"]);
        }

        [Fact]
        public void PrefixRun_DowmmWinePrefix_ForcesWine()
        {
            var plan = LinuxLaunchPlanner.BuildPrefixRunPlan(SteamGame(), "/dl/Setup.exe",
                Host(path: new() { ["wine"] = "/usr/bin/wine" }, env: new() { ["DOWMM_WINEPREFIX"] = "/forced" },
                     installedProton: "/p/proton"), out _);

            Assert.NotNull(plan);
            Assert.Equal(LaunchRoute.Wine, plan!.Route);
            Assert.Equal(["/dl/Setup.exe"], plan.StartInfo.ArgumentList);
            Assert.Equal("/forced", plan.StartInfo.Environment["WINEPREFIX"]);
        }

        [Fact]
        public void PrefixRun_GogGame_UsesWine()
        {
            var plan = LinuxLaunchPlanner.BuildPrefixRunPlan(GogGame(), "/dl/Setup.exe",
                Host(path: new() { ["wine"] = "/usr/bin/wine" }, installedProton: "/p/proton"), out _);

            Assert.Equal(LaunchRoute.Wine, plan!.Route);
        }

        // ---- SplitArguments -----------------------------------------------------------------

        [Theory]
        [InlineData("-modname W40k -nomovies", new[] { "-modname", "W40k", "-nomovies" })]
        [InlineData("  -modname   W40k  ", new[] { "-modname", "W40k" })]
        [InlineData("-modname \"My Mod\" -dev", new[] { "-modname", "My Mod", "-dev" })]
        [InlineData("-modname My Mod", new[] { "-modname", "My", "Mod" })]
        [InlineData("-modname \"\"", new[] { "-modname", "" })]
        [InlineData("", new string[0])]
        public void SplitArguments_HonoursQuotes(string input, string[] expected)
        {
            Assert.Equal(expected, LinuxLaunchPlanner.SplitArguments(input));
        }

        [Theory]
        [InlineData("-modname My Mod -nomovies")]
        [InlineData("-modname \"My Mod\" -nomovies")]
        public void SplitArguments_RoundTripsThroughWindowsCommandLine(string input)
        {
            // Wine/Proton rebuild the Windows command line by quoting arguments that contain spaces.
            var rebuilt = string.Join(' ', LinuxLaunchPlanner.SplitArguments(input)
                .Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

            Assert.Equal(input, rebuilt);
        }

        [Fact]
        public void ModNameWithSpaces_ReachesSteamAsSeparateArguments()
        {
            var options = new LaunchOptions { ModName = "My Mod", NoMovies = true };
            var plan = Plan(SteamGame(), Host(path: new() { ["steam"] = "/usr/bin/steam" }), options.ToArgumentString());

            Assert.Equal(["-applaunch", "9450", "-modname", "My", "Mod", "-nomovies"], plan.StartInfo.ArgumentList);
        }

        [Fact]
        public void SteamRunUrl_EncodesArguments()
        {
            Assert.Equal("steam://run/9450//-modname%20%22My%20Mod%22/", LinuxLaunchPlanner.BuildSteamRunUrl(9450, "-modname \"My Mod\""));
            Assert.Equal("steam://run/9450", LinuxLaunchPlanner.BuildSteamRunUrl(9450, " "));
        }

        // ---- game process finder (fake /proc) -----------------------------------------------

        private void FakeProc(int pid, string? cmdline, string? comm = null)
        {
            var dir = Path.Combine(_root, pid.ToString());
            Directory.CreateDirectory(dir);
            if (cmdline is not null)
                File.WriteAllText(Path.Combine(dir, "cmdline"), cmdline);
            if (comm is not null)
                File.WriteAllText(Path.Combine(dir, "comm"), comm + "\n");
        }

        private void BuildFakeProcTree()
        {
            Directory.CreateDirectory(Path.Combine(_root, "self"));
            FakeProc(100, "/home/tester/.steam/steam/ubuntu12_32/reaper\0SteamLaunch\0AppId=9450\0--\0/steam/proton\0waitforexitandrun\0/steam/steamapps/common/Soulstorm/Soulstorm.exe\0", "reaper");
            FakeProc(101, "python3\0/steam/proton\0waitforexitandrun\0/steam/steamapps/common/Soulstorm/Soulstorm.exe\0", "python3");
            FakeProc(102, "C:\\windows\\system32\\services.exe\0", "services.exe");
            FakeProc(103, "Z:\\steam\\steamapps\\common\\Soulstorm\\Soulstorm.exe\0-modname\0W40k\0", "Soulstorm.exe");
            FakeProc(104, null, "DarkCrusade.exe");                          // cmdline unreadable, comm matches
            FakeProc(105, "C:\\Games\\W40k\\W40kWA.exe\0", "W40kWA.exe");
            FakeProc(106, "/usr/bin/bash\0", "bash");
        }

        [Fact]
        public void ProcFinder_MatchesArgv0OrComm_NotWrappers()
        {
            BuildFakeProcTree();
            var finder = new ProcGameProcessFinder(_root);

            Assert.Equal([103], finder.FindProcesses("Soulstorm.exe"));
            Assert.Equal([103], finder.FindProcesses("soulstorm.EXE"));
            Assert.Equal([104], finder.FindProcesses("DarkCrusade.exe"));
            Assert.Equal([105], finder.FindProcesses("W40kWA.exe"));
            Assert.Empty(finder.FindProcesses("W40k.exe"));
        }

        [Fact]
        public void ProcFinder_MissingRoot_ReturnsEmpty()
        {
            Assert.Empty(new ProcGameProcessFinder(Path.Combine(_root, "missing")).FindProcesses("Soulstorm.exe"));
        }

        [Fact]
        public async Task WaitForNewProcess_IgnoresPreexistingAndFindsLateProcess()
        {
            BuildFakeProcTree();
            var finder = new ProcGameProcessFinder(_root);
            var late = Task.Run(async () =>
            {
                await Task.Delay(150);
                FakeProc(200, "Z:\\x\\Soulstorm.exe\0", "Soulstorm.exe");
            });

            var pid = await finder.WaitForNewProcessAsync("Soulstorm.exe", [103],
                TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20));
            await late;

            Assert.Equal(200, pid);
        }

        [Fact]
        public async Task WaitForNewProcess_TimesOutWithNull()
        {
            var finder = new ProcGameProcessFinder(_root);
            var pid = await finder.WaitForNewProcessAsync("Soulstorm.exe", [],
                TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(20));
            Assert.Null(pid);
        }

        [Fact]
        public async Task WaitForNewProcess_IsCancellable()
        {
            var finder = new ProcGameProcessFinder(_root);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => finder.WaitForNewProcessAsync(
                "Soulstorm.exe", [], TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(20), ct: cts.Token));
        }

        // ---- LaunchAsync end to end (fake start + fake finder) ------------------------------

        private sealed class ScriptedFinder(params int[][] scans) : IGameProcessFinder
        {
            private int _call;
            public IReadOnlyList<int> FindProcesses(string exeName) => scans[Math.Min(_call++, scans.Length - 1)];
        }

        private static LinuxGameLauncher Launcher(IGameProcessFinder finder, List<ProcessStartInfo> started) => new(
            Host(path: new() { ["steam"] = "/usr/bin/steam" }),
            finder,
            gameStartTimeout: TimeSpan.FromMilliseconds(200),
            pollInterval: TimeSpan.FromMilliseconds(10),
            startProcess: psi => { started.Add(psi); return Process.GetCurrentProcess(); });

        [Fact]
        public async Task LaunchViaSteam_ReportsGamePidNotSteamPid()
        {
            var started = new List<ProcessStartInfo>();
            // 1st scan = before launch (an old instance 50), then nothing, then the new game 4242.
            var launcher = Launcher(new ScriptedFinder([50], [50], [50, 4242]), started);

            var launched = await launcher.LaunchAsync(SteamGame(), new LaunchOptions());

            // Returns at once: the Steam client pid is not reported as the game.
            Assert.True(launched.Started);
            Assert.Equal(0, launched.ProcessId);
            Assert.NotNull(launched.GameConfirmed);

            var result = await launched.GameConfirmed!;
            Assert.True(result.Started);
            Assert.Equal(4242, result.ProcessId);
            Assert.Equal("Soulstorm.exe", result.ProcessName);
            Assert.Equal("/usr/bin/steam", Assert.Single(started).FileName);
        }

        [Fact]
        public async Task LaunchViaSteam_GameNotFound_StartedWithUnconfirmedDetail()
        {
            var started = new List<ProcessStartInfo>();
            var launcher = Launcher(new ScriptedFinder(Array.Empty<int>()), started);

            var result = await (await launcher.LaunchAsync(SteamGame(), new LaunchOptions())).GameConfirmed!;

            Assert.True(result.Started);
            Assert.Equal(0, result.ProcessId);
            Assert.Contains("could not be confirmed", result.Detail);
            Assert.Contains("confirm the launch arguments", result.Detail);
        }

        [Fact]
        public async Task Launch_NoRoute_FailsWithDetail()
        {
            var launcher = new LinuxGameLauncher(Host(), new ScriptedFinder(Array.Empty<int>()), startProcess: _ => throw new InvalidOperationException());

            var result = await launcher.LaunchAsync(GogGame(), new LaunchOptions());

            Assert.False(result.Started);
            Assert.False(string.IsNullOrEmpty(result.Detail));
            Assert.Null(result.GameConfirmed);
        }

        [Fact]
        public async Task LaunchDirectWine_EarlyNonZeroExit_ReportsFailureAsynchronously()
        {
            // `sh -c "exit 3"` stands in for a wine/proton command that dies before the game appears.
            if (OperatingSystem.IsWindows())
                return;

            var launcher = new LinuxGameLauncher(
                Host(path: new() { ["wine"] = "/usr/bin/wine" }),
                new ScriptedFinder(Array.Empty<int>()),
                gameStartTimeout: TimeSpan.FromSeconds(10),
                pollInterval: TimeSpan.FromMilliseconds(10),
                startProcess: _ => Process.Start(new ProcessStartInfo("/bin/sh") { ArgumentList = { "-c", "exit 3" } }));

            var launched = await launcher.LaunchAsync(GogGame(), new LaunchOptions());
            Assert.True(launched.Started);

            var result = await launched.GameConfirmed!;
            Assert.False(result.Started);
            Assert.Contains("exited with code 3", result.Detail);
        }

        [Fact]
        public async Task Launch_CancelledWait_CompletesWithoutThrowing()
        {
            using var cts = new CancellationTokenSource();
            var launcher = new LinuxGameLauncher(
                Host(path: new() { ["steam"] = "/usr/bin/steam" }), new ScriptedFinder(Array.Empty<int>()),
                gameStartTimeout: TimeSpan.FromSeconds(30), pollInterval: TimeSpan.FromMilliseconds(10),
                startProcess: _ => Process.GetCurrentProcess());

            var launched = await launcher.LaunchAsync(SteamGame(), new LaunchOptions(), cts.Token);
            cts.Cancel();
            var result = await launched.GameConfirmed!;

            Assert.True(result.Started);
            Assert.Contains("cancelled", result.Detail);
        }

        // ---- fixed-base installers (relocations stripped + ASLR flag) ----------------------

        /// <summary>A minimal PE header: PE32 or PE32+, with the given characteristics.</summary>
        private static byte[] PeHeader(bool relocsStripped, bool dynamicBase, bool pe32 = true)
        {
            var bytes = new byte[512];
            bytes[0] = (byte)'M';
            bytes[1] = (byte)'Z';
            BitConverter.GetBytes(0x80).CopyTo(bytes, 0x3C);
            "PE\0\0"u8.ToArray().CopyTo(bytes, 0x80);
            BitConverter.GetBytes((ushort)(relocsStripped ? 0x0103 : 0x0102)).CopyTo(bytes, 0x80 + 22);
            BitConverter.GetBytes((ushort)(pe32 ? 0x10b : 0x20b)).CopyTo(bytes, 0x80 + 24);
            BitConverter.GetBytes((ushort)(dynamicBase ? 0x8540 : 0x8500)).CopyTo(bytes, 0x80 + 24 + 70);
            return bytes;
        }

        [Theory]
        [InlineData(true, true, true, true)]
        [InlineData(false, true, true, false)]
        [InlineData(true, false, true, false)]
        [InlineData(true, true, false, false)]
        public void FixedBase_OnlyStrippedRelocsWithAslrOn32Bit_NeedsACopy(bool stripped, bool aslr, bool pe32, bool expected)
        {
            Assert.Equal(expected, FixedBaseWorkaround.NeedsCopy(PeHeader(stripped, aslr, pe32)));
            Assert.False(FixedBaseWorkaround.NeedsCopy("not a PE file"u8));
        }

        [Fact]
        public void FixedBase_CopySitsBesideTheOriginal_WithoutTheAslrFlag()
        {
            Directory.CreateDirectory(_root);
            string exe = Path.Combine(_root, "Setup.exe");
            File.WriteAllBytes(exe, PeHeader(relocsStripped: true, dynamicBase: true));

            string? copy = FixedBaseWorkaround.CreateCopy(exe);

            Assert.Equal(Path.Combine(_root, ".Setup.dmm.exe"), copy);
            Assert.False(FixedBaseWorkaround.NeedsCopy(File.ReadAllBytes(copy!)));
            Assert.True(FixedBaseWorkaround.NeedsCopy(File.ReadAllBytes(exe)));
            Assert.Null(FixedBaseWorkaround.CreateCopy(copy!));
        }

        [Fact]
        public async Task PrefixRun_FixedBaseInstaller_RunsTheCopy_ThenDeletesIt()
        {
            if (OperatingSystem.IsWindows())
                return;
            Directory.CreateDirectory(_root);
            string exe = Path.Combine(_root, "Setup.exe");
            File.WriteAllBytes(exe, PeHeader(relocsStripped: true, dynamicBase: true));
            string copy = Path.Combine(_root, ".Setup.dmm.exe");
            var started = new List<ProcessStartInfo>();
            var launcher = new LinuxGameLauncher(Host(installedProton: "/p/proton"), new ScriptedFinder(Array.Empty<int>()),
                startProcess: psi =>
                {
                    started.Add(psi);
                    return Process.Start(new ProcessStartInfo("/bin/sh") { ArgumentList = { "-c", "sleep 0.2" } });
                });

            var result = await launcher.RunInGamePrefixAsync(SteamGame(), exe);

            Assert.True(result.Started, result.Detail);
            Assert.Equal(["run", copy], started.Single().ArgumentList);
            Assert.True(SpinWait.SpinUntil(() => !File.Exists(copy), TimeSpan.FromSeconds(5)));
            Assert.True(File.Exists(exe));
        }
    }
}
