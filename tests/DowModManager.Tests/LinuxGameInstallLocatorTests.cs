using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Platform.Linux;
using Xunit;

namespace DowModManager.Tests
{
    /// <summary>
    /// Fake Steam trees in temp dirs. The locator is fed explicit Steam client roots (internal seam),
    /// so nothing depends on the real $HOME and the tests also run on Windows CI.
    /// </summary>
    public class LinuxGameInstallLocatorTests : IDisposable
    {
        private readonly string _tmp;
        private readonly string _steamApps;
        private readonly LinuxGameInstallLocator _locator;

        public LinuxGameInstallLocatorTests()
        {
            _tmp = Path.Combine(Path.GetTempPath(), "dmm_locator_" + Guid.NewGuid().ToString("N"));
            _steamApps = Path.Combine(_tmp, "steamapps");
            Directory.CreateDirectory(Path.Combine(_steamApps, "common"));
            _locator = new LinuxGameInstallLocator(Array.Empty<string>());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tmp))
                Directory.Delete(_tmp, recursive: true);
        }

        // ------------------------------------------------------------------ helpers

        private string CreateGame(string folderName, string exe) => CreateGameIn(_steamApps, folderName, exe);

        private static string CreateGameIn(string steamApps, string folderName, params string[] files)
        {
            var dir = Path.Combine(steamApps, "common", folderName);
            Directory.CreateDirectory(dir);
            foreach (var file in files)
                File.WriteAllText(Path.Combine(dir, file), "");
            return dir;
        }

        private void WriteManifest(int appId, string installDir) => WriteManifestIn(_steamApps, appId, installDir);

        private static void WriteManifestIn(string steamApps, int appId, string installDir) =>
            File.WriteAllText(Path.Combine(steamApps, $"appmanifest_{appId}.acf"),
                $"\"AppState\"\n{{\n\t\"appid\"\t\t\"{appId}\"\n\t\"installdir\"\t\t\"{installDir}\"\n}}\n");

        /// <summary>Creates a Steam client root (with its own steamapps) under the temp dir.</summary>
        private string CreateRoot(params string[] relative)
        {
            var root = Path.Combine([_tmp, .. relative]);
            Directory.CreateDirectory(Path.Combine(root, "steamapps", "common"));
            return root;
        }

        /// <summary>Writes a modern libraryfolders.vdf listing the given libraries (VDF-escaped).</summary>
        private static void WriteLibraryFolders(string steamRoot, params string[] libraries)
        {
            var body = string.Concat(libraries.Select((lib, i) =>
                $"\t\"{i}\"\n\t{{\n\t\t\"path\"\t\t\"{lib.Replace("\\", "\\\\")}\"\n\t\t\"label\"\t\t\"\"\n" +
                "\t\t\"apps\"\n\t\t{\n\t\t}\n\t}\n"));
            File.WriteAllText(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"),
                $"\"libraryfolders\"\n{{\n{body}}}\n");
        }

        private static async Task<GameInstall> DiscoverSingle(LinuxGameInstallLocator locator)
        {
            var installs = await locator.DiscoverAsync();
            return Assert.Single(installs);
        }

        // ------------------------------------------------------------------ IdentifyInstall

        [Fact]
        public void IdentifyInstall_UsesAppIdFromMatchingManifest()
        {
            var dir = CreateGame("Dawn of War Definitive Edition", "W40k.exe");
            WriteManifest(9450, "Dawn of War Soulstorm");
            WriteManifest(3556750, "Dawn of War Definitive Edition");

            var install = _locator.IdentifyInstall(dir);

            Assert.NotNull(install);
            Assert.Equal(3556750, install!.SteamAppId);
            Assert.Equal(GameVariant.DefinitiveEdition, install.Variant);
            Assert.Equal(Path.Combine(_steamApps, "compatdata", "3556750"), install.ProtonPrefix);
        }

        [Fact]
        public void IdentifyInstall_FallsBackToVariantAppIdWithoutManifest()
        {
            var dir = CreateGame("Dawn of War Soulstorm", "Soulstorm.exe");

            var install = _locator.IdentifyInstall(dir);

            Assert.NotNull(install);
            Assert.Equal(GameVariant.Soulstorm, install!.Variant);
            Assert.Equal(9450, install.SteamAppId);
        }

        [Theory]
        [InlineData("Dawn of War Soulstorm", "Soulstorm.exe", GameVariant.Soulstorm, 9450)]
        [InlineData("Dawn of War Dark Crusade", "DarkCrusade.exe", GameVariant.DarkCrusade, 4580)]
        [InlineData("Dawn of War Gold", "W40kWA.exe", GameVariant.WinterAssault, 4570)]
        [InlineData("Dawn of War Gold", "W40k.exe", GameVariant.Original, 4570)]
        public void IdentifyInstall_FallbackAppIdsMatchSteam(string folder, string exe, GameVariant variant, int appId)
        {
            var install = _locator.IdentifyInstall(CreateGame(folder, exe));

            Assert.NotNull(install);
            Assert.Equal(variant, install!.Variant);
            Assert.Equal(appId, install.SteamAppId);
            Assert.Equal(Path.Combine(_steamApps, "compatdata", appId.ToString()), install.ProtonPrefix);
        }

        [Fact]
        public void IdentifyInstall_AnniversaryEditionFolder_ResolvesToSoulstorm()
        {
            // Steam's Anniversary Edition + expansions all live in one folder.
            var dir = CreateGameIn(_steamApps, "Dawn of War Gold",
                "W40k.exe", "W40kWA.exe", "DarkCrusade.exe", "Soulstorm.exe");

            var install = _locator.IdentifyInstall(dir);

            Assert.Equal(GameVariant.Soulstorm, install!.Variant);
            Assert.Equal("Soulstorm.exe", install.ExecutableName);
            Assert.True(install.SupportsNoFog);
        }

        [Fact]
        public void IdentifyInstall_DefinitiveEditionFolder_WithoutManifest()
        {
            var dir = CreateGameIn(_steamApps, "Dawn of War Definitive Edition",
                "W40k.exe", "W40k.module", "WXP.module", "DXP2.module", "DXP3.module", "DoWDE.module");

            var install = _locator.IdentifyInstall(dir);

            Assert.NotNull(install);
            Assert.Equal(GameVariant.DefinitiveEdition, install!.Variant);
            Assert.Equal("W40k.exe", install.ExecutableName);
            Assert.Equal(3556750, install.SteamAppId);
            Assert.False(install.IsOldGame);
            Assert.False(install.SupportsNoFog);
            Assert.True(install.Is64Bit);
            Assert.Equal("DoWDE", install.BaseGameModuleName);
        }

        [Fact]
        public void IdentifyInstall_DefinitiveEditionDetectedFromSteamAppIdTxt()
        {
            var dir = CreateGame("Dawn of War Definitive Edition", "W40k.exe");
            File.WriteAllText(Path.Combine(dir, "steam_appid.txt"), "3556750\n");

            Assert.Equal(GameVariant.DefinitiveEdition, _locator.IdentifyInstall(dir)!.Variant);
        }

        [Fact]
        public void IdentifyInstall_OutsideSteam_HasNoSteamData()
        {
            var dir = Path.Combine(_tmp, "Games", "DoW");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Soulstorm.exe"), "");

            var install = _locator.IdentifyInstall(dir);

            Assert.Equal(GameDistribution.Unknown, install!.Distribution);
            Assert.Null(install.SteamAppId);
            Assert.Null(install.ProtonPrefix);
            Assert.Null(install.SteamRoot);
        }

        [Fact]
        public void IdentifyInstall_PicksRootWhoseLibraryFoldersListsTheLibrary()
        {
            // A leftover native root comes first, but only the Snap root knows the external library.
            var leftover = CreateRoot("home", ".steam", "steam");
            var snap = CreateRoot("home", "snap", "steam", "common", ".local", "share", "Steam");
            var library = Path.Combine(_tmp, "mnt", "steamssd", "steam");
            Directory.CreateDirectory(Path.Combine(library, "steamapps", "common"));
            WriteLibraryFolders(leftover, leftover);
            WriteLibraryFolders(snap, snap, library);
            var dir = CreateGameIn(Path.Combine(library, "steamapps"), "Dawn of War Soulstorm", "Soulstorm.exe");

            var install = new LinuxGameInstallLocator([leftover, snap]).IdentifyInstall(dir);

            Assert.Equal(snap, install!.SteamRoot);
            Assert.Equal(Path.Combine(library, "steamapps", "compatdata", "9450"), install.ProtonPrefix);
        }

        [Fact]
        public void IdentifyInstall_FallsBackToRootContainingTheGame()
        {
            var first = CreateRoot("home", ".steam", "steam");
            var flatpak = CreateRoot("home", ".var", "app", "com.valvesoftware.Steam", "data", "Steam");
            var dir = CreateGameIn(Path.Combine(flatpak, "steamapps"), "Dawn of War Soulstorm", "Soulstorm.exe");
            // Neither root has a libraryfolders.vdf.

            var install = new LinuxGameInstallLocator([first, flatpak]).IdentifyInstall(dir);

            Assert.Equal(flatpak, install!.SteamRoot);
        }

        [Fact]
        public void IdentifyInstall_FallsBackToFirstRootWhenNothingMatches()
        {
            var first = CreateRoot("home", ".steam", "steam");
            var second = CreateRoot("home", ".local", "share", "Steam");
            var dir = CreateGame("Dawn of War Soulstorm", "Soulstorm.exe"); // library unknown to both

            var install = new LinuxGameInstallLocator([first, second]).IdentifyInstall(dir);

            Assert.Equal(first, install!.SteamRoot);
        }

        // ------------------------------------------------------------------ DiscoverAsync

        [Theory]
        [InlineData(".local/share/Steam")]                               // native
        [InlineData(".var/app/com.valvesoftware.Steam/data/Steam")]      // Flatpak
        [InlineData(".var/app/com.valvesoftware.Steam/.local/share/Steam")] // older Flatpak
        [InlineData("snap/steam/common/.local/share/Steam")]             // Snap
        public async Task DiscoverAsync_FindsGameInDefaultRootLayouts(string relativeRoot)
        {
            var home = Path.Combine(_tmp, "home");
            var root = CreateRoot(["home", .. relativeRoot.Split('/')]);
            var dir = CreateGameIn(Path.Combine(root, "steamapps"), "Dawn of War Soulstorm", "Soulstorm.exe");
            WriteManifestIn(Path.Combine(root, "steamapps"), 9450, "Dawn of War Soulstorm");

            var locator = new LinuxGameInstallLocator(SteamPaths.DefaultRoots(home, steamDir: null));
            var install = await DiscoverSingle(locator);

            Assert.Equal(dir, install.InstallDir);
            Assert.Equal(GameVariant.Soulstorm, install.Variant);
            Assert.Equal(GameDistribution.Steam, install.Distribution);
            Assert.Equal(9450, install.SteamAppId);
            Assert.Equal(root, install.SteamRoot);
            Assert.Equal(Path.Combine(root, "steamapps", "compatdata", "9450"), install.ProtonPrefix);
        }

        [Fact]
        public async Task DiscoverAsync_FindsDefinitiveEditionInSecondaryLibrary()
        {
            var root = CreateRoot("home", ".local", "share", "Steam");
            var library = Path.Combine(_tmp, "mnt", "games", "SteamLibrary");
            var steamApps = Path.Combine(library, "steamapps");
            Directory.CreateDirectory(Path.Combine(steamApps, "common"));
            WriteLibraryFolders(root, root, library);
            var dir = CreateGameIn(steamApps, "Dawn of War Definitive Edition", "W40k.exe", "DoWDE.module");
            WriteManifestIn(steamApps, 3556750, "Dawn of War Definitive Edition");

            var install = await DiscoverSingle(new LinuxGameInstallLocator([root]));

            Assert.Equal(dir, install.InstallDir);
            Assert.Equal(GameVariant.DefinitiveEdition, install.Variant);
            Assert.Equal("W40k.exe", install.ExecutableName);
            Assert.Equal(3556750, install.SteamAppId);
            Assert.Equal(Path.Combine(steamApps, "compatdata", "3556750"), install.ProtonPrefix);
            Assert.Equal(root, install.SteamRoot);
        }

        [Fact]
        public async Task DiscoverAsync_LibrarySharedByTwoRoots_IsReportedOnceWithOwningRoot()
        {
            var leftover = CreateRoot("home", ".steam", "steam");
            var snap = CreateRoot("home", "snap", "steam", "common", ".local", "share", "Steam");
            var library = Path.Combine(_tmp, "mnt", "lib");
            Directory.CreateDirectory(Path.Combine(library, "steamapps", "common"));
            WriteLibraryFolders(leftover, leftover, library);
            WriteLibraryFolders(snap, snap, library);
            CreateGameIn(Path.Combine(library, "steamapps"), "Dawn of War Soulstorm", "Soulstorm.exe");

            var install = await DiscoverSingle(new LinuxGameInstallLocator([leftover, snap]));

            // Same rule as IdentifyInstall: the first root whose libraryfolders.vdf lists it.
            Assert.Equal(leftover, install.SteamRoot);
            Assert.Equal(install.SteamRoot, new LinuxGameInstallLocator([leftover, snap]).IdentifyInstall(install.InstallDir)!.SteamRoot);
        }

        [Fact]
        public async Task DiscoverAsync_MatchesFolderNameCaseInsensitively()
        {
            var root = CreateRoot("home", ".local", "share", "Steam");
            CreateGameIn(Path.Combine(root, "steamapps"), "dawn of war soulstorm", "Soulstorm.exe");

            var install = await DiscoverSingle(new LinuxGameInstallLocator([root]));

            Assert.Equal(GameVariant.Soulstorm, install.Variant);
        }

        [Fact]
        public async Task DiscoverAsync_IgnoresNonGameFolders()
        {
            var root = CreateRoot("home", ".local", "share", "Steam");
            CreateGameIn(Path.Combine(root, "steamapps"), "Dawn of War II", "DOW2.exe");
            CreateGameIn(Path.Combine(root, "steamapps"), "Proton 9.0", "proton");

            Assert.Empty(await new LinuxGameInstallLocator([root]).DiscoverAsync());
        }

        // ------------------------------------------------------------------ SteamPaths

        [Fact]
        public void DefaultRoots_HonoursSteamDirFirstAndCoversAllPackagings()
        {
            var home = Path.Combine(_tmp, "home");
            var custom = Path.Combine(_tmp, "custom-steam");

            var roots = SteamPaths.DefaultRoots(home, custom).ToList();

            Assert.Equal(custom, roots[0]);
            Assert.Contains(Path.Combine(home, ".steam", "steam"), roots);
            Assert.Contains(Path.Combine(home, ".local", "share", "Steam"), roots);
            Assert.Contains(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam"), roots);
            Assert.Contains(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"), roots);
            Assert.Contains(Path.Combine(home, "snap", "steam", "common", ".local", "share", "Steam"), roots);
        }

        [Fact]
        public void DefaultRoots_BlankHomeFallsBackToUserProfile()
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var roots = SteamPaths.DefaultRoots(home: "  ", steamDir: "").ToList();

            Assert.Equal(Path.Combine(profile, ".steam", "steam"), roots[0]);
        }

        [Fact]
        public void ExistingRoots_DeduplicatesSymlinkedRoots()
        {
            var real = CreateRoot("home", ".local", "share", "Steam");
            var link = Path.Combine(_tmp, "home", ".steam", "steam");
            Directory.CreateDirectory(Path.GetDirectoryName(link)!);
            try { Directory.CreateSymbolicLink(link, real); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return; // Windows without symlink privilege: nothing to verify.
            }

            Assert.Equal([link], SteamPaths.ExistingRoots([link, real, Path.Combine(_tmp, "missing")]));
        }

        [Fact]
        public void ReadAppId_HandlesEscapedInstallDir()
        {
            var dir = CreateGame("Dawn of War Soulstorm", "Soulstorm.exe");
            // A path-like, VDF-escaped value still names the folder.
            File.WriteAllText(Path.Combine(_steamApps, "appmanifest_9450.acf"),
                "\"AppState\"\n{\n\t\"appid\"\t\t\"9450\"\n\t\"installdir\"\t\t\"common\\\\Dawn of War Soulstorm\"\n}\n");

            Assert.Equal(9450, SteamPaths.ReadAppId(_steamApps, dir));
        }

        [Theory]
        [InlineData(@"plain", "plain")]
        [InlineData(@"D:\\SteamLibrary", @"D:\SteamLibrary")]
        [InlineData(@"say \""hi\""", "say \"hi\"")]
        [InlineData(@"a\tb", "a\tb")]
        public void UnescapeVdf_DecodesEscapes(string raw, string expected)
        {
            Assert.Equal(expected, SteamPaths.UnescapeVdf(raw));
        }
    }
}
