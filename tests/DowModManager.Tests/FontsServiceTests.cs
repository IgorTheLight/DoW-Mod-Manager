using DoW_Mod_Manager.Services;
using Xunit;

namespace DowModManager.Tests
{
    public class FontsServiceTests : IDisposable
    {
        private const string Fnt =
            "font = \n{\n" +
            "\ttype \t\t= \"FreeType2\";\n" +
            "\tfile\t\t= \"gil_____.ttf\";\n\n" +
            "\tsizeDefault \t= 30;\n" +
            "\tsize800\t\t= 12;\t\n" +
            "\tsize1024\t= 14;\t\n" +
            "\tsize24 \t= 24;\n" +
            "}\n";

        private readonly string _dir;
        private readonly string _fontDir;
        private readonly FontsService _service = new();

        public FontsServiceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dmm_fonts_" + Guid.NewGuid().ToString("N"));
            // Lower-case "data" on disk: the service must still find it on case-sensitive systems.
            _fontDir = Path.Combine(_dir, "Engine", "Locale", "English", "data", "font");
            Directory.CreateDirectory(_fontDir);
            File.WriteAllText(Path.Combine(_fontDir, "gillsans_11.fnt"), Fnt);
            File.WriteAllText(Path.Combine(_fontDir, "gil_____.ttf"), "");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        [Fact]
        public void Read_ParsesFileAndSizeForWidth()
        {
            Assert.True(_service.IsAvailable(_dir));

            var entries = _service.Read(_dir, "1024");

            Assert.Equal(10, entries.Count);
            var gill = entries.Single(e => e.DowFontFile == "gillsans_11.fnt");
            Assert.Equal(("gil_____.ttf", true, 14), (gill.FontFileName, gill.FontFileFound, gill.Size));
            Assert.False(entries.Single(e => e.DowFontFile == "gillsans_16.fnt").Exists);
        }

        [Fact]
        public void Write_ChangesOnlyTheSelectedWidth()
        {
            var gill = _service.Read(_dir, "800").Single(e => e.DowFontFile == "gillsans_11.fnt");

            _service.Write(_dir, "800", [gill with { Size = 16, FontFileName = "other.ttf" }]);

            string text = File.ReadAllText(Path.Combine(_fontDir, "gillsans_11.fnt"));
            Assert.Contains("\tsize800\t\t= 16;", text);
            Assert.Contains("\tsize1024\t= 14;", text);
            Assert.Contains("\"other.ttf\"", text);
        }

        [Fact]
        public void BackupAndRestore_RoundTrips()
        {
            _service.EnsureBackup(_dir);
            string fnt = Path.Combine(_fontDir, "gillsans_11.fnt");
            File.WriteAllText(fnt, "broken");

            Assert.True(_service.RestoreDefaults(_dir));
            Assert.Equal(Fnt, File.ReadAllText(fnt));
        }

        [Fact]
        public void Read_MatchesFontFileCaseInsensitively()
        {
            File.WriteAllText(Path.Combine(_fontDir, "gillsans_11.fnt"), Fnt.Replace("gil_____.ttf", "GIL_____.TTF"));

            var gill = _service.Read(_dir, "800").Single(e => e.DowFontFile == "gillsans_11.fnt");

            Assert.True(gill.FontFileFound);
        }

        [Theory]
        [InlineData("1920", "1280")]
        [InlineData("1024", "1024")]
        [InlineData("1360", "Default")]
        public void MapScreenWidth_UsesLegacyHeuristic(string width, string expected) =>
            Assert.Equal(expected, _service.MapScreenWidth(width));

        [Fact]
        public void Write_UsesCrlfLineEndings()
        {
            var gill = _service.Read(_dir, "800").Single(e => e.DowFontFile == "gillsans_11.fnt");

            _service.Write(_dir, "800", [gill with { Size = 16 }]);

            CrlfAssert.File(Path.Combine(_fontDir, "gillsans_11.fnt"));
        }

        [Fact]
        public void ReadWrite_FindManagedFontWithDifferentCasing()
        {
            File.Move(Path.Combine(_fontDir, "gillsans_11.fnt"), Path.Combine(_fontDir, "GillSans_11.FNT"));

            var gill = _service.Read(_dir, "800").Single(e => e.DowFontFile == "gillsans_11.fnt");
            Assert.True(gill.Exists);

            _service.Write(_dir, "800", [gill with { Size = 16 }]);
            Assert.Contains("\tsize800\t\t= 16;", File.ReadAllText(Path.Combine(_fontDir, "GillSans_11.FNT")));
        }
    }
}
