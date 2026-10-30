using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Port of the mod-list reading of <c>ModDownloaderForm.ReadModsFromFile</c> and of the mod-list
    /// update check in <c>DownloadHelper</c>, on top of <see cref="IDownloadService"/>.
    /// </summary>
    /// <remarks>
    /// File format: the first line is the list version (e.g. <c>209</c>), then one section per game
    /// (<c>[SS]</c>, <c>[DC]</c>, <c>[WA]</c>, <c>[OR]</c>, terminated by <c>[END]</c>). Each mod is
    /// five lines — name, module file, site, download link, patch link (<c>-</c> for none) — followed
    /// by a blank line.
    /// </remarks>
    public sealed class ModlistService : IModlistService
    {
        public const string ModlistFileName = "DoW Mod Manager Download Mods.list";

        private const string BaseUrl =
            "https://raw.githubusercontent.com/IgorTheLight/DoW-Mod-Manager/master/DoW%20Mod%20Manager/ModList/";

        public const string ModlistUrl = BaseUrl + "DoW%20Mod%20Manager%20Download%20Mods.list";
        public const string VersionUrl = BaseUrl + "version";

        private readonly IDownloadService _download;

        public string ModlistPath { get; }

        public ModlistService(IDownloadService download)
            : this(download, GamePaths.AppDataDirectory())
        {
        }

        public ModlistService(IDownloadService download, string cacheDirectory)
        {
            _download = download ?? throw new ArgumentNullException(nameof(download));
            ModlistPath = Path.Combine(cacheDirectory, ModlistFileName);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DownloadableMod>> LoadAsync(GameVariant variant, CancellationToken ct = default)
        {
            if (!File.Exists(ModlistPath))
            {
                await _download.DownloadFileAsync(ModlistUrl, ModlistPath, ct).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    string remote = await _download.DownloadStringAsync(VersionUrl, ct).ConfigureAwait(false);
                    string local = File.ReadLines(ModlistPath).FirstOrDefault() ?? "";
                    if (ParseVersion(remote) > ParseVersion(local))
                        await _download.DownloadFileAsync(ModlistUrl, ModlistPath, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
                {
                    // Offline or GitHub unreachable: the cached list is good enough.
                }
            }

            return Parse(await File.ReadAllTextAsync(ModlistPath, ct).ConfigureAwait(false), variant);
        }

        /// <inheritdoc />
        public IReadOnlyList<DownloadableMod> Parse(string content, GameVariant variant)
        {
            string tag = variant switch
            {
                // The mod list files Definitive Edition releases under the Soulstorm section.
                GameVariant.Soulstorm or GameVariant.DefinitiveEdition => "[SS]",
                GameVariant.DarkCrusade => "[DC]",
                GameVariant.WinterAssault => "[WA]",
                _ => "[OR]",
            };

            string[] lines = content.Replace("\r\n", "\n").Split('\n');
            var result = new List<DownloadableMod>();

            int i = Array.FindIndex(lines, l => l.TrimEnd().StartsWith(tag, StringComparison.Ordinal));
            if (i < 0)
                return result;

            for (i++; i < lines.Length && !lines[i].StartsWith('['); )
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    i++;
                    continue;
                }

                var props = new string[5];
                for (int p = 0; p < 5; p++, i++)
                {
                    string value = i < lines.Length ? lines[i].TrimEnd() : "";
                    props[p] = value.StartsWith('-') ? "" : value;
                }

                result.Add(new DownloadableMod(props[0], props[1], props[2], props[3], props[4]));
            }

            return result;
        }

        /// <summary>The legacy compared versions as integers with the dots stripped ("2.0.9" → 209).</summary>
        public static int ParseVersion(string version)
        {
            string digits = version.Trim().Replace(".", "");
            return int.TryParse(digits, out int value) ? value : 0;
        }
    }
}
