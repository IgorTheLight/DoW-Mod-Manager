using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Services;
using Xunit;

namespace DowModManager.Tests
{
    public class ModMergerServiceTests : IDisposable
    {
        private readonly string _dir;
        private readonly ModMergerService _service;

        public ModMergerServiceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dmm_merge_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _service = new ModMergerService(new ModService(new ModuleParser()));
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        private string WriteModule(string name, string content)
        {
            string path = Path.Combine(_dir, name + ".module");
            File.WriteAllText(path, content);
            return path;
        }

        private const string ModuleWithDeps =
            "[global]\n" +
            "UIName = My Mod\n" +
            "Playable = 1\n" +
            "ModFolder = MyMod\n" +
            "RequiredMod.1 = W40k\n" +
            "RequiredMod.2 = DXP2\n" +
            "RequiredMod.3 = ModA\n" +
            "//RequiredMod.4 = ModB\n" +
            "RequiredMod.5 = Ghost\n" +
            "DataFolder.1 = Data\n";

        [Fact]
        public void Load_ClassifiesEntriesAndKeepsBaseModulesApart()
        {
            string path = WriteModule("MyMod", ModuleWithDeps);

            MergeDefinition def = _service.Load(path, ["MyMod", "ModA", "ModB"]);

            Assert.Equal(["W40k", "DXP2"], def.BaseModules);
            Assert.Equal(
                [new MergeEntry("ModA", MergeModState.Active),
                 new MergeEntry("ModB", MergeModState.Inactive),
                 new MergeEntry("Ghost", MergeModState.Missing)],
                def.Entries);
        }

        [Fact]
        public void Save_WritesMergedCopyWithSequentialIndicesAndKeepsOtherLines()
        {
            string path = WriteModule("MyMod", ModuleWithDeps);
            var module = new MergeableModule { Name = "MyMod", FilePath = path };
            var def = new MergeDefinition
            {
                BaseModules = ["W40k", "DXP2"],
                Entries = [new MergeEntry("ModB", MergeModState.Active), new MergeEntry("ModA", MergeModState.Inactive)],
            };

            string written = _service.Save(module, def);

            Assert.Equal(Path.Combine(_dir, "MyMod_MERGED.module"), written);
            Assert.Equal(ModuleWithDeps, File.ReadAllText(path)); // original untouched
            Assert.Equal(
                ["[global]", "UIName = My Mod", "Playable = 1", "ModFolder = MyMod",
                 "RequiredMod.1 = W40k", "RequiredMod.2 = DXP2",
                 "RequiredMod.3 = ModB", "//RequiredMod.4 = ModA",
                 "DataFolder.1 = Data"],
                File.ReadAllLines(written));
        }

        [Fact]
        public void GetMergeableModules_PairsOriginalWithMergedCopy()
        {
            WriteModule("MyMod", ModuleWithDeps);
            WriteModule("MyMod_MERGED", ModuleWithDeps);
            WriteModule("Other", "Playable = 1\n");

            var modules = _service.GetMergeableModules(_dir, isOldGame: false);

            Assert.Equal(["MyMod", "Other"], modules.Select(m => m.Name));
            Assert.Equal(Path.Combine(_dir, "MyMod_MERGED.module"), modules[0].MergedFilePath);
            Assert.Null(modules[1].MergedFilePath);
        }

        [Fact]
        public void DeleteMerge_RemovesOnlyTheMergedCopy()
        {
            string original = WriteModule("MyMod", ModuleWithDeps);
            string merged = WriteModule("MyMod_MERGED", ModuleWithDeps);

            _service.DeleteMerge(new MergeableModule { Name = "MyMod", FilePath = original, MergedFilePath = merged });

            Assert.True(File.Exists(original));
            Assert.False(File.Exists(merged));
        }

        [Fact]
        public void GetAllModuleNames_ExcludesBaseModules()
        {
            WriteModule("W40k", "");
            WriteModule("WXP", "");
            WriteModule("DXP2", "");
            WriteModule("ModA", "");

            Assert.Equal(["ModA"], _service.GetAllModuleNames(_dir));
        }

        [Fact]
        public void SortInactiveToBottom_IsStable()
        {
            var sorted = ModMergerService.SortInactiveToBottom(
            [
                new MergeEntry("A", MergeModState.Inactive),
                new MergeEntry("B", MergeModState.Active),
                new MergeEntry("C", MergeModState.Missing),
                new MergeEntry("D", MergeModState.Active),
            ]);

            Assert.Equal(["B", "D", "A", "C"], sorted.Select(e => e.Name));
        }

        [Fact]
        public void Save_WritesCrlfLineEndings()
        {
            string path = WriteModule("MyMod", ModuleWithDeps); // LF-only source
            var module = new MergeableModule { Name = "MyMod", FilePath = path };
            var def = new MergeDefinition { BaseModules = ["W40k"], Entries = [new MergeEntry("ModA", MergeModState.Active)] };

            CrlfAssert.File(_service.Save(module, def));
        }
    }
}
