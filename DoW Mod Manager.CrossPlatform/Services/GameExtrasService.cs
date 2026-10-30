namespace DoW_Mod_Manager.Services
{
    /// <inheritdoc cref="IGameExtrasService" />
    public sealed class GameExtrasService : IGameExtrasService
    {
        private const string RepoUrl =
            "https://raw.githubusercontent.com/IgorTheLight/DoW-Mod-Manager/master/DoW%20Mod%20Manager/";

        private const string CameraUrl = RepoUrl + "CAMERA/";
        private const string FontUrl = RepoUrl + "FONT/";

        private static readonly string[] CameraFiles = ["camera_high.lua", "camera_low.lua"];

        private static readonly string[] ArtFiles =
            ["font_glyphs.gfx", "fontaux.gfx", "fontbody.gfx", "fontdecor.gfx", "fonthead.gfx"];

        private static readonly string[] FontFiles =
        [
            "albertus extra bold12.fnt", "albertus extra bold14.fnt", "albertus extra bold16.fnt",
            "ansnb___.ttf", "engo.ttf", "engravers old english mt30.fnt", "gil_____.ttf",
            "gillsans_11.fnt", "gillsans_11b.fnt", "gillsans_16.fnt", "gillsans_bold_16.fnt",
            "quorum medium bold13.fnt", "quorum medium bold16.fnt",
        ];

        private readonly IDownloadService _download;

        public GameExtrasService(IDownloadService download)
        {
            _download = download ?? throw new ArgumentNullException(nameof(download));
        }

        public bool IsCameraInstalled(string installDir)
        {
            string dir = GamePaths.EngineData(installDir);
            return CameraFiles.All(f => File.Exists(GamePaths.Resolve(dir, f)));
        }

        public async Task InstallCameraAsync(string installDir, CancellationToken ct = default)
        {
            string dir = GamePaths.EngineData(installDir);
            foreach (string file in CameraFiles)
                await _download.DownloadFileAsync(CameraUrl + file, Path.Combine(dir, file), ct).ConfigureAwait(false);
        }

        public void RemoveCamera(string installDir)
        {
            string dir = GamePaths.EngineData(installDir);
            foreach (string file in CameraFiles)
                File.Delete(GamePaths.Resolve(dir, file));
        }

        public bool IsLargerFontsInstalled(string installDir)
        {
            string root = GamePaths.EnglishLocaleData(installDir);
            return Directory.Exists(GamePaths.Resolve(root, "art")) && Directory.Exists(GamePaths.Resolve(root, "font"));
        }

        public async Task InstallLargerFontsAsync(string installDir, CancellationToken ct = default)
        {
            string root = GamePaths.EnglishLocaleData(installDir);
            string artDir = GamePaths.Resolve(root, "art", "ui", "swf");
            string fontDir = GamePaths.Resolve(root, "font");

            foreach (string file in ArtFiles)
                await Download(FontUrl + "art/ui/swf/" + file, Path.Combine(artDir, file)).ConfigureAwait(false);
            foreach (string file in FontFiles)
                await Download(FontUrl + "font/" + file, Path.Combine(fontDir, file)).ConfigureAwait(false);

            Task Download(string url, string dest) =>
                _download.DownloadFileAsync(url.Replace(" ", "%20"), dest, ct);
        }

        public void RemoveLargerFonts(string installDir)
        {
            string root = GamePaths.EnglishLocaleData(installDir);
            foreach (string sub in new[] { "art", "font" })
            {
                string dir = GamePaths.Resolve(root, sub);
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }
    }
}
