using System.IO.Compression;
using System.Text;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Services;
using Xunit;

namespace DowModManager.Tests
{
    /// <summary>
    /// One-click mod install: downloads folder, browser-download watching, archive layout and
    /// extraction, and install/uninstall through a fake launcher on a temp game folder.
    /// </summary>
    public class ModInstallTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "dmm_install_" + Guid.NewGuid().ToString("N"));
        private readonly string _game;
        private readonly string _downloads;
        private readonly FakeLauncher _launcher = new();

        public ModInstallTests()
        {
            _game = Directory.CreateDirectory(Path.Combine(_root, "game")).FullName;
            _downloads = Directory.CreateDirectory(Path.Combine(_root, "downloads")).FullName;
            File.WriteAllText(Path.Combine(_game, "W40k.module"), "[global]\n");
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        private GameInstall Install(string? modsDir = null) => new()
        {
            InstallDir = _game,
            ExecutableName = "W40k.exe",
            Variant = GameVariant.DefinitiveEdition,
            ModsDir = modsDir
        };

        private ModInstallService Service() =>
            new(_launcher, isWindows: false, new InstalledModsStore(Path.Combine(_root, "store.json")), _downloads,
                TimeSpan.FromMilliseconds(10));

        private static readonly DownloadableMod Unification = new(
            "Unification Mod for AE and DE   (Version 7.3.8: Archive + Installer)", "Unification_New_Races",
            "https://site/unification", "https://dl/299481", "https://dl/299482");

        private static readonly DownloadableMod Zipped = new("My Zipped Mod", "MyMod", "https://site/mymod", "https://dl/mymod", "");

        private static readonly Progress<ModInstallProgress> NoProgress = new();

        private string Zip(string name, params (string Key, string Content)[] entries)
        {
            string path = Path.Combine(_downloads, name);
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var (key, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(key).Open());
                writer.Write(content);
            }
            return path;
        }

        private string Installer(string name, string referencedArchive)
        {
            string path = Path.Combine(_downloads, name);
            File.WriteAllBytes(path, [.. Encoding.ASCII.GetBytes("MZ stub "), .. Encoding.Unicode.GetBytes("\\" + referencedArchive), 0, 0]);
            return path;
        }

        // ---- downloads folder ----------------------------------------------------------------

        [Theory]
        [InlineData("XDG_DOWNLOAD_DIR=\"$HOME/Téléchargements\"\n", "/home/u/Téléchargements")]
        [InlineData("# comment\nXDG_DESKTOP_DIR=\"$HOME/Bureau\"\nXDG_DOWNLOAD_DIR=\"/data/dl/\"\n", "/data/dl")]
        [InlineData("XDG_DOWNLOAD_DIR=\"$HOME/\"\n", null)]
        [InlineData("XDG_MUSIC_DIR=\"$HOME/Music\"\n", null)]
        public void XdgUserDirs_GiveTheDownloadFolder(string text, string? expected)
        {
            Assert.Equal(expected, DownloadsFolder.ParseXdgUserDirs(text, "/home/u"));
        }

        // ---- browser download watching -------------------------------------------------------

        [Fact]
        public async Task Watcher_WaitsForFirefoxPartFile_ThenReturnsTheFile()
        {
            File.WriteAllText(Path.Combine(_downloads, "old.zip"), "old");
            var existing = DownloadWatcher.Snapshot(_downloads);
            var reports = new List<DownloadProgress>();
            var watcher = new DownloadWatcher(TimeSpan.FromMilliseconds(20));

            var wait = watcher.WaitForNewFileAsync(_downloads, existing, new SyncProgress<DownloadProgress>(reports.Add));

            // Firefox: an empty placeholder plus a growing .part file, renamed over it at the end.
            string final = Path.Combine(_downloads, "Unification.7z");
            File.WriteAllText(final, "");
            File.WriteAllText(final + ".part", "12345");
            await Task.Delay(100);
            Assert.False(wait.IsCompleted);

            File.Move(final + ".part", final, overwrite: true);
            string path = await wait.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(final, path);
            Assert.Contains(reports, r => r.FileName == "Unification.7z" && r.Bytes == 5);
        }

        [Fact]
        public async Task Watcher_HonoursCancellation()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var watcher = new DownloadWatcher(TimeSpan.FromMilliseconds(20));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                watcher.WaitForNewFileAsync(_downloads, DownloadWatcher.Snapshot(_downloads), null, cts.Token));
        }

        // ---- archive layout and extraction ---------------------------------------------------

        [Theory]
        [InlineData(new[] { "MyMod.module", "MyMod/Data/a.txt" }, "")]
        [InlineData(new[] { "MyMod v1.2/readme.txt", "MyMod v1.2/MyMod.module", "MyMod v1.2/MyMod/Data/a.txt" }, "MyMod v1.2/")]
        [InlineData(new[] { "a\\b\\Deep.module", "a\\Top.module" }, "a/")]
        [InlineData(new[] { "readme.txt" }, null)]
        public void ContentRoot_IsTheShallowestFolderWithAModule(string[] keys, string? expected)
        {
            Assert.Equal(expected, ModArchive.FindContentRoot(keys));
        }

        [Fact]
        public void InstallerReferences_FindsAsciiAndUtf16Names()
        {
            string exe = Installer("Unification-v7.3.8.exe", "Unification.7z");
            File.AppendAllText(exe, "Other.zip");

            Assert.True(ModArchive.InstallerReferences(exe, "Unification.7z"));
            Assert.True(ModArchive.InstallerReferences(exe, "Other.zip"));
            Assert.False(ModArchive.InstallerReferences(exe, "Missing.7z"));
        }

        [Fact]
        public async Task Extract_PutsTheContentRootInTheGameFolder_ReusingFolderCasing()
        {
            Directory.CreateDirectory(Path.Combine(_game, "MyMod", "data"));
            File.WriteAllText(Path.Combine(_game, "MyMod", "data", "keep.txt"), "game file");
            string zip = Zip("mymod.zip",
                ("MyMod v1/readme.txt", "r"), ("MyMod v1/MyMod.module", "[global]"), ("MyMod v1/MyMod/Data/a.txt", "a"),
                ("MyMod v1/MyMod/Data/keep.txt", "replaced"));

            var result = await ModArchive.ExtractAsync(zip, _game);

            Assert.True(File.Exists(Path.Combine(_game, "MyMod.module")));
            Assert.Equal("a", File.ReadAllText(Path.Combine(_game, "MyMod", "data", "a.txt")));
            if (!OperatingSystem.IsWindows())
                Assert.False(Directory.Exists(Path.Combine(_game, "MyMod", "Data"))); // went into the existing "data"
            // Windows paths ignore case: the replaced file may be reported as Data\keep.txt there.
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            Assert.Contains(result.Overwritten, p => string.Equals(p, Path.Combine(_game, "MyMod", "data", "keep.txt"), comparison));
            Assert.Equal(3, result.Created.Count);
        }

        [Fact]
        public async Task Extract_RefusesEntriesOutsideTheGameFolder()
        {
            string zip = Zip("evil.zip", ("Evil.module", "[global]"), ("../outside.txt", "x"));

            await Assert.ThrowsAsync<InvalidDataException>(() => ModArchive.ExtractAsync(zip, _game));
            Assert.False(File.Exists(Path.Combine(_root, "outside.txt")));
        }

        // ---- install / uninstall -------------------------------------------------------------

        [Fact]
        public async Task Install_InstallerWithItsArchive_RunsOnlyTheInstaller()
        {
            string exe = Installer("Unification-v7.3.8.exe", "Unification.7z");
            string payload = Path.Combine(_downloads, "Unification.7z");
            File.WriteAllText(payload, "not extracted by the manager");
            _launcher.OnRun = _ =>
            {
                File.WriteAllText(Path.Combine(_game, "Unification_New_Races.module"), "[global]");
                File.WriteAllText(Path.Combine(_game, "Unification-v7.3.8-Uninstall.exe"), "MZ");
            };

            var result = await Service().InstallAsync(Install(), Unification, [payload, exe], NoProgress);

            Assert.True(result.Success, result.Message);
            Assert.Equal([exe], _launcher.Runs);
            Assert.True(Service().IsInstalled(Install(), Unification));
            Assert.True(Service().CanUninstall(Install(), Unification));
        }

        [Fact]
        public async Task Install_InstallerThatDoesNotCreateTheModule_Fails()
        {
            string exe = Installer("Unification-v7.3.8.exe", "Unification.7z");
            File.WriteAllText(Path.Combine(_downloads, "Unification.7z"), "x");

            var result = await Service().InstallAsync(Install(), Unification, [exe, Path.Combine(_downloads, "Unification.7z")], NoProgress);

            Assert.False(result.Success);
            Assert.Contains("Unification_New_Races.module", result.Message);
        }

        [Fact]
        public async Task ArchiveInstall_ThenUninstall_RemovesOnlyItsFiles()
        {
            string zip = Zip("mymod.zip", ("MyMod.module", "[global]"), ("MyMod/Data/a.txt", "a"), ("MyMod/Data/sub/b.txt", "b"));
            var service = Service();

            var installed = await service.InstallAsync(Install(), Zipped, [zip], NoProgress);
            Assert.True(installed.Success, installed.Message);
            Assert.True(service.CanUninstall(Install(), Zipped));

            var removed = await service.UninstallAsync(Install(), Zipped, NoProgress);

            Assert.True(removed.Success, removed.Message);
            Assert.False(Directory.Exists(Path.Combine(_game, "MyMod")));
            Assert.True(File.Exists(Path.Combine(_game, "W40k.module")));
            Assert.Empty(_launcher.Runs);
        }

        [Fact]
        public async Task Uninstall_ModInstalledElsewhere_RunsItsUninstaller()
        {
            File.WriteAllText(Path.Combine(_game, "Unification_New_Races.module"), "[global]");
            string uninstaller = Path.Combine(_game, "Unification-v7.3.8-Uninstall.exe");
            File.WriteAllText(uninstaller, "MZ");
            _launcher.OnRun = _ => File.Delete(Path.Combine(_game, "Unification_New_Races.module"));

            var result = await Service().UninstallAsync(Install(), Unification, NoProgress);

            Assert.True(result.Success, result.Message);
            Assert.Equal([uninstaller], _launcher.Runs);
        }

        [Fact]
        public async Task Uninstall_UnknownInstall_IsRefused()
        {
            File.WriteAllText(Path.Combine(_game, "MyMod.module"), "[global]");

            Assert.False(Service().CanUninstall(Install(), Zipped));
            var result = await Service().UninstallAsync(Install(), Zipped, NoProgress);

            Assert.False(result.Success);
            Assert.True(File.Exists(Path.Combine(_game, "MyMod.module")));
        }

        [Fact]
        public void IsInstalled_FindsModulesInTheDefinitiveEditionModsFolder()
        {
            string mods = Directory.CreateDirectory(Path.Combine(_root, "userdata", "Mods", "MyMod")).FullName;
            File.WriteAllText(Path.Combine(mods, "mymod.module"), "[global]");

            Assert.False(Service().IsInstalled(Install(), Zipped));
            Assert.True(Service().IsInstalled(Install(Path.GetDirectoryName(mods)), Zipped));
        }

        [Fact]
        public void DownloadedFiles_AreFoundByTheModsName_NewestFirstForEachLink()
        {
            File.WriteAllText(Path.Combine(_downloads, "Unification.7z"), "a");
            File.SetLastWriteTimeUtc(Path.Combine(_downloads, "Unification.7z"), DateTime.UtcNow.AddHours(-2));
            File.WriteAllText(Path.Combine(_downloads, "Unification-v7.3.8.exe"), "b");
            File.WriteAllText(Path.Combine(_downloads, "Unification-v7.3.8-Uninstall.exe"), "c");
            File.WriteAllText(Path.Combine(_downloads, "OtherMod.zip"), "d");

            var files = Service().FindDownloadedFiles(Unification).Select(Path.GetFileName);

            Assert.Equal(["Unification.7z", "Unification-v7.3.8.exe"], files);
            Assert.Empty(Service().FindDownloadedFiles(Zipped));
        }

        [Fact]
        public void DownloadedFiles_PreferTheNamesSeenForEachLink()
        {
            var store = new InstalledModsStore(Path.Combine(_root, "store.json"));
            store.RememberDownload("https://dl/mymod", "renamed-by-moddb.zip");
            File.WriteAllText(Path.Combine(_downloads, "renamed-by-moddb.zip"), "a");

            Assert.Equal([Path.Combine(_downloads, "renamed-by-moddb.zip")], Service().FindDownloadedFiles(Zipped));
        }

        [Theory]
        [InlineData("Unification_New_Races", "Unification Mod", "Unification")]
        [InlineData("", "Advanced Campaign Mod (DoW and WA Campaigns - Version 3.7)", "Advanced")]
        [InlineData("UltimateApocalypse_THB", "Ultimate Apocalypse Mod", "UltimateApocalypse")]
        public void Token_IsTheModsMainWord(string module, string name, string expected)
        {
            Assert.Equal(expected, ModInstallService.Token(new DownloadableMod(name, module, "", "", "")));
        }

        private sealed class FakeLauncher : IGameLauncher
        {
            public List<string> Runs { get; } = [];
            public Action<string>? OnRun { get; set; }

            public Task<GameLaunchResult> LaunchAsync(GameInstall game, LaunchOptions options, CancellationToken ct = default) =>
                throw new NotSupportedException();

            public Task<GameLaunchResult> RunInGamePrefixAsync(GameInstall game, string exePath, CancellationToken ct = default)
            {
                Runs.Add(exePath);
                OnRun?.Invoke(exePath);
                return Task.FromResult(new GameLaunchResult { Started = true, ProcessId = 0 });
            }
        }

        private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
        {
            public void Report(T value) => report(value);
        }
    }
}
