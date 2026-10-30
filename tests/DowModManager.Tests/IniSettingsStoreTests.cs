using DoW_Mod_Manager.Services;
using Xunit;

namespace DowModManager.Tests
{
    public class IniSettingsStoreTests : IDisposable
    {
        private readonly string _dir;
        private readonly ISettingsStore _store = new IniSettingsStore();

        public IniSettingsStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dmm_ini_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        private void WriteIni(params string[] lines) =>
            File.WriteAllLines(Path.Combine(_dir, IniSettingsStore.ConfigFileName), lines);

        [Fact]
        public void Read_ReturnsDefaults_WhenFileAbsent()
        {
            IDictionary<string, int> settings = _store.Read(_dir);
            IDictionary<string, int> defaults = _store.GetDefaults();

            Assert.Equal(defaults[SettingsKeys.NoMovies], settings[SettingsKeys.NoMovies]);
            Assert.Equal(defaults[SettingsKeys.ActionState], settings[SettingsKeys.ActionState]);
        }

        [Fact]
        public void Read_OverridesDefaults_FromValidLines()
        {
            WriteIni(
                "Dev = 1",
                "ChoiceIndex = 3");

            IDictionary<string, int> settings = _store.Read(_dir);

            Assert.Equal(1, settings[SettingsKeys.Dev]);
            Assert.Equal(3, settings[SettingsKeys.ChoiceIndex]);
        }

        [Fact]
        public void Read_IgnoresMalformedLines()
        {
            WriteIni(
                "this line has no equal",
                "Dev = = double equal",
                "ChoiceIndex = 2");

            IDictionary<string, int> settings = _store.Read(_dir);

            // Malformed lines ignored; Dev stays at default (0), ChoiceIndex applied.
            Assert.Equal(0, settings[SettingsKeys.Dev]);
            Assert.Equal(2, settings[SettingsKeys.ChoiceIndex]);
        }

        [Fact]
        public void Read_ClampsNegativeToZero()
        {
            WriteIni("Dev = -5");

            IDictionary<string, int> settings = _store.Read(_dir);

            Assert.Equal(0, settings[SettingsKeys.Dev]);
        }

        [Fact]
        public void Read_NonIntegerValue_BecomesZero()
        {
            WriteIni("Dev = notanumber");

            IDictionary<string, int> settings = _store.Read(_dir);

            Assert.Equal(0, settings[SettingsKeys.Dev]);
        }

        [Fact]
        public void Read_ActionState_IgnoredWhenOutOfRange()
        {
            WriteIni("ActionState = 99");

            IDictionary<string, int> settings = _store.Read(_dir);

            // Out-of-range ActionState is ignored, so default is retained.
            Assert.Equal(_store.GetDefaults()[SettingsKeys.ActionState], settings[SettingsKeys.ActionState]);
        }

        [Fact]
        public void Read_ActionState_AppliedWhenInRange()
        {
            WriteIni("ActionState = 4");

            IDictionary<string, int> settings = _store.Read(_dir);

            Assert.Equal(4, settings[SettingsKeys.ActionState]);
        }

        [Fact]
        public void WriteThenRead_RoundTrips()
        {
            IDictionary<string, int> defaults = _store.GetDefaults();
            defaults[SettingsKeys.Dev] = 1;
            defaults[SettingsKeys.ChoiceIndex] = 5;

            _store.Write(_dir, defaults);
            IDictionary<string, int> read = _store.Read(_dir);

            Assert.Equal(1, read[SettingsKeys.Dev]);
            Assert.Equal(5, read[SettingsKeys.ChoiceIndex]);
            Assert.Equal(defaults[SettingsKeys.AOTCompilation], read[SettingsKeys.AOTCompilation]);
        }

        [Fact]
        public void Write_UsesCrlfLineEndings()
        {
            _store.Write(_dir, _store.GetDefaults());

            CrlfAssert.File(Path.Combine(_dir, IniSettingsStore.ConfigFileName));
        }

        [Fact]
        public void WriteString_KeepsSpaces_AndSurvivesNumericWrites()
        {
            const string dir = "/home/me/Games/Dawn of War Soulstorm";
            _store.WriteString(_dir, SettingsKeys.SoulstormDir, dir);

            var settings = _store.Read(_dir);
            settings[SettingsKeys.Dev] = 1;
            _store.Write(_dir, settings);

            Assert.Equal(dir, _store.ReadString(_dir, SettingsKeys.SoulstormDir));
            Assert.Equal(1, _store.Read(_dir)[SettingsKeys.Dev]);
            CrlfAssert.File(Path.Combine(_dir, IniSettingsStore.ConfigFileName));
        }

        [Fact]
        public void WriteString_Empty_RemovesTheSetting()
        {
            _store.WriteString(_dir, SettingsKeys.SoulstormDir, "C:\\Games\\Soulstorm");
            _store.WriteString(_dir, SettingsKeys.SoulstormDir, "");

            Assert.Null(_store.ReadString(_dir, SettingsKeys.SoulstormDir));
            Assert.DoesNotContain("SoulstormDir", File.ReadAllText(Path.Combine(_dir, IniSettingsStore.ConfigFileName)));
        }

        [Fact]
        public void WriteString_RejectsNumericKeys()
        {
            Assert.Throws<ArgumentException>(() => _store.WriteString(_dir, SettingsKeys.Dev, "1"));
        }
    }
}
