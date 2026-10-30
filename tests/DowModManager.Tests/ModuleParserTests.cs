using DoW_Mod_Manager.Services;
using DoW_Mod_Manager.Models;
using Xunit;

namespace DowModManager.Tests
{
    public class ModuleParserTests
    {
        private readonly IModuleParser _parser = new ModuleParser();

        private const string PlayableModule =
            "Playable = 1\n" +
            "ModFolder = MyMod\n" +
            "ModVersion = 2.5\n" +
            "RequiredMod.1 = W40k.module\n" +
            "RequiredMod.2 = DXP2.module\n";

        [Fact]
        public void ParseContent_ExtractsModFolderVersionAndPlayable()
        {
            ModInfo info = _parser.ParseContent(PlayableModule, isOldGame: false, name: "MyMod");

            Assert.Equal("MyMod", info.ModFolder);
            Assert.Equal("2.5", info.ModVersion);
            Assert.True(info.IsPlayable);
            Assert.Equal("MyMod", info.Name);
        }

        [Fact]
        public void ParseContent_ExtractsRequiredMods()
        {
            ModInfo info = _parser.ParseContent(PlayableModule, isOldGame: false);

            Assert.Equal(new[] { "W40k.module", "DXP2.module" }, info.RequiredMods);
        }

        [Fact]
        public void ParseContent_NotPlayable_WhenNoPlayableFlagAndNotOldGame()
        {
            string content = "ModFolder = X\nModVersion = 1.0\n";

            ModInfo info = _parser.ParseContent(content, isOldGame: false);

            Assert.False(info.IsPlayable);
        }

        [Fact]
        public void ParseContent_OldGame_IsAlwaysPlayable()
        {
            string content = "ModFolder = X\n"; // no Playable flag

            ModInfo info = _parser.ParseContent(content, isOldGame: true);

            Assert.True(info.IsPlayable);
        }

        [Fact]
        public void ParseContent_IgnoresCommentedRequiredMods()
        {
            string content =
                "Playable = 1\n" +
                "// RequiredMod.1 = Commented.module\n" +
                ";; RequiredMod.2 = AlsoCommented.module\n" +
                "RequiredMod.3 = Real.module\n";

            ModInfo info = _parser.ParseContent(content, isOldGame: false);

            Assert.Equal(new[] { "Real.module" }, info.RequiredMods);
        }

        [Fact]
        public void Parse_ReadsFromFile()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dmm_parser_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "TestMod.module");
                File.WriteAllText(path, PlayableModule);

                ModInfo info = _parser.Parse(path, isOldGame: false);

                Assert.Equal("TestMod", info.Name);
                Assert.Equal("MyMod", info.ModFolder);
                Assert.Equal(path, info.ModuleFilePath);
                Assert.True(info.IsPlayable);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
