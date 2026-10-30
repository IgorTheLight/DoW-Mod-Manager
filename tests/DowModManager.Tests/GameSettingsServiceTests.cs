using System.Text;
using DoW_Mod_Manager.Services;
using Xunit;
using K = DoW_Mod_Manager.Services.GameSettingsKeys;

namespace DowModManager.Tests
{
    public class GameSettingsServiceTests : IDisposable
    {
        private readonly string _dir;
        private readonly GameSettingsService _service = new();

        public GameSettingsServiceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dmm_gamesettings_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        private string ProfileDir(string name)
        {
            string dir = Path.Combine(_dir, "Profiles", name);
            Directory.CreateDirectory(dir);
            return dir;
        }

        // Verbatim Definitive Edition Local.ini: keys the manager doesn't know (screenvsync,
        // screengama) and values its validators reject (screenwindowed=2, a real adapter name).
        private const string DefinitiveEditionLocalIni =
            "[global]\r\nforce_watch_movies=1\r\nplayerprofile=Profile1\r\nscreenadapter=0\r\n" +
            "screenantialias=0\r\nscreendevice=NVIDIA GeForce RTX 4070 Ti\r\nscreengama=10\r\n" +
            "screengamma=10\r\nscreenheight=1080\r\nscreenrefresh=0\r\nscreenvsync=1\r\n" +
            "screenwidth=1920\r\nscreenwindowed=2\r\ntexturedetail=2\r\n";

        [Fact]
        public void Write_ExistingLocalIni_KeepsUnknownKeysAndUnchangedRejectedValues()
        {
            string path = Path.Combine(_dir, "Local.ini");
            File.WriteAllText(path, DefinitiveEditionLocalIni);

            var s = _service.Read(_dir);
            _service.Write(_dir, s);

            Assert.Equal(DefinitiveEditionLocalIni, File.ReadAllText(path));
        }

        [Fact]
        public void Write_ExistingLocalIni_UpdatesChangedValuesInPlace()
        {
            string path = Path.Combine(_dir, "Local.ini");
            File.WriteAllText(path, DefinitiveEditionLocalIni);

            var s = _service.Read(_dir);
            s[K.ScreenWidth] = "2560";
            s[K.ScreenWindowed] = "1";
            s[K.SoundQuality] = "1";
            _service.Write(_dir, s);

            string[] lines = File.ReadAllLines(path);
            Assert.Contains("screenwidth=2560", lines);
            Assert.Contains("screenwindowed=1", lines);
            Assert.Contains("screenvsync=1", lines);
            Assert.Contains("screendevice=NVIDIA GeForce RTX 4070 Ti", lines);
            Assert.Equal($"{K.SoundQuality}=1", lines[^1]);
            Assert.DoesNotContain(lines, l => l.StartsWith(K.ShadowMap + "="));
            Assert.Equal(1, lines.Count(l => l.StartsWith("screenwidth=")));
            CrlfAssert.File(path);
        }

        [Fact]
        public void Read_WithoutFiles_ReturnsDefaults()
        {
            var settings = _service.Read(_dir);

            Assert.Equal(_service.GetDefaults().Values, settings.Values);
        }

        [Fact]
        public void Read_LocalIni_AcceptsValidValuesAndIgnoresInvalidOnes()
        {
            File.WriteAllText(Path.Combine(_dir, "Local.ini"),
                "[global]\n" +
                "screenwidth=1360\n" +
                "screenheight=864\n" +
                "dynamiclights=7\n" +          // out of range → default kept
                "texturedetail=0\n" +
                "screengamma=abc\n" +          // not a number → default kept
                "bad=line=here\n");

            var s = _service.Read(_dir);

            Assert.Equal("1360", s[K.ScreenWidth]);
            Assert.Equal("864", s[K.ScreenHeight]);
            Assert.Equal("2", s[K.DynamicLights]);
            Assert.Equal("0", s[K.TextureDetail]);
            Assert.Equal("10", s[K.ScreenGamma]);
        }

        [Fact]
        public void Read_ResolvesPathsCaseInsensitively()
        {
            Directory.CreateDirectory(Path.Combine(_dir, "drivers"));
            File.WriteAllText(Path.Combine(_dir, "drivers", "SPDX9_CONFIG.txt"), "foo 1\nallowhwcursor 0\n");

            Assert.Equal("0", _service.Read(_dir)[K.AllowHwCursor]);
        }

        [Fact]
        public void Read_UnknownProfileFallsBackToFirstExisting()
        {
            File.WriteAllText(Path.Combine(_dir, "Local.ini"), "playerprofile=Profile9\n");
            ProfileDir("Profile2");
            ProfileDir("Profile1");

            Assert.Equal("Profile1", _service.Read(_dir)[K.PlayerProfile]);
        }

