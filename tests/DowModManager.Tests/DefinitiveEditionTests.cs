using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Platform;
using DoW_Mod_Manager.Services;
using Xunit;

namespace DowModManager.Tests
{
    /// <summary>Dawn of War: Definitive Edition handling outside the Steam locator.</summary>
    public class DefinitiveEditionTests : IDisposable
    {
        private readonly string _dir;

        public DefinitiveEditionTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dmm_de_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        private void WriteFile(string name, string content = "") =>
            File.WriteAllText(Path.Combine(_dir, name), content);

        /// <summary>The DE base modules, as shipped (Playable = 0), plus one real mod.</summary>
        private void CreateDefinitiveEditionFolder()
        {
            WriteFile("W40k.exe");
            foreach (var name in new[] { "W40k", "WXP", "DXP2", "DXP3", "DoWDE" })
                WriteFile(name + ".module", $"[global]\nModFolder = {name}\nPlayable = 0\nRuntimeMode = 3\n");
            WriteFile("MyMod.module", "[global]\nModFolder = MyMod\nPlayable = 1\nRequiredMod.1 = DoWDE\n");
        }

        [Fact]
        public void Detector_PrefersDefinitiveEditionOverOriginal()
        {
            CreateDefinitiveEditionFolder();

            Assert.Equal(GameVariant.DefinitiveEdition, GameVariantDetector.Detect(_dir));
            Assert.Equal("W40k.exe", GameVariantDetector.ExecutableFor(GameVariant.DefinitiveEdition));
        }

        [Fact]
        public void Detector_ManifestAppIdIdentifiesDefinitiveEdition()
        {
            WriteFile("W40k.exe");

            Assert.Equal(GameVariant.Original, GameVariantDetector.Detect(_dir));
            Assert.Equal(GameVariant.DefinitiveEdition, GameVariantDetector.Detect(_dir, steamAppId: 3556750));
        }

        [Fact]
        public void GetPlayableMods_SkipsDefinitiveEditionBaseModules()
        {
            CreateDefinitiveEditionFolder();
            // Even if a base module claimed to be playable it must not be listed.
            WriteFile("DoWDE.module", "[global]\nModFolder = DoWDE\nPlayable = 1\n");
            var install = new GameInstall { InstallDir = _dir, ExecutableName = "W40k.exe", Variant = GameVariant.DefinitiveEdition };

            var mods = new ModService(new ModuleParser()).GetPlayableMods(_dir, install.IsOldGame);

            Assert.Equal(["MyMod"], mods.Select(m => m.Name));
        }

        [Fact]
        public void Merger_TreatsDefinitiveEditionModulesAsBase()
        {
            CreateDefinitiveEditionFolder();
            var merger = new ModMergerService(new ModService(new ModuleParser()));

            Assert.Equal(["MyMod"], merger.GetAllModuleNames(_dir));
            var def = merger.Load(Path.Combine(_dir, "MyMod.module"), ["MyMod"]);
            Assert.Equal(["DoWDE"], def.BaseModules);
            Assert.Empty(def.Entries);
        }

        [Theory]
        [InlineData("DoWDE", true)]
        [InlineData("dxp3.module", true)]
        [InlineData(" W40k ", true)]
        [InlineData("Unification", false)]
        [InlineData("", false)]
        public void BaseModules_Contains(string name, bool expected)
        {
            Assert.Equal(expected, BaseModules.Contains(name));
        }

        [Fact]
        public void Modlist_DefinitiveEditionUsesSoulstormSection()
        {
            using var http = new HttpClient();
            var service = new ModlistService(new DownloadService(http), _dir);
            const string list = "[SS]\nDoWpro\nDoWpro\nhttps://x\nhttps://dl/x\n-\n[DC]\nOther\nOther\nhttps://y\nhttps://dl/y\n-\n[END]\n";

            var mods = service.Parse(list, GameVariant.DefinitiveEdition);

            Assert.Equal(service.Parse(list, GameVariant.Soulstorm), mods);
            Assert.Equal("DoWpro", Assert.Single(mods).Name);
        }

        [Fact]
        public void Detector_GogDefinitiveEdition_UsesW40kGogExe()
        {
            CreateDefinitiveEditionFolder();
            File.Delete(Path.Combine(_dir, "W40k.exe"));
            WriteFile("W40k_gog.exe");

            Assert.Equal(GameVariant.DefinitiveEdition, GameVariantDetector.Detect(_dir));
            Assert.Equal("W40k_gog.exe", GameVariantDetector.ExecutableFor(GameVariant.DefinitiveEdition, _dir));
            Assert.True(GameVariantDetector.IsGogDefinitiveEdition(GameVariant.DefinitiveEdition, _dir));
        }

        [Fact]
        public void Detector_SteamExeWinsOverGogExe()
        {
            CreateDefinitiveEditionFolder();
            WriteFile("W40k_gog.exe");

            Assert.Equal("W40k.exe", GameVariantDetector.ExecutableFor(GameVariant.DefinitiveEdition, _dir));
            Assert.False(GameVariantDetector.IsGogDefinitiveEdition(GameVariant.DefinitiveEdition, _dir));
        }

        [Fact]
        public void Detector_GogExeWithoutDefinitiveEditionFiles_IsNoGame()
        {
            WriteFile("W40k_gog.exe");

            Assert.Null(GameVariantDetector.Detect(_dir));
        }

        [Fact]
        public void LinuxLocator_ManualGogDefinitiveEdition_IsGog()
        {
            CreateDefinitiveEditionFolder();
            File.Delete(Path.Combine(_dir, "W40k.exe"));
            WriteFile("W40k_gog.exe");

            var install = new DoW_Mod_Manager.Platform.Linux.LinuxGameInstallLocator().IdentifyInstall(_dir);

            Assert.NotNull(install);
            Assert.Equal("W40k_gog.exe", install!.ExecutableName);
            Assert.Equal(GameDistribution.GoG, install.Distribution);
        }
    }
}
