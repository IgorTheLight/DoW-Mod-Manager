using DoW_Mod_Manager.Services;
using DoW_Mod_Manager.Models;
using Xunit;

namespace DowModManager.Tests
{
    public class ModServiceTests : IDisposable
    {
        private readonly string _dir;
        private readonly IModService _service;

        public ModServiceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dmm_mods_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _service = new ModService(new ModuleParser());
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        private void WriteModule(string name, string content) =>
            File.WriteAllText(Path.Combine(_dir, name + ".module"), content);

        [Fact]
        public void GetPlayableMods_SkipsBaseModules()
        {
            WriteModule("W40k", "Playable = 1\nModFolder = W40k\n");
            WriteModule("WXP", "Playable = 1\nModFolder = WXP\n");
            WriteModule("DXP2", "Playable = 1\nModFolder = DXP2\n");
            WriteModule("MyMod", "Playable = 1\nModFolder = MyMod\n");

            IReadOnlyList<ModInfo> mods = _service.GetPlayableMods(_dir, isOldGame: false);

            Assert.Single(mods);
            Assert.Equal("MyMod", mods[0].Name);
        }

        [Fact]
        public void GetPlayableMods_FiltersOutUnplayable()
        {
            WriteModule("Playable", "Playable = 1\nModFolder = P\n");
            WriteModule("Unplayable", "ModFolder = U\n"); // no Playable flag

            IReadOnlyList<ModInfo> mods = _service.GetPlayableMods(_dir, isOldGame: false);

            Assert.Single(mods);
            Assert.Equal("Playable", mods[0].Name);
        }

        [Fact]
        public void GetPlayableMods_OldGame_IncludesEvenWithoutPlayableFlag()
        {
            WriteModule("OldMod", "ModFolder = O\n");

            IReadOnlyList<ModInfo> mods = _service.GetPlayableMods(_dir, isOldGame: true);

            Assert.Single(mods);
        }

        [Fact]
        public void GetPlayableMods_ReturnsEmpty_WhenDirectoryMissing()
        {
            IReadOnlyList<ModInfo> mods =
                _service.GetPlayableMods(Path.Combine(_dir, "does_not_exist"), isOldGame: false);

            Assert.Empty(mods);
        }

        [Fact]
        public void ResolveDependencies_InstalledWhenModuleAndFolderExist()
        {
            // The dependency module file and its declared asset folder both exist.
            WriteModule("DepMod", "Playable = 1\nModFolder = DepFolder\n");
            Directory.CreateDirectory(Path.Combine(_dir, "DepFolder"));

            var mod = new ModInfo { RequiredMods = new[] { "DepMod.module" } };

            IReadOnlyList<ModDependencyStatus> deps = _service.ResolveDependencies(_dir, mod);

            Assert.Single(deps);
            Assert.Equal("DepFolder", deps[0].ModFolder);
            Assert.True(deps[0].IsInstalled);
        }

        [Fact]
        public void ResolveDependencies_NotInstalledWhenFolderMissing()
        {
            // Module file exists, but the asset folder it declares does not.
            WriteModule("DepMod", "Playable = 1\nModFolder = DepFolder\n");

            var mod = new ModInfo { RequiredMods = new[] { "DepMod" } };

            IReadOnlyList<ModDependencyStatus> deps = _service.ResolveDependencies(_dir, mod);

            Assert.False(deps[0].IsInstalled);
            Assert.Equal("DepFolder", deps[0].ModFolder);
        }

        [Fact]
        public void ResolveDependencies_MissingWhenModuleFileAbsent()
        {
            var mod = new ModInfo { RequiredMods = new[] { "NoSuchMod.module" } };

            IReadOnlyList<ModDependencyStatus> deps = _service.ResolveDependencies(_dir, mod);

            Assert.Single(deps);
            Assert.Equal(ModDependencyStatus.Missing, deps[0].ModFolder);
            Assert.False(deps[0].IsInstalled);
        }

        [Fact]
        public void ResolveDependencies_MatchesModuleAndFolderNamesCaseInsensitively()
        {
            // Windows-authored mods: names in .module files rarely match the on-disk casing.
            WriteModule("DXP2", "ModFolder = dxp2data\n");
            Directory.CreateDirectory(Path.Combine(_dir, "DXP2Data"));
            WriteModule("Unification", "Playable = 1\nModFolder = unification\n");
            Directory.CreateDirectory(Path.Combine(_dir, "Unification"));

            var mod = new ModInfo { RequiredMods = new[] { "dxp2", "UNIFICATION.module" } };

            IReadOnlyList<ModDependencyStatus> deps = _service.ResolveDependencies(_dir, mod);

            Assert.Equal(["dxp2data", "unification"], deps.Select(d => d.ModFolder));
            Assert.All(deps, d => Assert.True(d.IsInstalled));
        }

        [Fact]
        public void ResolveDependencies_AcceptsBackslashesInModFolder()
        {
            WriteModule("Nested", "ModFolder = Mods\\Nested\n");
            Directory.CreateDirectory(Path.Combine(_dir, "mods", "nested"));

            var mod = new ModInfo { RequiredMods = new[] { "Nested" } };

            Assert.True(_service.ResolveDependencies(_dir, mod)[0].IsInstalled);
        }
    }
}
