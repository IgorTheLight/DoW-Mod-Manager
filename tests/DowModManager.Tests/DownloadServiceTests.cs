using System.Net;
using System.Text;
using DoW_Mod_Manager.Services;
using Xunit;

namespace DowModManager.Tests
{
    public class DownloadServiceTests
    {
        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
                _responder = responder;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(_responder(request));
        }

        [Fact]
        public async Task DownloadStringAsync_ReturnsBody()
        {
            var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("hello world", Encoding.UTF8)
            });
            var service = new DownloadService(new HttpClient(handler));

            string result = await service.DownloadStringAsync("https://example.com/version");

            Assert.Equal("hello world", result);
        }

        [Fact]
        public async Task DownloadStringAsync_Throws_OnHttpError()
        {
            var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            var service = new DownloadService(new HttpClient(handler));

            await Assert.ThrowsAsync<HttpRequestException>(
                () => service.DownloadStringAsync("https://example.com/missing"));
        }

        [Fact]
        public async Task DownloadFileAsync_WritesFile()
        {
            var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4 })
            });
            var service = new DownloadService(new HttpClient(handler));

            string dir = Path.Combine(Path.GetTempPath(), "dmm_dl_" + Guid.NewGuid().ToString("N"));
            string dest = Path.Combine(dir, "nested", "file.bin");
            try
            {
                await service.DownloadFileAsync("https://example.com/file.bin", dest);

                Assert.True(File.Exists(dest));
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(dest));
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }
    }
}
