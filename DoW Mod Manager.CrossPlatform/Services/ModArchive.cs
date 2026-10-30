using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace DoW_Mod_Manager.Services
{
    /// <summary>Files an extraction created, and existing files it replaced (kept on uninstall).</summary>
    public sealed record ExtractResult(IReadOnlyList<string> Created, IReadOnlyList<string> Overwritten);

    /// <summary>
    /// Mod archives (.zip, .7z, .rar) and installers (.exe). An archive is laid out like the game
    /// folder from its "content root": the shallowest folder holding a <c>.module</c> file, which
    /// is extracted into the game folder.
    /// </summary>
    public static class ModArchive
    {
        private static readonly string[] ArchiveExtensions = [".zip", ".7z", ".rar"];

        public static bool IsArchive(string path) =>
            ArchiveExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        public static bool IsInstaller(string path) =>
            string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The content root of an archive with these entry keys ("" for the top), or null when no
        /// entry is a <c>.module</c> file.
        /// </summary>
        public static string? FindContentRoot(IEnumerable<string> keys)
        {
            var module = keys
                .Select(Normalize)
                .Where(k => k.EndsWith(".module", StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k.Count(c => c == '/'))
                .FirstOrDefault();
            if (module is null)
                return null;
            int slash = module.LastIndexOf('/');
            return slash < 0 ? "" : module[..(slash + 1)];
        }

        /// <summary>
        /// True when <paramref name="installerPath"/> names <paramref name="fileName"/> (ASCII or
        /// UTF-16): the installer unpacks that archive itself (e.g. Unification's installer and
        /// <c>Unification.7z</c>), so the archive must stay next to it and not be extracted.
        /// </summary>
        public static bool InstallerReferences(string installerPath, string fileName)
        {
            byte[] data;
            try { data = File.ReadAllBytes(installerPath); }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }

            return Contains(data, Encoding.ASCII.GetBytes(fileName)) || Contains(data, Encoding.Unicode.GetBytes(fileName));
        }

        /// <summary>
        /// Extracts the content root of <paramref name="archivePath"/> into <paramref name="targetDir"/>,
        /// reusing the on-disk casing of existing folders. <paramref name="progress"/> gets 0..1.
        /// </summary>
        public static Task<ExtractResult> ExtractAsync(string archivePath, string targetDir,
            IProgress<double>? progress = null, CancellationToken ct = default) =>
            Task.Run(() => Extract(archivePath, targetDir, progress, ct), ct);

        private static ExtractResult Extract(string archivePath, string targetDir, IProgress<double>? progress, CancellationToken ct)
        {
            using var archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions());
            var files = archive.Entries.Where(e => !e.IsDirectory && e.Key is not null).ToList();
            string root = FindContentRoot(files.Select(e => e.Key!))
                          ?? throw new InvalidDataException($"{Path.GetFileName(archivePath)} contains no .module file, so the manager cannot tell where it goes.");
            long total = Math.Max(1, files.Where(e => Normalize(e.Key!).StartsWith(root, StringComparison.Ordinal)).Sum(e => e.Size));

            string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDir));
            var created = new List<string>();
            var overwritten = new List<string>();
            long done = 0;
            var buffer = new byte[1 << 20];

            void Write(string rawKey, Func<Stream> open)
            {
                ct.ThrowIfCancellationRequested();
                string key = Normalize(rawKey);
                if (!key.StartsWith(root, StringComparison.Ordinal))
                    return;

                string dest = Path.GetFullPath(GamePaths.Resolve(target, key[root.Length..]));
                if (!dest.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new InvalidDataException($"{Path.GetFileName(archivePath)}: entry \"{rawKey}\" points outside the game folder.");

                (File.Exists(dest) ? overwritten : created).Add(dest);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                using var source = open();
                using var output = File.Create(dest);
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, read);
                    done += read;
                    progress?.Report(Math.Min(1.0, (double)done / total));
                }
            }

            // Solid archives and 7z must be read in order; zip and plain rar entries one by one.
            if (archive.IsSolid || archive.Type == SharpCompress.Common.ArchiveType.SevenZip)
            {
                using var reader = archive.ExtractAllEntries();
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory && reader.Entry.Key is { } key)
                        Write(key, reader.OpenEntryStream);
                }
            }
            else
            {
                foreach (var entry in files)
                    Write(entry.Key!, entry.OpenEntryStream);
            }

            progress?.Report(1.0);
            return new ExtractResult(created, overwritten);
        }

        private static string Normalize(string key) => key.Replace('\\', '/').TrimStart('/');

        private static bool Contains(byte[] haystack, byte[] needle) =>
            needle.Length > 0 && haystack.AsSpan().IndexOf(needle) >= 0;
    }
}
