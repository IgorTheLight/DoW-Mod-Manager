using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Services
{
    /// <summary>What an install or uninstall is doing; <see cref="Fraction"/> (0..1) is null when unknown.</summary>
    public sealed record ModInstallProgress(string Text, double? Fraction = null);

    public sealed record ModInstallResult(bool Success, string Message);

    /// <summary>
    /// One-click install and uninstall of the mods of the curated list: download through the
    /// browser (ModDB only serves browsers), then run the mod's installer in the game's prefix or
    /// extract its archive into the game folder, and keep a record to undo it.
    /// </summary>
    public interface IModInstallService
    {
        /// <summary>The browser's downloads folder, or null when none was found.</summary>
        string? DownloadsFolder { get; }

        /// <summary>True when the mod's <c>.module</c> file is in the game folder (or the DE Mods folder).</summary>
        bool IsInstalled(GameInstall install, DownloadableMod mod);

        /// <summary>True when the mod is installed and the manager knows how to remove it.</summary>
        bool CanUninstall(GameInstall install, DownloadableMod mod);

        /// <summary>
        /// Earlier downloads of all the mod's files in <see cref="DownloadsFolder"/>, oldest first,
        /// or an empty list when not all of them are there.
        /// </summary>
        IReadOnlyList<string> FindDownloadedFiles(DownloadableMod mod);

        /// <summary>Opens <paramref name="url"/> with <paramref name="openUrl"/> and waits for the browser's download.</summary>
        Task<string> DownloadInBrowserAsync(string url, Func<string, Task> openUrl,
            IProgress<ModInstallProgress> progress, CancellationToken ct = default);

        Task<ModInstallResult> InstallAsync(GameInstall install, DownloadableMod mod, IReadOnlyList<string> files,
            IProgress<ModInstallProgress> progress, CancellationToken ct = default);

        Task<ModInstallResult> UninstallAsync(GameInstall install, DownloadableMod mod,
            IProgress<ModInstallProgress> progress, CancellationToken ct = default);
    }
}
