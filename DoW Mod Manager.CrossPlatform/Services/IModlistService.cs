using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Services
{
    /// <summary>A mod listed in the curated "Download Mods" list (legacy <c>ModDownloaderForm.Mod</c>).</summary>
    public sealed record DownloadableMod(
        string Name,
        string ModuleFileName,
        string SiteLink,
        string DownloadLink,
        string PatchLink)
    {
        public override string ToString() => Name;
    }

    /// <summary>
    /// Provides the curated mod list used by the Mod Downloader. The list is cached in the user's
    /// data directory and refreshed from GitHub when a newer version is published (legacy
    /// <c>DownloadHelper.CheckForModlistUpdate</c> / <c>DownloadModlist</c>).
    /// </summary>
    public interface IModlistService
    {
        /// <summary>Local cache file of the mod list.</summary>
        string ModlistPath { get; }

        /// <summary>
        /// Returns the mods for <paramref name="variant"/>, downloading the list when it is not cached
        /// yet and refreshing it when a newer version exists. Network failures fall back to the cache.
        /// </summary>
        Task<IReadOnlyList<DownloadableMod>> LoadAsync(GameVariant variant, CancellationToken ct = default);

        /// <summary>Parses the mod-list text and returns the section matching <paramref name="variant"/>.</summary>
        IReadOnlyList<DownloadableMod> Parse(string content, GameVariant variant);
    }
}
