using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DoW_Mod_Manager.Services
{
    /// <summary>Bytes a browser has written so far for <see cref="FileName"/>.</summary>
    public sealed record DownloadProgress(string FileName, long Bytes);

    /// <summary>
    /// Waits for a browser download to land in a folder. ModDB serves files only to real browsers
    /// (Cloudflare check), so the manager opens the link in the browser and watches the folder:
    /// in-progress files (<c>.part</c>, <c>.crdownload</c>...) report their size, and the download
    /// is done when a new complete file has kept the same size for one poll.
    /// </summary>
    public sealed class DownloadWatcher
    {
        /// <summary>Suffixes browsers give a file while they are still writing it.</summary>
        public static readonly string[] PartialSuffixes = [".part", ".crdownload", ".download", ".opdownload", ".partial"];

        private readonly TimeSpan _pollInterval;

        public DownloadWatcher(TimeSpan? pollInterval = null)
        {
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
        }

        /// <summary>Names of the files in <paramref name="folder"/> now: the ones to ignore later.</summary>
        public static HashSet<string> Snapshot(string folder) =>
            new(SafeFiles(folder).Select(Path.GetFileName)!, StringComparer.Ordinal);

        public static bool IsPartial(string fileName) =>
            PartialSuffixes.Any(s => fileName.EndsWith(s, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Completes with the path of the first file that appears in <paramref name="folder"/>, is not
        /// in <paramref name="existing"/>, is not partial, has no partial sibling and keeps its size.
        /// </summary>
        public async Task<string> WaitForNewFileAsync(string folder, IReadOnlySet<string> existing,
            IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
        {
            var lastSizes = new Dictionary<string, long>(StringComparer.Ordinal);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var files = SafeFiles(folder)
                    .Select(p => new FileInfo(p))
                    .Where(f => !f.Name.StartsWith('.') && !existing.Contains(f.Name))
                    .ToList();

                var partials = files.Where(f => IsPartial(f.Name)).ToList();
                if (partials.OrderByDescending(SafeLength).FirstOrDefault() is { } inProgress)
                    progress?.Report(new DownloadProgress(StripPartialSuffix(inProgress.Name), SafeLength(inProgress)));

                foreach (var file in files.Where(f => !IsPartial(f.Name)))
                {
                    long size = SafeLength(file);
                    bool stillWriting = partials.Any(p => p.Name.StartsWith(file.Name, StringComparison.Ordinal));
                    if (size <= 0 || stillWriting)
                    {
                        lastSizes.Remove(file.Name);
                        continue;
                    }
                    if (lastSizes.TryGetValue(file.Name, out long previous) && previous == size)
                    {
                        progress?.Report(new DownloadProgress(file.Name, size));
                        return file.FullName;
                    }
                    lastSizes[file.Name] = size;
                }

                await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
            }
        }

        private static string StripPartialSuffix(string name)
        {
            foreach (var s in PartialSuffixes)
            {
                if (name.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                    return name[..^s.Length];
            }
            return name;
        }

        private static long SafeLength(FileInfo f)
        {
            try { f.Refresh(); return f.Exists ? f.Length : 0; }
            catch (IOException) { return 0; }
        }

        private static IEnumerable<string> SafeFiles(string folder)
        {
            try { return Directory.EnumerateFiles(folder).ToList(); }
            catch (IOException) { return []; }
            catch (UnauthorizedAccessException) { return []; }
        }
    }
}