        [Fact]
        public void WriteThenRead_RoundTripsAllFiles()
        {
            ProfileDir("Profile1");
            Directory.CreateDirectory(Path.Combine(_dir, "Drivers"));
            File.WriteAllText(Path.Combine(_dir, "Drivers", "spdx9_config.txt"), "something 1\nallowhwcursor 1\n");

            var s = _service.GetDefaults();
            s[K.ScreenWidth] = "1920";
            s[K.ScreenHeight] = "1080";
            s[K.AllowHwCursor] = "0";
            s[K.VolumeMusic] = "0.50000";
            s[K.InvertPan] = "0";
            _service.Write(_dir, s);

            var back = _service.Read(_dir);
            Assert.Equal(s.Values, back.Values);
            Assert.StartsWith("[global]", File.ReadAllText(Path.Combine(_dir, "Local.ini")));
            Assert.Equal(["something 1", "allowhwcursor 0"], File.ReadAllLines(Path.Combine(_dir, "Drivers", "spdx9_config.txt")));
        }

        [Fact]
        public void Write_UpdatesExistingPlayerConfigInPlace()
        {
            string profile = ProfileDir("Profile1");
            string cfg = Path.Combine(profile, "playercfg.lua");
            File.WriteAllText(cfg, "Controls = \n{\n\tinvertPan = 1,\n\tscrollRate = 1,\n}\nOther = \n{\n\tkeep = 42,\n}\n");

            var s = _service.GetDefaults();
            s[K.InvertPan] = "0";
            s[K.ScrollRate] = "1.50000";
            _service.Write(_dir, s);

            string text = File.ReadAllText(cfg);
            Assert.Contains("\tinvertPan = 0,", text);
            Assert.Contains("\tscrollRate = 1.50000,", text);
            Assert.Contains("\tkeep = 42,", text);
        }

        [Fact]
        public void Profiles_CreateRenameDelete()
        {
            ProfileDir("Profile1");
            File.WriteAllText(Path.Combine(_dir, "Profiles", "Profile1", "name.dat"), "Alice", Encoding.Unicode);

            var created = _service.CreateProfile(_dir, "Bob");
            Assert.Equal(new PlayerProfile("Profile2", "Bob"), created);

            _service.RenameProfile(_dir, "Profile2", "Robert");
            Assert.Equal(
                [new PlayerProfile("Profile1", "Alice"), new PlayerProfile("Profile2", "Robert")],
                _service.GetProfiles(_dir));

            _service.DeleteProfile(_dir, "Profile1");
            Assert.Equal("Profile2", Assert.Single(_service.GetProfiles(_dir)).ProfileName);

            // The freed index is reused.
            Assert.Equal("Profile1", _service.CreateProfile(_dir, "Carol").ProfileName);
        }

        [Fact]
        public void PlayerName_WithoutBom_IsReadAsUtf16()
        {
            string profile = ProfileDir("Profile1");
            File.WriteAllBytes(Path.Combine(profile, "name.dat"), Encoding.Unicode.GetBytes("Zoë"));

            Assert.Equal("Zoë", Assert.Single(_service.GetProfiles(_dir)).PlayerName);
        }

        [Theory]
        [InlineData(GraphicsPreset.LowMinus, false, true)]
        [InlineData(GraphicsPreset.Medium, false, false)]
        [InlineData(GraphicsPreset.UltraPlus, true, false)]
        public void ApplyGraphicsPreset_ReturnsLaunchFlags(GraphicsPreset preset, bool highPoly, bool noPrecache)
        {
            var s = _service.GetDefaults();

            var change = GameSettingsService.ApplyGraphicsPreset(s, preset);

            Assert.Equal(new LaunchFlagChange(highPoly, noPrecache), change);
        }

        [Fact]
        public void ApplyGraphicsPreset_Ultra_SetsAllShadows()
        {
            var s = _service.GetDefaults();

            GameSettingsService.ApplyGraphicsPreset(s, GraphicsPreset.Ultra);

            Assert.Equal(("1", "1", "1"), (s[K.ShadowBlob], s[K.ShadowMap], s[K.ShadowVolume]));
            Assert.Equal("3", s[K.DynamicLights]);
        }

        [Fact]
        public void ApplyAudioPreset_Low()
        {
            var s = _service.GetDefaults();

            GameSettingsService.ApplyAudioPreset(s, AudioPreset.Low);

            Assert.Equal(("1", "16", "0"), (s[K.SoundLimitSamples], s[K.SoundNrChannels], s[K.SoundQuality]));
        }

        [Fact]
        public void Write_UsesCrlfLineEndingsForAllGameFiles()
        {
            ProfileDir("Profile1");
            Directory.CreateDirectory(Path.Combine(_dir, "Drivers"));
            string driver = Path.Combine(_dir, "Drivers", "spdx9_config.txt");
            File.WriteAllText(driver, "something 1\nallowhwcursor 1\n");

            _service.Write(_dir, _service.GetDefaults()); // creates playercfg.lua

            CrlfAssert.File(Path.Combine(_dir, "Local.ini"));
            CrlfAssert.File(driver);
            string cfg = Path.Combine(_dir, "Profiles", "Profile1", "playercfg.lua");
            CrlfAssert.File(cfg);

            _service.Write(_dir, _service.GetDefaults()); // updates playercfg.lua in place
            CrlfAssert.File(cfg);
        }

        [Fact]
        public void Write_CreatesDriverConfigWithCrlf()
        {
            Directory.CreateDirectory(Path.Combine(_dir, "Drivers"));

            _service.Write(_dir, _service.GetDefaults());

            CrlfAssert.File(Path.Combine(_dir, "Drivers", "spdx9_config.txt"));
        }
    }
}
