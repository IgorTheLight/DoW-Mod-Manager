using DoW_Mod_Manager.Platform.Linux;
using Xunit;

namespace DowModManager.Tests
{
    /// <summary>Proton build selection against a fake Steam root (config.vdf, tool dirs, compatdata).</summary>
    public class ProtonToolResolverTests : IDisposable
    {
        private readonly string _steam = Path.Combine(Path.GetTempPath(), "dmm_proton_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_steam))
                Directory.Delete(_steam, recursive: true);
        }

        private string ValveTool(string folder, string prefixVersion)
        {
            var dir = Path.Combine(_steam, "steamapps", "common", folder);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "proton"),
                $"#!/usr/bin/env python3\n\nCURRENT_PREFIX_VERSION=\"{prefixVersion}\"\n");
            File.WriteAllText(Path.Combine(dir, "toolmanifest.vdf"), "\"manifest\"\n{\n  \"commandline\" \"/proton %verb%\"\n}\n");
            return Path.Combine(dir, "proton");
        }

        private string CustomTool(string folder, string internalName, string prefixVersion)
        {
            var dir = Path.Combine(_steam, "compatibilitytools.d", folder);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "proton"), $"CURRENT_PREFIX_VERSION=\"{prefixVersion}\"\n");
            File.WriteAllText(Path.Combine(dir, "compatibilitytool.vdf"), $$"""
                "compatibilitytools"
                {
                  "compat_tools"
                  {
                    "{{internalName}}" // Internal name of this tool
                    {
                      "install_path" "."
                      "display_name" "{{internalName}}"
                    }
                  }
                }
                """);
            return Path.Combine(dir, "proton");
        }

        private void Mapping(params (string AppId, string Tool)[] entries)
        {
            var dir = Path.Combine(_steam, "config");
            Directory.CreateDirectory(dir);
            var body = string.Concat(entries.Select(e =>
                $"\t\t\t\t\t\"{e.AppId}\"\n\t\t\t\t\t{{\n\t\t\t\t\t\t\"name\"\t\t\"{e.Tool}\"\n\t\t\t\t\t\t\"config\"\t\t\"\"\n\t\t\t\t\t\t\"priority\"\t\t\"250\"\n\t\t\t\t\t}}\n"));
            File.WriteAllText(Path.Combine(dir, "config.vdf"),
                "\"InstallConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"Valve\"\n\t\t{\n\t\t\t\"Steam\"\n\t\t\t{\n"
                + "\t\t\t\t\"CompatToolMapping\"\n\t\t\t\t{\n" + body + "\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n");
        }

        private string Prefix(int appId, string? version)
        {
            var dir = Path.Combine(_steam, "steamapps", "compatdata", appId.ToString());
            Directory.CreateDirectory(dir);
            if (version is not null)
                File.WriteAllText(Path.Combine(dir, "version"), version + "\n");
            return dir;
        }

        private ProtonSelection Select(int appId = 9450)
        {
            var sel = ProtonToolResolver.Select(appId, Path.Combine(_steam, "steamapps", "compatdata", appId.ToString()), [_steam]);
            Assert.NotNull(sel);
            return sel!;
        }

        [Theory]
        [InlineData("Proton 9.0", "proton_9")]
        [InlineData("Proton 9.0 (Beta)", "proton_9")]
        [InlineData("Proton 11.0", "proton_11")]
        [InlineData("Proton 5.13", "proton_513")]
        [InlineData("Proton 6.3", "proton_63")]
        [InlineData("Proton - Experimental", "proton_experimental")]
        [InlineData("Proton Hotfix", "proton_hotfix")]
        [InlineData("Proton EasyAntiCheat Runtime", null)]
        public void ValveToolName_FromFolder(string folder, string? expected)
        {
            Assert.Equal(expected, ProtonToolResolver.ValveToolName(folder));
        }

        [Fact]
        public void PerGameMapping_ToCustomTool_Wins()
        {
            ValveTool("Proton 11.0", "11.0-100");
            var ge = CustomTool("GE-Proton9-20", "GE-Proton9-20", "GE-Proton9-20");
            Mapping(("0", "proton_11"), ("9450", "GE-Proton9-20"));

            var sel = Select();

            Assert.Equal(Path.GetFullPath(ge), sel.ScriptPath);
            Assert.Contains("Steam compat tool for app 9450", sel.Reason);
        }

        [Fact]
        public void PerGameMapping_ToValveTool_UsesFolderHeuristic()
        {
            var p9 = ValveTool("Proton 9.0 (Beta)", "9.0-203");
            ValveTool("Proton 11.0", "11.0-100");
            Mapping(("9450", "proton_9"));

            Assert.Equal(Path.GetFullPath(p9), Select().ScriptPath);
        }

        [Fact]
        public void CustomToolName_ComesFromManifestNotFolder()
        {
            var tool = CustomTool("some-folder", "MyProton", "MyProton-1");
            ValveTool("Proton 11.0", "11.0-100");
            Mapping(("9450", "MyProton"));

            Assert.Equal(Path.GetFullPath(tool), Select().ScriptPath);
        }

        [Fact]
        public void PrefixVersion_PreferredOverNewerDefault()
        {
            var p9 = ValveTool("Proton 9.0 (Beta)", "9.0-203");
            ValveTool("Proton 11.0", "11.0-100");
            Mapping(("0", "proton_11"));
            Prefix(9450, "9.0-203");

            var sel = Select();

            Assert.Equal(Path.GetFullPath(p9), sel.ScriptPath);
            Assert.Contains("prefix version 9.0-203", sel.Reason);
        }

        [Fact]
        public void PrefixVersion_SameSeriesWhenNoExactMatch()
        {
            var p9 = ValveTool("Proton 9.0", "9.0-205");
            ValveTool("Proton 11.0", "11.0-100");
            Prefix(9450, "9.0-203");

            var sel = Select();

            Assert.Equal(Path.GetFullPath(p9), sel.ScriptPath);
            Assert.Contains("same Proton series", sel.Reason);
        }

        [Fact]
        public void OnlyNewerAvailable_SaysPrefixWillBeUpgraded()
        {
            ValveTool("Proton 11.0", "11.0-100");
            Mapping(("0", "proton_11"));
            Prefix(9450, "9.0-203");

            var sel = Select();

            Assert.Contains("Steam default compat tool", sel.Reason);
            Assert.Contains("will upgrade it", sel.Reason);
        }

        [Fact]
        public void MappedToolMissing_FallsBackToNewestWithNotes()
        {
            ValveTool("Proton 8.0", "8.0-104");
            var p11 = ValveTool("Proton 11.0", "11.0-100");
            Mapping(("9450", "GE-Proton7-1"));

            var sel = Select();

            Assert.Equal(Path.GetFullPath(p11), sel.ScriptPath);
            Assert.Contains("\"GE-Proton7-1\", which is not installed", sel.Reason);
            Assert.Contains("using the newest installed Proton", sel.Reason);
        }

        [Fact]
        public void NoConfig_NoPrefix_PicksNewest()
        {
            ValveTool("Proton 9.0", "9.0-203");
            var p11 = ValveTool("Proton 11.0", "11.0-100");

            Assert.Equal(Path.GetFullPath(p11), Select().ScriptPath);
        }

        [Fact]
        public void NoTools_ReturnsNull()
        {
            Directory.CreateDirectory(_steam);
            Mapping(("0", "proton_11"));

            Assert.Null(ProtonToolResolver.Select(9450, null, [_steam]));
        }

        [Fact]
        public void TextVdf_ParsesCommentsAndEscapes()
        {
            var vdf = TextVdf.Parse("\"a\" // c\n{\n \"path\" \"C:\\\\Games\"\n \"b\" { \"name\" \"x\" }\n}\n");

            Assert.Equal("C:\\Games", vdf.Find("a")!.GetString("path"));
            Assert.Equal("x", vdf.Find("b")!.GetString("NAME"));
        }

        // ---- Steam Linux Runtime ----------------------------------------------------------

        [Fact]
        public void RequiredToolAppId_IsReadFromToolManifest()
        {
            var ge = Path.GetDirectoryName(CustomTool("GE-Proton10-34", "GE-Proton10-34", "GE-Proton10-34"))!;
            File.WriteAllText(Path.Combine(ge, "toolmanifest.vdf"),
                "\"manifest\"\n{\n  \"version\" \"2\"\n  \"commandline\" \"/proton %verb%\"\n  \"require_tool_appid\" \"1628350\"\n}\n");
            var old = Path.GetDirectoryName(ValveTool("Proton 5.13", "5.13-6"))!;

            Assert.Equal(1628350, ProtonToolResolver.ReadRequiredToolAppId(ge));
            Assert.Null(ProtonToolResolver.ReadRequiredToolAppId(old));
            Assert.Null(ProtonToolResolver.ReadRequiredToolAppId(Path.Combine(_steam, "missing")));
        }

        private string Runtime(string library, string installDir, bool withEntryPoint = true)
        {
            var steamApps = Path.Combine(library, "steamapps");
            var dir = Path.Combine(steamApps, "common", installDir);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(steamApps, "appmanifest_1628350.acf"),
                $"\"AppState\"\n{{\n\t\"appid\"\t\t\"1628350\"\n\t\"installdir\"\t\t\"{installDir}\"\n}}\n");
            var entryPoint = Path.Combine(dir, "_v2-entry-point");
            if (withEntryPoint)
                File.WriteAllText(entryPoint, "#!/bin/sh\n");
            return entryPoint;
        }

        [Fact]
        public void RuntimeEntryPoint_IsFoundThroughAppManifest_InAnyLibrary()
        {
            var library = Path.Combine(_steam, "library2");
            Directory.CreateDirectory(Path.Combine(_steam, "steamapps"));
            File.WriteAllText(Path.Combine(_steam, "steamapps", "libraryfolders.vdf"),
                $"\"libraryfolders\"\n{{\n\t\"1\"\n\t{{\n\t\t\"path\"\t\t\"{library.Replace("\\", "\\\\")}\"\n\t}}\n}}\n");
            var entryPoint = Runtime(library, "SteamLinuxRuntime_sniper");

            Assert.Equal(entryPoint, ProtonToolResolver.FindRuntimeEntryPoint(1628350, [_steam]));
        }

        [Fact]
        public void RuntimeEntryPoint_MissingOrWithoutEntryPoint_IsNull()
        {
            Directory.CreateDirectory(_steam);
            Assert.Null(ProtonToolResolver.FindRuntimeEntryPoint(1628350, [_steam]));

            Runtime(_steam, "SteamLinuxRuntime", withEntryPoint: false);
            Assert.Null(ProtonToolResolver.FindRuntimeEntryPoint(1628350, [_steam]));
        }
    }
}
