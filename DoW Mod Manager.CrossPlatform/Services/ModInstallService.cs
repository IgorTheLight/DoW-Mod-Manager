using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Services
{
    /// <inheritdoc />
    public sealed class ModInstallService : IModInstallService
    {
        public const string StoreFileName = "installed-mods.json";

        private static readonly string[] GenericWords = ["mod", "mods", "version", "dawn", "war", "for", "the"];

        private readonly IGameLauncher _launcher;
        private readonly bool _isWindows;
        private readonly InstalledModsStore _store;
        private readonly DownloadWatcher _watcher;
        private readonly TimeSpan _poll;

        public string? DownloadsFolder { get; }

        public ModInstallService(IGameLauncher launcher, IPlatformService platform)
            : this(launcher, platform.IsWindows,
                   new InstalledModsStore(Path.Combine(GamePaths.AppDataDirectory(), StoreFileName)),
                   Services.DownloadsFolder.Resolve(), TimeSpan.FromSeconds(1))
        {
        }

        internal ModInstallService(IGameLauncher launcher, bool isWindows, InstalledModsStore store,
            string? downloadsFolder, TimeSpan poll)
        {
            _launcher = launcher;
            _isWindows = isWindows;
            _store = store;
            DownloadsFolder = downloadsFolder;
            _poll = poll;
            _watcher = new DownloadWatcher(poll);
        }

        /// <summary>The mod's download and patch links, in that order.</summary>
        public static IReadOnlyList<string> Links(DownloadableMod mod) =>
            new[] { mod.DownloadLink, mod.PatchLink }.Where(l => l.Length > 0).ToList();

        public bool IsInstalled(GameInstall install, DownloadableMod mod) => FindModule(install, mod.ModuleFileName) is not null;

        public bool CanUninstall(GameInstall install, DownloadableMod mod) =>
            IsInstalled(install, mod)
            && (_store.Get(install.InstallDir, mod.ModuleFileName) is not null || FindUninstaller(install, mod) is not null);

        public IReadOnlyList<string> FindDownloadedFiles(DownloadableMod mod)
        {
            var links = Links(mod);
            if (DownloadsFolder is null || links.Count == 0)
                return [];

            var remembered = links
                .Select(l => _store.GetDownloadFileName(l) is { } name ? Path.Combine(DownloadsFolder, name) : null)
                .ToList();
            if (remembered.All(p => p is not null && File.Exists(p)))
                return remembered!;

            string token = Simplify(Token(mod));
            var matches = SafeFiles(DownloadsFolder)
                .Select(p => new FileInfo(p))
                .Where(f => (ModArchive.IsArchive(f.Name) || ModArchive.IsInstaller(f.Name))
                            && !IsUninstaller(f.Name) && Simplify(f.Name).Contains(token, StringComparison.Ordinal))
                .OrderBy(f => f.LastWriteTimeUtc)
                .Select(f => f.FullName)
                .ToList();
            return matches.Count >= links.Count ? matches.TakeLast(links.Count).ToList() : [];
        }

        public async Task<string> DownloadInBrowserAsync(string url, Func<string, Task> openUrl,
            IProgress<ModInstallProgress> progress, CancellationToken ct = default)
        {
            string folder = DownloadsFolder
                            ?? throw new InvalidOperationException("No downloads folder was found for your browser's downloads.");
            var existing = DownloadWatcher.Snapshot(folder);
            await openUrl(url);
            progress.Report(new ModInstallProgress($"Waiting for the download to start in your browser. Save it to {folder}."));

            string path = await _watcher.WaitForNewFileAsync(folder, existing,
                new InlineProgress<DownloadProgress>(p =>
                    progress.Report(new ModInstallProgress($"Downloading {p.FileName} in your browser: {FormatSize(p.Bytes)}"))),
                ct);
            _store.RememberDownload(url, Path.GetFileName(path));
            return path;
        }

        public async Task<ModInstallResult> InstallAsync(GameInstall install, DownloadableMod mod, IReadOnlyList<string> files,
            IProgress<ModInstallProgress> progress, CancellationToken ct = default)
        {
            if (files.Count == 0)
                return new ModInstallResult(false, "There is nothing to install.");

            var installers = files.Where(ModArchive.IsInstaller).ToList();
            var payloads = files
                .Where(f => ModArchive.IsArchive(f) && installers.Any(i => ModArchive.InstallerReferences(i, Path.GetFileName(f))))
                .ToList();
            foreach (var payload in payloads)
            {
                if (!installers.Any(i => SameDirectory(i, payload)))
                    return new ModInstallResult(false, $"Put {Path.GetFileName(payload)} in the same folder as its installer, then try again.");
            }

            var started = DateTime.UtcNow;
            var record = new InstalledModRecord
            {
                Name = mod.Name, Module = mod.ModuleFileName, InstallDir = install.InstallDir, InstalledUtc = started
            };

            foreach (var file in files.Except(payloads))
            {
                string name = Path.GetFileName(file);
                if (ModArchive.IsInstaller(file))
                {
                    progress.Report(new ModInstallProgress($"{name} is open: follow its steps.{GameFolderHint(install)}"));
                    var run = await _launcher.RunInGamePrefixAsync(install, file, ct);
                    if (!run.Started)
                        return new ModInstallResult(false, $"Could not run {name}: {run.Detail}");
                    await WaitForExitAsync(run.ProcessId, ct);
                }
                else if (ModArchive.IsArchive(file))
                {
                    try
                    {
                        var extracted = await ModArchive.ExtractAsync(file, install.InstallDir,
                            new InlineProgress<double>(f => progress.Report(new ModInstallProgress($"Extracting {name}…", f))), ct);
                        record.Files.AddRange(extracted.Created);
                    }
                    catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
                                                  or SharpCompress.Common.SharpCompressException)
                    {
                        SaveIfAny(record);
                        return new ModInstallResult(false, $"Could not extract {name}: {ex.Message}");
                    }
                }
                else
                {
                    return new ModInstallResult(false, $"{name} is neither an installer nor an archive (.zip, .7z, .rar).");
                }
            }

            // Some installers hand over to a second process: give the module a moment to appear.
            if (installers.Count > 0 && mod.ModuleFileName.Length > 0)
                await WaitUntilAsync(() => IsInstalled(install, mod), TimeSpan.FromSeconds(15),
                                     "Waiting for the installer to finish…", progress, ct);

            record.Uninstallers = SafeFiles(install.InstallDir)
                .Where(p => IsUninstaller(Path.GetFileName(p)) && File.GetLastWriteTimeUtc(p) >= started.AddSeconds(-2))
                .ToList();
            SaveIfAny(record);

            return mod.ModuleFileName.Length == 0 || IsInstalled(install, mod)
                ? new ModInstallResult(true, $"{mod.Name} is installed.")
                : new ModInstallResult(false, $"Finished, but {mod.ModuleFileName}.module is not in the game folder. "
                                              + "If the installer was cancelled or failed, try again.");
        }

        public async Task<ModInstallResult> UninstallAsync(GameInstall install, DownloadableMod mod,
            IProgress<ModInstallProgress> progress, CancellationToken ct = default)
        {
            var record = _store.Get(install.InstallDir, mod.ModuleFileName);
            string? uninstaller = record?.Uninstallers.FirstOrDefault(File.Exists) ?? FindUninstaller(install, mod);

            if (uninstaller is not null)
            {
                string name = Path.GetFileName(uninstaller);
                progress.Report(new ModInstallProgress($"{name} is open: follow its steps."));
                var run = await _launcher.RunInGamePrefixAsync(install, uninstaller, ct);
                if (!run.Started)
                    return new ModInstallResult(false, $"Could not run {name}: {run.Detail}");
                await WaitForExitAsync(run.ProcessId, ct);

                // NSIS uninstallers copy themselves to a temp folder and return at once on Windows;
                // under Proton the launcher already waits for every process of the prefix.
                await WaitUntilAsync(() => !IsInstalled(install, mod), _isWindows ? TimeSpan.FromMinutes(30) : TimeSpan.FromSeconds(15),
                                     "Waiting for the uninstaller to finish… (Cancel stops waiting)", progress, ct);
            }
            else if (record is { Files.Count: > 0 })
            {
                DeleteFiles(install.InstallDir, record.Files, progress, ct);
            }
            else
            {
                return new ModInstallResult(false,
                    $"The manager did not install {mod.Name} and found no uninstaller for it. Remove its files from the game folder by hand.");
            }

            if (mod.ModuleFileName.Length > 0 && IsInstalled(install, mod))
                return new ModInstallResult(false, $"{mod.ModuleFileName}.module is still in the game folder. If the uninstaller was cancelled, try again.");

            _store.Remove(install.InstallDir, mod.ModuleFileName);
            return new ModInstallResult(true, $"{mod.Name} is uninstalled.");
        }

        // ---- helpers --------------------------------------------------------------------------

        /// <summary>The <c>.module</c> file of <paramref name="module"/> in the folders the game loads mods from.</summary>
        public static string? FindModule(GameInstall install, string module)
        {
            if (string.IsNullOrWhiteSpace(module))
                return null;
            string fileName = module + ".module";

            var dirs = new List<string> { install.InstallDir };
            if (install.ModsDir is { } modsDir)
            {
                dirs.Add(modsDir);
                dirs.AddRange(SafeDirectories(modsDir));
            }

            return dirs
                .SelectMany(d => SafeFiles(d, "*.module"))
                .FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>An uninstaller in the game folder whose name contains the mod's main word.</summary>
        public static string? FindUninstaller(GameInstall install, DownloadableMod mod)
        {
            string token = Simplify(Token(mod));
            return SafeFiles(install.InstallDir)
                .Where(p => IsUninstaller(Path.GetFileName(p)) && Simplify(Path.GetFileName(p)).Contains(token, StringComparison.Ordinal))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        /// <summary>
        /// The word that names the mod in its file names: the first significant word of the module
        /// name, else of the display name ("Unification_New_Races" → "Unification").
        /// </summary>
        public static string Token(DownloadableMod mod)
        {
            foreach (var source in new[] { mod.ModuleFileName, mod.Name })
            {
                foreach (var word in source.Split([' ', '_', '-', '.', ':', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries))
                {
                    if (word.Length >= 4 && !word.All(char.IsDigit) && !GenericWords.Contains(word.ToLowerInvariant()))
                        return word;
                }
            }
            return mod.Name;
        }

        private static bool IsUninstaller(string fileName) =>
            ModArchive.IsInstaller(fileName) && fileName.Contains("uninst", StringComparison.OrdinalIgnoreCase);

        private static string Simplify(string s) => new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        private string GameFolderHint(GameInstall install) =>
            _isWindows
                ? $" Game folder: {install.InstallDir}"
                : $" Game folder: Z:{install.InstallDir.Replace('/', '\\')}";

        private void SaveIfAny(InstalledModRecord record)
        {
            if (record.Files.Count > 0 || record.Uninstallers.Count > 0)
                _store.Save(record);
        }

        private static void DeleteFiles(string installDir, IReadOnlyList<string> files, IProgress<ModInstallProgress> progress, CancellationToken ct)
        {
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installDir));
            var dirs = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                string file = Path.GetFullPath(files[i]);
                if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    continue;
                try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                dirs.Add(Path.GetDirectoryName(file)!);
                progress.Report(new ModInstallProgress("Removing files…", (double)(i + 1) / files.Count));
            }

            // Remove the folders the mod emptied, deepest first, never the game folder itself.
            foreach (var dir in dirs.OrderByDescending(d => d.Length))
            {
                for (var d = dir; d.Length > root.Length && d.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
                     d = Path.GetDirectoryName(d)!)
                {
                    try
                    {
                        if (!Directory.Exists(d) || Directory.EnumerateFileSystemEntries(d).Any())
                            break;
                        Directory.Delete(d);
                    }
                    catch (IOException) { break; }
                    catch (UnauthorizedAccessException) { break; }
                }
            }
        }

        /// <summary>Polls until process <paramref name="pid"/> is gone (it may not be our child any more).</summary>
        private async Task WaitForExitAsync(int pid, CancellationToken ct)
        {
            while (pid > 0)
            {
                try
                {
                    using var process = Process.GetProcessById(pid);
                    if (process.HasExited)
                        return;
                }
                catch (ArgumentException) { return; }
                catch (InvalidOperationException) { return; }
                await Task.Delay(_poll, ct);
            }
        }

        private async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string text,
            IProgress<ModInstallProgress> progress, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow + timeout;
            if (condition())
                return;
            progress.Report(new ModInstallProgress(text));
            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(_poll, ct);
        }

        private static bool SameDirectory(string a, string b) =>
            string.Equals(Path.GetDirectoryName(Path.GetFullPath(a)), Path.GetDirectoryName(Path.GetFullPath(b)), StringComparison.Ordinal);

        public static string FormatSize(long bytes) => bytes switch
        {
            >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
            >= 1L << 20 => $"{bytes / (double)(1L << 20):0} MB",
            _ => $"{bytes / 1024.0:0} KB"
        };

        private static IEnumerable<string> SafeFiles(string dir, string pattern = "*")
        {
            try { return Directory.Exists(dir) ? Directory.EnumerateFiles(dir, pattern).ToList() : []; }
            catch (IOException) { return []; }
            catch (UnauthorizedAccessException) { return []; }
        }

        private static IEnumerable<string> SafeDirectories(string dir)
        {
            try { return Directory.Exists(dir) ? Directory.EnumerateDirectories(dir).ToList() : []; }
            catch (IOException) { return []; }
            catch (UnauthorizedAccessException) { return []; }
        }

        /// <summary>Reports on the calling thread (unlike <see cref="Progress{T}"/>, which posts).</summary>
        private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
        {
            public void Report(T value) => report(value);
        }
    }
}
