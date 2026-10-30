using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Services;
using Xunit;

namespace DowModManager.Tests
{
    public class ModlistServiceTests : IDisposable
    {
        private const string Sample =
            "209\n" +
            "[SS]\n" +
            "\n" +
            "Alpha Mod\n" +
            "AlphaModule\n" +
            "https://site/alpha\n" +
            "https://dl/alpha\n" +
            "-\n" +
            "\n" +
            "Beta Mod\n" +
            "-\n" +
            "https://site/beta\n" +
            "https://dl/beta\n" +
            "https://dl/beta-patch\n" +
            "\n" +
            "[DC]\n" +
            "\n" +
            "Gamma Mod\n" +
            "GammaModule\n" +
            "https://site/gamma\n" +
            "https://dl/gamma\n" +
            "-\n" +
            "\n" +
            "[END]\n";

        private readonly string _dir;

        public ModlistServiceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dmm_modlist_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        [Fact]
        public void Parse_ReturnsOnlyTheRequestedSection()
        {
            var service = new ModlistService(new FakeDownloadService(), _dir);

            var ss = service.Parse(Sample, GameVariant.Soulstorm);
            var dc = service.Parse(Sample, GameVariant.DarkCrusade);

            Assert.Equal(
                [new DownloadableMod("Alpha Mod", "AlphaModule", "https://site/alpha", "https://dl/alpha", ""),
                 new DownloadableMod("Beta Mod", "", "https://site/beta", "https://dl/beta", "https://dl/beta-patch")],
                ss);
            Assert.Equal("Gamma Mod", Assert.Single(dc).Name);
            Assert.Empty(service.Parse(Sample, GameVariant.WinterAssault));
        }

        [Fact]
        public void Parse_HandlesWindowsLineEndings()
        {
            var service = new ModlistService(new FakeDownloadService(), _dir);

            var ss = service.Parse(Sample.Replace("\n", "\r\n"), GameVariant.Soulstorm);

            Assert.Equal(2, ss.Count);
            Assert.Equal("https://dl/alpha", ss[0].DownloadLink);
        }

        [Fact]
        public async Task LoadAsync_DownloadsWhenNotCached()
        {
            var fake = new FakeDownloadService { FileContent = Sample };
            var service = new ModlistService(fake, _dir);

            var mods = await service.LoadAsync(GameVariant.Soulstorm);

            Assert.Equal(2, mods.Count);
            Assert.Equal(1, fake.FileDownloads);
        }

        [Fact]
        public async Task LoadAsync_RefreshesOnlyWhenRemoteIsNewer()
        {
            File.WriteAllText(Path.Combine(_dir, ModlistService.ModlistFileName), Sample);
            var same = new FakeDownloadService { RemoteVersion = "2.0.9", FileContent = Sample };
            await new ModlistService(same, _dir).LoadAsync(GameVariant.Soulstorm);
            Assert.Equal(0, same.FileDownloads);

            var newer = new FakeDownloadService { RemoteVersion = "2.1.0", FileContent = Sample.Replace("209", "210") };
            await new ModlistService(newer, _dir).LoadAsync(GameVariant.Soulstorm);
            Assert.Equal(1, newer.FileDownloads);
        }

        [Fact]
        public async Task LoadAsync_FallsBackToCacheWhenOffline()
        {
            File.WriteAllText(Path.Combine(_dir, ModlistService.ModlistFileName), Sample);
            var offline = new FakeDownloadService { Offline = true };

            var mods = await new ModlistService(offline, _dir).LoadAsync(GameVariant.DarkCrusade);

            Assert.Equal("Gamma Mod", Assert.Single(mods).Name);
        }

        [Theory]
        [InlineData("2.0.9", 209)]
        [InlineData("209\n", 209)]
        [InlineData("garbage", 0)]
        public void ParseVersion_StripsDots(string input, int expected) =>
            Assert.Equal(expected, ModlistService.ParseVersion(input));

        private sealed class FakeDownloadService : IDownloadService
        {
            public string RemoteVersion { get; init; } = "0";
            public string FileContent { get; init; } = "";
            public bool Offline { get; init; }
            public int FileDownloads { get; private set; }

            public Task<string> DownloadStringAsync(string url, CancellationToken cancellationToken = default) =>
                Offline ? throw new HttpRequestException("offline") : Task.FromResult(RemoteVersion);

            public Task DownloadFileAsync(string url, string destPath, CancellationToken cancellationToken = default)
            {
                if (Offline)
                    throw new HttpRequestException("offline");
                FileDownloads++;
                File.WriteAllText(destPath, FileContent);
                return Task.CompletedTask;
            }
        }
    }
}
