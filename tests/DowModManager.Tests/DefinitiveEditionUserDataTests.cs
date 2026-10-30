using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Platform;
using DoW_Mod_Manager.Platform.Linux;
using DoW_Mod_Manager.Services;
using Xunit;
using K = DoW_Mod_Manager.Services.GameSettingsKeys;

namespace DowModManager.Tests
{
    /// <summary>
    /// Definitive Edition per-user data (<c>%APPDATA%\Relic Entertainment\Dawn of War</c>) and its
    /// <c>Mods</c> folder, on fake trees.
    /// </summary>
    public class DefinitiveEditionUserDataTests : IDisposable
    {
        private readonly string _tmp;
        private readonly string _install;
        private readonly string _userData;
        private readonly string _modsDir;
        private readonly GameInstall _de;

        public DefinitiveEditionUserDataTests()
        {
            _tmp = Path.Combine(Path.GetTempPath(), "dmm_deuser_" + Guid.NewGuid().ToString("N"));
            _install = Path.Combine(_tmp, "Dawn of War Definitive Edition");
            _userData = Path.Combine(_tmp, "AppData", "Roaming", "Relic Entertainment", "Dawn of War");
            _modsDir = Path.Combine(_userData, "Mods");
            Directory.CreateDirectory(_install);
            Directory.CreateDirectory(_modsDir);

            File.WriteAllText(Path.Combine(_install, "W40k.exe"), "");
            foreach (var name in BaseModules.Names)
            {
                WriteModule(_install, name, $"ModFolder = {name}\nPlayable = 0\n");
                Directory.CreateDirectory(Path.Combine(_install, name));
            }

            _de = new GameInstall
            {
                InstallDir = _install,
                ExecutableName = "W40k.exe",
                Variant = GameVariant.DefinitiveEdition,
                UserDataDir = _userData,
                ModsDir = _modsDir,
            };
        }

        public void Dispose()
        {
            if (Directory.Exists(_tmp))
                Directory.Delete(_tmp, recursive: true);
        }

