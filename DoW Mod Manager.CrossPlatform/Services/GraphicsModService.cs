namespace DoW_Mod_Manager.Services
{
    /// <inheritdoc cref="IGraphicsModService" />
    public sealed class GraphicsModService : IGraphicsModService
    {
        private const string RepoUrl =
            "https://raw.githubusercontent.com/IgorTheLight/DoW-Mod-Manager/master/DoW%20Mod%20Manager/";

        private sealed record WrapperFiles(string Url, string VersionFile, string[] Files);

        // Same file names (and casing) as the legacy downloads: the version file comes first.
        private static readonly WrapperFiles Dxvk =
            new(RepoUrl + "DXVK/", "dxvk.version", ["dxvk.version", "dxvk.conf", "d3d9.dll", "d3d8.dll"]);

        private static readonly WrapperFiles DgVoodoo2 =
            new(RepoUrl + "dgVoodoo2/", "dgVoodoo.version", ["dgVoodoo.version", "dgVoodoo.conf", "D3D9.dll", "dgVoodooCpl.exe"]);

        /// <summary>The Direct3D DLLs a wrapper may place next to the game executable.</summary>
        private static readonly string[] WrapperDlls = ["d3d9.dll", "d3d8.dll"];

        private readonly IDownloadService _download;

        public GraphicsModService(IDownloadService download)
        {
            _download = download ?? throw new ArgumentNullException(nameof(download));
        }

        public bool IsInstalled(string installDir, GraphicsWrapper wrapper) =>
            FilesOf(wrapper).Files.All(f => File.Exists(GamePaths.Resolve(installDir, f)));

        public async Task<GraphicsWrapperState> GetStateAsync(string installDir, GraphicsWrapper wrapper, bool checkForUpdate, CancellationToken ct = default)
        {
            if (!IsInstalled(installDir, wrapper))
                return GraphicsWrapperState.NotInstalled;
            if (!checkForUpdate)
                return GraphicsWrapperState.Installed;

            WrapperFiles files = FilesOf(wrapper);
            try
            {
                string latest = await _download.DownloadStringAsync(files.Url + files.VersionFile, ct).ConfigureAwait(false);
                string current = File.ReadAllText(GamePaths.Resolve(installDir, files.VersionFile));

                return Version.TryParse(latest.Trim(), out Version? latestVersion)
                    && Version.TryParse(current.Trim(), out Version? currentVersion)
                    && currentVersion < latestVersion
                    ? GraphicsWrapperState.UpdateAvailable
                    : GraphicsWrapperState.Installed;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
            {
                return GraphicsWrapperState.Installed;
            }
        }

        public async Task InstallAsync(string installDir, GraphicsWrapper wrapper, CancellationToken ct = default)
        {
            WrapperFiles files = FilesOf(wrapper);

            // Download everything first so a failed download leaves the current setup untouched.
            string staging = Path.Combine(installDir, ".dowmm-" + wrapper.ToString().ToLowerInvariant());
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            try
            {
                foreach (string file in files.Files)
                    await _download.DownloadFileAsync(files.Url + file, Path.Combine(staging, file), ct).ConfigureAwait(false);

                Remove(installDir, Other(wrapper));
                foreach (string file in files.Files)
                {
                    // On a case-sensitive file system "D3D9.dll" and "d3d9.dll" are different files,
                    // while Wine sees one: drop whatever casing is there before moving ours in.
                    DeleteAnyCasing(installDir, file);
                    File.Move(Path.Combine(staging, file), Path.Combine(installDir, file));
                }
            }
            finally
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
        }

        public void Remove(string installDir, GraphicsWrapper wrapper)
        {
            foreach (string file in FilesOf(wrapper).Files)
                DeleteAnyCasing(installDir, file);
        }

        /// <summary>
        /// The <c>WINEDLLOVERRIDES</c> value that makes Wine/Proton load the Direct3D DLLs found next to
        /// the game executable (native first, builtin as fallback), e.g. <c>d3d9,d3d8=n,b</c>. Null
        /// when the game folder has none.
        /// </summary>
        public static string? WineDllOverrides(string installDir)
        {
            var dlls = WrapperDlls
                .Where(dll => File.Exists(GamePaths.Resolve(installDir, dll)))
                .Select(Path.GetFileNameWithoutExtension)
                .ToList();
            return dlls.Count == 0 ? null : string.Join(',', dlls) + "=n,b";
        }

        private static WrapperFiles FilesOf(GraphicsWrapper wrapper) =>
            wrapper == GraphicsWrapper.Dxvk ? Dxvk : DgVoodoo2;

        private static GraphicsWrapper Other(GraphicsWrapper wrapper) =>
            wrapper == GraphicsWrapper.Dxvk ? GraphicsWrapper.DgVoodoo2 : GraphicsWrapper.Dxvk;

        private static void DeleteAnyCasing(string dir, string fileName)
        {
            if (!Directory.Exists(dir))
                return;
            foreach (string path in Directory.EnumerateFiles(dir))
            {
                if (string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
                    File.Delete(path);
            }
        }
    }
}
