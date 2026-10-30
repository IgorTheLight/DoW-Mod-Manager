namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// <see cref="HttpClient"/>-based, cross-platform implementation of
    /// <see cref="IDownloadService"/>.
    /// </summary>
    public sealed class DownloadService : IDownloadService
    {
        private readonly HttpClient _httpClient;

        public DownloadService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <inheritdoc />
        public async Task<string> DownloadStringAsync(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL must be provided.", nameof(url));

            using HttpResponseMessage response =
                await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DownloadFileAsync(string url, string destPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL must be provided.", nameof(url));
            if (string.IsNullOrEmpty(destPath))
                throw new ArgumentException("Destination path must be provided.", nameof(destPath));

            string? directory = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using HttpResponseMessage response = await _httpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using Stream source =
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using FileStream destination = File.Create(destPath);
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }
    }
}