        private static string WriteModule(string dir, string name, string content)
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name + ".module");
            File.WriteAllText(path, "[global]\n" + content);
            return path;
        }

        private static ModService Mods() => new(new ModuleParser());

        // ------------------------------------------------------------------ UserDataDir resolution

        [Fact]
        public void LinuxLocator_DefinitiveEdition_UserDataInProtonPrefix()
        {
            var steamApps = Path.Combine(_tmp, "lib", "steamapps");
            var game = Path.Combine(steamApps, "common", "Dawn of War Definitive Edition");
            Directory.CreateDirectory(game);
            File.WriteAllText(Path.Combine(game, "W40k.exe"), "");
            File.WriteAllText(Path.Combine(game, "DoWDE.module"), "");
            // The game created the folders with its own casing; lookups must still find them.
            var roaming = Path.Combine(steamApps, "compatdata", "3556750", "pfx", "drive_c", "users", "steamuser", "AppData", "Roaming");
            var expected = Path.Combine(roaming, "Relic Entertainment", "Dawn of War");
            Directory.CreateDirectory(Path.Combine(expected, "Mods"));

            var install = new LinuxGameInstallLocator(Array.Empty<string>()).IdentifyInstall(game);

            Assert.Equal(GameVariant.DefinitiveEdition, install!.Variant);
            Assert.Equal(expected, install.UserDataDir);
            Assert.Equal(Path.Combine(expected, "Mods"), install.ModsDir);
        }

        [Fact]
        public void LinuxLocator_DefinitiveEdition_ResolvesCaseInsensitively()
        {
            if (!OperatingSystem.IsLinux())
                return; // Case-insensitive file systems need no resolution.

            var steamApps = Path.Combine(_tmp, "lib", "steamapps");
            var game = Path.Combine(steamApps, "common", "Dawn of War Definitive Edition");
            Directory.CreateDirectory(game);
            File.WriteAllText(Path.Combine(game, "W40k.exe"), "");
            File.WriteAllText(Path.Combine(game, "DoWDE.module"), "");
            var actual = Path.Combine(steamApps, "compatdata", "3556750", "pfx", "drive_c", "users", "steamuser",
                "AppData", "Roaming", "relic entertainment", "dawn of war");
            Directory.CreateDirectory(actual);

            var install = new LinuxGameInstallLocator(Array.Empty<string>()).IdentifyInstall(game);

            Assert.Equal(actual, install!.UserDataDir);
        }

        [Fact]
        public void LinuxLocator_ClassicGame_UserDataIsInstallDir()
        {
            var game = Path.Combine(_tmp, "lib", "steamapps", "common", "Dawn of War Soulstorm");
            Directory.CreateDirectory(game);
            File.WriteAllText(Path.Combine(game, "Soulstorm.exe"), "");

            var install = new LinuxGameInstallLocator(Array.Empty<string>()).IdentifyInstall(game);

            Assert.Equal(game, install!.UserDataDir);
            Assert.Null(install.ModsDir);
        }

        [Fact]
        public void UserDataFor_WindowsAppData()
        {
            var appData = Path.Combine(_tmp, "Users", "me", "AppData", "Roaming");

            var (userData, mods) = GameVariantDetector.UserDataFor(GameVariant.DefinitiveEdition, _install, appData);

            Assert.Equal(Path.Combine(appData, "Relic Entertainment", "Dawn of War"), userData);
            Assert.Equal(Path.Combine(appData, "Relic Entertainment", "Dawn of War", "Mods"), mods);
            Assert.Equal((_install, (string?)null), GameVariantDetector.UserDataFor(GameVariant.Soulstorm, _install, appData));
        }

        [Fact]
        public void GameInstall_UserDataDirDefaultsToInstallDir()
        {
            var install = new GameInstall { InstallDir = _install, ExecutableName = "Soulstorm.exe", Variant = GameVariant.Soulstorm };

            Assert.Equal(_install, install.UserDataDir);
        }

        // ------------------------------------------------------------------ settings & warnings

        [Fact]
        public void Settings_ReadAndWriteUseUserDataDir()
        {
            File.WriteAllText(Path.Combine(_userData, "Local.ini"), "[global]\nscreenwidth=1920\nscreenheight=1080\n");
            File.WriteAllText(Path.Combine(_install, "Local.ini"), "[global]\nscreenwidth=640\n"); // must be ignored
            Directory.CreateDirectory(Path.Combine(_install, "Drivers"));
            File.WriteAllText(Path.Combine(_install, "Drivers", "spdx9_config.txt"), "allowhwcursor 0\n");
            var profile = Path.Combine(_userData, "Profiles", "Profile1");
            Directory.CreateDirectory(profile);
            File.WriteAllText(Path.Combine(profile, "playercfg.lua"), "Sound = \n{\n\tVolumeMusic = 0.25,\n}\n");
            var service = new GameSettingsService();

            var settings = service.Read(_de);

            Assert.Equal("1920", settings[K.ScreenWidth]);
            Assert.Equal("0", settings[K.AllowHwCursor]);
            Assert.Equal("0.25", settings[K.VolumeMusic]);
            Assert.Equal("DoWDE", settings[K.CurrentMod]);
            Assert.Equal(["Profile1"], service.GetProfiles(_de.UserDataDir).Select(p => p.ProfileName));

            settings[K.ScreenWidth] = "2560";
            service.Write(_de, settings);

            Assert.Contains("screenwidth=2560", File.ReadAllText(Path.Combine(_userData, "Local.ini")));
            Assert.Contains("screenwidth=640", File.ReadAllText(Path.Combine(_install, "Local.ini")));
            Assert.Contains("VolumeMusic = 0.25", File.ReadAllText(Path.Combine(profile, "playercfg.lua")));
        }

        [Fact]
        public void Settings_DefaultsUseBaseGameModule()
        {
            var service = new GameSettingsService();
            var soulstorm = new GameInstall { InstallDir = _install, ExecutableName = "Soulstorm.exe", Variant = GameVariant.Soulstorm };

            Assert.Equal("DoWDE", service.GetDefaults(_de)[K.CurrentMod]);
            Assert.Equal("W40k", service.GetDefaults(soulstorm)[K.CurrentMod]);
        }

        [Fact]
        public void Warnings_ReadFromUserDataDir()
        {
            File.WriteAllText(Path.Combine(_userData, "warnings.log"), "ok\nMOD -- Fatal Data Error: boom\n");

            var errors = new WarningsLogService().GetFatalDataErrors(_de.UserDataDir);

            Assert.Equal(["MOD -- Fatal Data Error: boom"], errors);
            Assert.Empty(new WarningsLogService().GetFatalDataErrors(_de.InstallDir));
        }

        // ------------------------------------------------------------------ mods in both locations

        [Fact]
        public void GetPlayableMods_FindsInstallDirFlatAndNestedMods()
        {
            WriteModule(_install, "RootMod", "ModFolder = RootMod\nPlayable = 1\n");
            WriteModule(_modsDir, "FlatMod", "ModFolder = FlatMod\nPlayable = 1\n");
            WriteModule(Path.Combine(_modsDir, "Nested"), "Nested", "ModFolder = Nested\nPlayable = 1\n");
            WriteModule(Path.Combine(_modsDir, "Nested"), "Hidden", "ModFolder = Hidden\nPlayable = 0\n");
            // A duplicate of an install-dir mod is listed once, from the install dir.
            WriteModule(_modsDir, "RootMod", "ModFolder = Other\nPlayable = 1\n");
            // A stray base module in Mods never shows up.
            WriteModule(_modsDir, "DoWDE", "ModFolder = DoWDE\nPlayable = 1\n");

            var mods = Mods().GetPlayableMods(_de);

            Assert.Equal(["RootMod", "FlatMod", "Nested"], mods.Select(m => m.Name));
            Assert.Equal(Path.Combine(_install, "RootMod.module"), mods[0].ModuleFilePath);
            Assert.Equal(Path.Combine(_modsDir, "Nested", "Nested.module"), mods[2].ModuleFilePath);
        }

        [Fact]
        public void GetPlayableMods_ClassicInstallIgnoresModsDir()
        {
            WriteModule(_modsDir, "FlatMod", "ModFolder = FlatMod\nPlayable = 1\n");

            Assert.Empty(Mods().GetPlayableMods(_install, isOldGame: false));
        }

        [Fact]
        public void ResolveDependencies_AcrossInstallDirAndMods()
        {
            // Nested mod shipping its own dependency, using a flat mod and an install-dir mod.
            var nestedDir = Path.Combine(_modsDir, "Nested");
            WriteModule(nestedDir, "Nested",
                "ModFolder = NestedData\nPlayable = 1\n" +
                "RequiredMod.1 = dowde\nRequiredMod.2 = Bundled\nRequiredMod.3 = FlatMod\nRequiredMod.4 = RootMod\nRequiredMod.5 = Ghost\n");
            WriteModule(nestedDir, "Bundled", "ModFolder = BundledData\n");
            Directory.CreateDirectory(Path.Combine(nestedDir, "BundledData"));
            WriteModule(_modsDir, "FlatMod", "ModFolder = FlatData\n");
            Directory.CreateDirectory(Path.Combine(_modsDir, "FlatData"));
            WriteModule(_install, "RootMod", "ModFolder = RootData\n");
            Directory.CreateDirectory(Path.Combine(_install, "RootData"));
            // A base-module copy in Mods must not shadow the real one in the install dir.
            WriteModule(_modsDir, "DoWDE", "ModFolder = NotThere\n");

            var mod = Mods().GetPlayableMods(_de).Single(m => m.Name == "Nested");
            var deps = Mods().ResolveDependencies(_de, mod);

            Assert.Equal(
                [("dowde", "DoWDE", true), ("Bundled", "BundledData", true), ("FlatMod", "FlatData", true),
                 ("RootMod", "RootData", true), ("Ghost", ModDependencyStatus.Missing, false)],
                deps.Select(d => (d.RequiredModName, d.ModFolder, d.IsInstalled)));
        }

        [Fact]
        public void ResolveDependencies_ModFolderIsRelativeToTheModule()
        {
            // FlatData exists only in the install dir: FlatMod (in Mods) must not see it.
            WriteModule(_modsDir, "FlatMod", "ModFolder = FlatData\n");
            Directory.CreateDirectory(Path.Combine(_install, "FlatData"));
            WriteModule(_install, "RootMod", "ModFolder = RootData\nPlayable = 1\nRequiredMod.1 = FlatMod\n");

            var mod = Mods().GetPlayableMods(_de).Single(m => m.Name == "RootMod");

            Assert.False(Assert.Single(Mods().ResolveDependencies(_de, mod)).IsInstalled);
            Assert.Equal(_install, ModService.ModRoot(_de, mod));
            Assert.Equal(_modsDir, ModService.ModRoot(_de, new ModInfo { ModuleFilePath = Path.Combine(_modsDir, "FlatMod.module") }));
            Assert.Equal(_install, ModService.ModRoot(_de, new ModInfo()));
        }

        // ------------------------------------------------------------------ merger

        [Fact]
        public void Merger_WritesMergedCopyNextToModsModule()
        {
            var nestedDir = Path.Combine(_modsDir, "Nested");
            WriteModule(nestedDir, "Nested", "ModFolder = Nested\nPlayable = 1\nRequiredMod.1 = DoWDE\n");
            WriteModule(_modsDir, "FlatMod", "ModFolder = FlatMod\nPlayable = 1\n");
            WriteModule(_modsDir, "FlatMod_MERGED", "ModFolder = FlatMod\nPlayable = 1\n");
            var merger = new ModMergerService(Mods());

            var modules = merger.GetMergeableModules(_de);
            Assert.Equal(["FlatMod", "Nested"], modules.Select(m => m.Name));
            Assert.Equal(Path.Combine(_modsDir, "FlatMod_MERGED.module"), modules[0].MergedFilePath);
            Assert.Equal(["FlatMod", "FlatMod_MERGED", "Nested"], merger.GetAllModuleNames(_de));

            var nested = modules[1];
            var def = merger.Load(nested.EffectiveFilePath, merger.GetAllModuleNames(_de));
            var written = merger.Save(nested, new MergeDefinition
            {
                BaseModules = def.BaseModules,
                Entries = [new MergeEntry("FlatMod", MergeModState.Active)],
            });

            Assert.Equal(Path.Combine(nestedDir, "Nested_MERGED.module"), written);
            Assert.False(File.Exists(Path.Combine(_install, "Nested_MERGED.module")));
            Assert.Contains("RequiredMod.2 = FlatMod", File.ReadAllText(written));
        }

        // ------------------------------------------------------------------ Soulstorm folder

        [Fact]
        public void SoulstormModsDir_ListsItsModsLastAndResolvesTheirDependencies()
        {
            var soulstorm = Path.Combine(_tmp, "Dawn of War Soulstorm");
            WriteModule(_install, "RootMod", "ModFolder = RootMod\nPlayable = 1\n");
            WriteModule(soulstorm, "SsMod", "ModFolder = SsData\nPlayable = 1\nRequiredMod.1 = SsBase\nRequiredMod.2 = DoWDE\n");
            WriteModule(soulstorm, "SsBase", "ModFolder = SsBaseData\n");
            Directory.CreateDirectory(Path.Combine(soulstorm, "SsBaseData"));
            // Soulstorm's own base modules are never listed, nor used instead of the DE ones.
            WriteModule(soulstorm, "DXP3", "ModFolder = DXP3\nPlayable = 1\n");
            WriteModule(soulstorm, "DoWDE", "ModFolder = NotThere\n");

            var de = _de.WithSoulstormModsDir(soulstorm);
            var mods = Mods().GetPlayableMods(de);

            Assert.Equal(["RootMod", "SsMod"], mods.Select(m => m.Name));
            Assert.DoesNotContain(Mods().GetPlayableMods(_de), m => m.Name == "SsMod");

            var deps = Mods().ResolveDependencies(de, mods.Single(m => m.Name == "SsMod"));
            Assert.All(deps, d => Assert.True(d.IsInstalled, d.RequiredModName));
            Assert.Contains("SsBase", new ModMergerService(Mods()).GetAllModuleNames(de));
        }

        [Fact]
        public void WithSoulstormModsDir_KeepsEverythingElse()
        {
            var de = _de.WithSoulstormModsDir("/games/Soulstorm");

            Assert.Equal("/games/Soulstorm", de.SoulstormModsDir);
            Assert.Equal((_de.InstallDir, _de.UserDataDir, _de.ModsDir, _de.ExecutableName), (de.InstallDir, de.UserDataDir, de.ModsDir, de.ExecutableName));
            Assert.Null(de.WithSoulstormModsDir(" ").SoulstormModsDir);
        }
    }
}
