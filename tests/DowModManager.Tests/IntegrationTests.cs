using System.IO;
using System.Linq;
using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Platform.Linux;
using DoW_Mod_Manager.Services;
using Xunit;

namespace DowModManager.Tests;

/// <summary>
/// WS7 integration coverage: exercises the Linux install locator (WS2) + module/mod services
/// (WS3) + shared models against a synthetic on-disk game directory, plus the launch command
/// line. No real Steam/Proton needed.
/// </summary>
public sealed class IntegrationTests
{
    private static string CreateSoulstormDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dowmm-it-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        // Game executable (content irrelevant for identification).
        File.WriteAllText(Path.Combine(dir, "Soulstorm.exe"), "");

        // Base modules — must be skipped by the mod service.
        File.WriteAllText(Path.Combine(dir, "W40k.module"), "Name = \"W40k\"\nModFolder = W40k\n");
        File.WriteAllText(Path.Combine(dir, "DXP2.module"), "Name = \"DXP2\"\nModFolder = DXP2\n");

        // A playable mod that requires W40k and ships its own asset folder.
        File.WriteAllText(Path.Combine(dir, "MyMod.module"),
            "Playable = 1\nModFolder = MyMod\nModVersion = 1.2.3\nRequiredMod.1 = W40k\n");
        Directory.CreateDirectory(Path.Combine(dir, "MyMod"));   // mod asset folder present
        Directory.CreateDirectory(Path.Combine(dir, "W40k"));    // required dep asset folder present

        return dir;
    }

    [Fact]
    public void LinuxLocator_identifies_Soulstorm_directory()
    {
        string dir = CreateSoulstormDir();
        try
        {
            var locator = new LinuxGameInstallLocator();
            GameInstall? install = locator.IdentifyInstall(dir);

            Assert.NotNull(install);
            Assert.Equal(GameVariant.Soulstorm, install!.Variant);
            Assert.Equal("Soulstorm.exe", install.ExecutableName);
            Assert.True(install.SupportsNoFog);
            Assert.False(install.IsOldGame);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ModService_lists_playable_mods_and_resolves_dependencies()
    {
        string dir = CreateSoulstormDir();
        try
        {
            var mods = new ModService(new ModuleParser());

            var playable = mods.GetPlayableMods(dir, isOldGame: false);
            Assert.Single(playable);                          // base modules skipped
            Assert.Equal("MyMod", playable[0].Name);
            Assert.Equal("1.2.3", playable[0].ModVersion);

            var deps = mods.ResolveDependencies(dir, playable[0]);
            Assert.Single(deps);
            Assert.Equal("W40k", deps[0].RequiredModName);
            Assert.True(deps[0].IsInstalled);                 // W40k asset folder exists
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ModService_flags_missing_dependency()
    {
        string dir = CreateSoulstormDir();
        try
        {
            Directory.Delete(Path.Combine(dir, "W40k"), recursive: true);   // remove the dep asset folder

            var mods = new ModService(new ModuleParser());
            var mod = mods.GetPlayableMods(dir, isOldGame: false).Single();
            var deps = mods.ResolveDependencies(dir, mod);

            Assert.False(deps.Single().IsInstalled);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, false, false, false, "-modname MyMod")]
    [InlineData(true, true, false, false, "-modname MyMod -dev -nomovies")]
    [InlineData(true, true, true, true, "-modname MyMod -dev -nomovies -forcehighpoly -noprecachemodels")]
    public void LaunchOptions_builds_legacy_command_line(bool dev, bool noMovies, bool highPoly, bool noPrecache, string expected)
    {
        var options = new LaunchOptions
        {
            ModName = "MyMod",
            Dev = dev,
            NoMovies = noMovies,
            ForceHighPoly = highPoly,
            NoPrecacheModels = noPrecache,
        };

        Assert.Equal(expected, options.ToArgumentString());
    }
}
