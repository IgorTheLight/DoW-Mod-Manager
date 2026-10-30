namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Cross-platform downloads. Replaces the <c>WebClient</c> bits of the legacy
    /// <c>DownloadHelper</c>. The Windows shortcut / restart logic is intentionally not
    /// part of this service (that belongs to the platform / file-system services).
    /// </summary>
    public interface IDownloadService
    {
        /// <summary>
        /// Downloads the resource at <paramref name="url"/> as a string.
        /// </summary>
        Task<string> DownloadStringAsync(string url, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads the resource at <paramref name="url"/> to <paramref name="destPath"/>.
        /// </summary>
        Task DownloadFileAsync(string url, string destPath, CancellationToken cancellationToken = default);
    }
}
