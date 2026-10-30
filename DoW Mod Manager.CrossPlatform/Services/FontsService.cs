using System.IO.Compression;

namespace DoW_Mod_Manager.Services
{
    /// <summary>Cross-platform port of the legacy <c>FontsManagerForm</c> file handling.</summary>
    public sealed class FontsService : IFontsService
    {
        public const string BackupFileName = "defaults.zip";

        /// <summary>The managed <c>*.fnt</c> files and what they are used for in game.</summary>
        private static readonly (string File, string Description)[] ManagedFonts =
        [
            ("albertus extra bold12.fnt", "Ingame units description"),
            ("albertus extra bold14.fnt", "Ingame objectives"),
            ("albertus extra bold16.fnt", "Ingame menu"),
            ("engravers old english mt30.fnt", "Some big font"),
            ("gillsans_11.fnt", "Map and ingame UI descriptions"),
            ("gillsans_11b.fnt", "Unknown"),
            ("gillsans_16.fnt", "Unknown"),
            ("gillsans_bold_16.fnt", "Some menu UI and map selection"),
            ("quorum medium bold13.fnt", "Splash screen, some menu UI, grey buttons"),
            ("quorum medium bold16.fnt", "Splash screen, menu items, buttons"),
        ];

        private static readonly string[] Widths =
            ["Default", "640", "800", "1024", "1280", "1366", "1600", "1920", "2560", "4096"];

        public IReadOnlyList<string> ScreenWidths => Widths;

        public string GetFontsDirectory(string installDir) => GamePaths.Fonts(installDir);

        public bool IsAvailable(string installDir) => Directory.Exists(GetFontsDirectory(installDir));

        /// <inheritdoc />
        public void EnsureBackup(string installDir)
        {
            string dir = GetFontsDirectory(installDir);
            string backup = Path.Combine(dir, BackupFileName);
            if (!Directory.Exists(dir) || File.Exists(backup))
                return;

            // Zip elsewhere first: zipping straight into the directory would archive the zip itself.
            string temp = Path.Combine(Path.GetTempPath(), $"dmm_fonts_{Guid.NewGuid():N}.zip");
            ZipFile.CreateFromDirectory(dir, temp);
            File.Move(temp, backup);
        }

        /// <inheritdoc />
        public IReadOnlyList<FontEntry> Read(string installDir, string screenWidth)
        {
            string dir = GetFontsDirectory(installDir);
            string sizeKey = SizeKey(screenWidth);
            var result = new List<FontEntry>(ManagedFonts.Length);

            foreach (var (file, description) in ManagedFonts)
            {
                string path = GamePaths.Resolve(dir, file);
                if (!File.Exists(path))
                {
                    result.Add(new FontEntry(file, description, false, "", false, 0));
                    continue;
                }

                string fontFile = "";
                int size = 0;
                foreach (string line in File.ReadLines(path))
                {
                    if (line.Contains("file"))
                    {
                        fontFile = QuotedValue(line) ?? fontFile;
                    }
                    else if (line.Contains(sizeKey))
                    {
                        string compact = line.Replace(" ", "");
                        int eq = compact.IndexOf('=');
                        int end = compact.IndexOf(';');
                        if (eq >= 0 && end > eq)
                            int.TryParse(compact[(eq + 1)..end], out size);
                        break;
                    }
                }

                // The game runs on a case-insensitive file system (Windows / Wine): "ANSNB___.TTF"
                // must match "ansnb___.ttf" on Linux too.
                bool found = fontFile.Length > 0 && File.Exists(GamePaths.Resolve(dir, fontFile));
                result.Add(new FontEntry(file, description, true, fontFile, found, size));
            }

            return result;
        }

        /// <inheritdoc />
        public void Write(string installDir, string screenWidth, IEnumerable<FontEntry> entries)
        {
            string dir = GetFontsDirectory(installDir);
            string sizeKey = SizeKey(screenWidth);

            foreach (FontEntry entry in entries)
            {
                string path = GamePaths.Resolve(dir, entry.DowFontFile);
                if (!entry.Exists || !File.Exists(path))
                    continue;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("file"))
                    {
                        string? current = QuotedValue(lines[i]);
                        if (current is not null && entry.FontFileName.Length > 0)
                            lines[i] = lines[i].Replace($"\"{current}\"", $"\"{entry.FontFileName}\"");
                    }
                    else if (lines[i].Contains(sizeKey))
                    {
                        int eq = lines[i].IndexOf('=');
                        int end = lines[i].IndexOf(';');
                        if (eq < 0 || end <= eq)
                            continue;

                        // Replace the *last* occurrence so "size24 = 24;" only changes the value.
                        string current = lines[i][(eq + 1)..end].Trim();
                        int at = lines[i].LastIndexOf(current, end, StringComparison.Ordinal);
                        if (at > eq)
                            lines[i] = lines[i].Remove(at, current.Length).Insert(at, entry.Size.ToString());
                    }
                }

                WindowsText.WriteAllLines(path, lines);
            }
        }

        /// <inheritdoc />
        public bool RestoreDefaults(string installDir)
        {
            string dir = GetFontsDirectory(installDir);
            string backup = Path.Combine(dir, BackupFileName);
            if (!File.Exists(backup))
                return false;

            using ZipArchive zip = ZipFile.OpenRead(backup);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (entry.Name.Length > 0)
                    entry.ExtractToFile(Path.Combine(dir, entry.Name), overwrite: true);
            }

            return true;
        }

        /// <inheritdoc />
        public string MapScreenWidth(string screenWidth) => screenWidth switch
        {
            // Discovered by the original author: the game picks a smaller UI width at these resolutions.
            "1366" => "1024",
            "1280" => "800",
            "1920" => "1280",
            _ when Widths.Contains(screenWidth) => screenWidth,
            _ => "Default",
        };

        private static string SizeKey(string screenWidth) => "size" + screenWidth;

        private static string? QuotedValue(string line)
        {
            int first = line.IndexOf('"');
            int last = line.LastIndexOf('"');
            return first >= 0 && last > first ? line[(first + 1)..last] : null;
        }
    }
}
