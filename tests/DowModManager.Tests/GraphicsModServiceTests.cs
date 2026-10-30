using System.Net.Http;
using DoW_Mod_Manager.Services;
using Xunit;

namespace DowModManager.Tests
{
    /// <summary>DXVK / dgVoodoo2 installs into a temp game folder through a fake download service.</summary>
    public class GraphicsModServiceTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "dmm_graphics_" + Guid.NewGuid().ToString("N"));
        private readonly FakeDownload _download = new();
        private readonly GraphicsModService _service;

        public GraphicsModServiceTests()
        {
            Directory.CreateDirectory(_dir);
            _service = new GraphicsModService(_download);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        private sealed class FakeDownload : IDownloadService
        {
            public string RemoteVersion { get; set; } = "1.0";
            public string? FailOn { get; set; }
            public List<string> Urls { get; } = [];

            public Task<string> DownloadStringAsync(string url, CancellationToken cancellationToken = default)
            {
                if (FailOn is not null && url.EndsWith(FailOn))
                    throw new HttpRequestException("offline");
                return Task.FromResult(RemoteVersion);
            }

            public Task DownloadFileAsync(string url, string destPath, CancellationToken cancellationToken = default)
            {
                if (FailOn is not null && url.EndsWith(FailOn))
                    throw new HttpRequestException("offline");
                Urls.Add(url);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.WriteAllText(destPath, destPath.EndsWith(".version") ? RemoteVersion : url);
                return Task.CompletedTask;
            }
        }

        private string[] Files() => Directory.GetFileSystemEntries(_dir).Select(Path.GetFileName).Order().ToArray()!;

        [Fact]
        public async Task InstallDxvk_DownloadsTheLegacyFileSet()
        {
            await _service.InstallAsync(_dir, GraphicsWrapper.Dxvk);

            Assert.Equal(["d3d8.dll", "d3d9.dll", "dxvk.conf", "dxvk.version"], Files());
            Assert.All(_download.Urls, u => Assert.Contains("/DoW%20Mod%20Manager/DXVK/", u));
            Assert.True(_service.IsInstalled(_dir, GraphicsWrapper.Dxvk));
            Assert.False(_service.IsInstalled(_dir, GraphicsWrapper.DgVoodoo2));
        }

        [Fact]
        public async Task InstallDgVoodoo2_RemovesDxvk_AndLeavesOneD3d9Dll()
        {
            await _service.InstallAsync(_dir, GraphicsWrapper.Dxvk);
            await _service.InstallAsync(_dir, GraphicsWrapper.DgVoodoo2);

            Assert.Equal(["D3D9.dll", "dgVoodoo.conf", "dgVoodoo.version", "dgVoodooCpl.exe"], Files());
            Assert.True(_service.IsInstalled(_dir, GraphicsWrapper.DgVoodoo2));
            Assert.False(_service.IsInstalled(_dir, GraphicsWrapper.Dxvk));
        }

        [Fact]
        public async Task FailedDownload_LeavesTheInstalledWrapperAlone()
        {
            await _service.InstallAsync(_dir, GraphicsWrapper.Dxvk);
            _download.FailOn = "dgVoodooCpl.exe";

            await Assert.ThrowsAsync<HttpRequestException>(() => _service.InstallAsync(_dir, GraphicsWrapper.DgVoodoo2));

            Assert.True(_service.IsInstalled(_dir, GraphicsWrapper.Dxvk));
            Assert.Equal(["d3d8.dll", "d3d9.dll", "dxvk.conf", "dxvk.version"], Files());
        }

        [Fact]
        public async Task Remove_DeletesOnlyThatWrapper()
        {
            File.WriteAllText(Path.Combine(_dir, "Soulstorm.exe"), "");
            await _service.InstallAsync(_dir, GraphicsWrapper.DgVoodoo2);

            _service.Remove(_dir, GraphicsWrapper.DgVoodoo2);

            Assert.Equal(["Soulstorm.exe"], Files());
        }

        [Theory]
        [InlineData("1.0", GraphicsWrapperState.Installed)]
        [InlineData("2.5", GraphicsWrapperState.UpdateAvailable)]
        public async Task GetState_ComparesWithTheUpstreamVersion(string remote, GraphicsWrapperState expected)
        {
            await _service.InstallAsync(_dir, GraphicsWrapper.Dxvk);
            _download.RemoteVersion = remote;

            Assert.Equal(expected, await _service.GetStateAsync(_dir, GraphicsWrapper.Dxvk, checkForUpdate: true));
            Assert.Equal(GraphicsWrapperState.Installed, await _service.GetStateAsync(_dir, GraphicsWrapper.Dxvk, checkForUpdate: false));
        }

        [Fact]
        public async Task GetState_Offline_ReportsInstalled()
        {
            await _service.InstallAsync(_dir, GraphicsWrapper.DgVoodoo2);
            _download.FailOn = "dgVoodoo.version";

            Assert.Equal(GraphicsWrapperState.Installed, await _service.GetStateAsync(_dir, GraphicsWrapper.DgVoodoo2, checkForUpdate: true));
            Assert.Equal(GraphicsWrapperState.NotInstalled, await _service.GetStateAsync(_dir, GraphicsWrapper.Dxvk, checkForUpdate: true));
        }

        [Fact]
        public async Task WineDllOverrides_ListsTheDllsPresent()
        {
            Assert.Null(GraphicsModService.WineDllOverrides(_dir));

            await _service.InstallAsync(_dir, GraphicsWrapper.Dxvk);
            Assert.Equal("d3d9,d3d8=n,b", GraphicsModService.WineDllOverrides(_dir));

            await _service.InstallAsync(_dir, GraphicsWrapper.DgVoodoo2);
            Assert.Equal("d3d9=n,b", GraphicsModService.WineDllOverrides(_dir));
        }
    }
}
