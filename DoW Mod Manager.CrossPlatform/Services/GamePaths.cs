using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Resolves paths inside a game install. The legacy code used Windows paths with inconsistent
    /// casing (<c>Engine\Locale\English\data\font</c> vs <c>...\Data</c>); on a case-sensitive Linux
    /// file system each segment is matched against what actually exists on disk.
    /// </summary>
    public static class GamePaths
    {
        /// <summary>
        /// Combines <paramref name="segments"/> onto <paramref name="root"/>, reusing the on-disk
        /// casing of every segment that already exists. Missing segments keep the given casing.
        /// Segments may hold several components separated by <c>\</c> or <c>/</c> (values read
        /// from <c>.module</c> files use Windows separators).
        /// </summary>
        public static string Resolve(string root, params string[] segments)
        {
            string current = root;
            bool exists = Directory.Exists(current);

            foreach (string segment in segments)
            {
                foreach (string part in segment.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
                {
                    string exact = Path.Combine(current, part);
                    string? match = null;

                    // Fast path (and the only path on Windows): the given casing already exists.
                    if (exists && (File.Exists(exact) || Directory.Exists(exact)))
                    {
                        match = part;
                    }
                    else if (exists)
                    {
                        match = Directory.EnumerateFileSystemEntries(current)
                            .Select(Path.GetFileName)
                            .FirstOrDefault(n => string.Equals(n, part, StringComparison.OrdinalIgnoreCase));
                    }

                    current = Path.Combine(current, match ?? part);
                    exists = match is not null && Directory.Exists(current);
                }
            }

            return current;
        }

        private static readonly char[] Separators = ['\\', '/'];

        /// <summary><c>Local.ini</c>: the game's own video/audio/profile settings.</summary>
        public static string LocalIni(string installDir) => Resolve(installDir, "Local.ini");

        /// <summary><c>Drivers/spdx9_config.txt</c>: holds the <c>allowhwcursor</c> switch.</summary>
        public static string DriverConfig(string installDir) => Resolve(installDir, "Drivers", "spdx9_config.txt");

        /// <summary><c>Profiles/</c>: one <c>ProfileN</c> directory per player.</summary>
        public static string Profiles(string installDir) => Resolve(installDir, "Profiles");

        /// <summary><c>Engine/Data</c>: where the "better camera" LUA files go.</summary>
        public static string EngineData(string installDir) => Resolve(installDir, "Engine", "Data");

        /// <summary><c>Engine/Locale/English/Data</c>: root of the "larger fonts" mod.</summary>
        public static string EnglishLocaleData(string installDir) => Resolve(installDir, "Engine", "Locale", "English", "Data");

        /// <summary><c>Engine/Locale/English/Data/font</c>: the *.fnt files managed by the Fonts Manager.</summary>
        public static string Fonts(string installDir) => Resolve(installDir, "Engine", "Locale", "English", "Data", "font");

        /// <summary>
        /// Directories that may hold mod <c>.module</c> files, in lookup order: the install dir, then
        /// (Definitive Edition) the Mods dir itself and each of its subfolders, since mods are
        /// installed both as <c>Mods/X.module</c> and <c>Mods/X/X.module</c>, then the optional
        /// Soulstorm install whose mods the Definitive Edition may also use.
        /// </summary>
        public static IReadOnlyList<string> ModuleDirectories(string installDir, string? modsDir, string? soulstormDir = null)
        {
            var dirs = new List<string> { installDir };
            if (!string.IsNullOrEmpty(modsDir) && Directory.Exists(modsDir))
            {
                dirs.Add(modsDir);
                try
                {
                    dirs.AddRange(Directory.GetDirectories(modsDir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            if (!string.IsNullOrEmpty(soulstormDir) && Directory.Exists(soulstormDir))
                dirs.Add(soulstormDir);
            return dirs;
        }

        /// <inheritdoc cref="ModuleDirectories(string, string?, string?)"/>
        public static IReadOnlyList<string> ModuleDirectories(GameInstall install) =>
            ModuleDirectories(install.InstallDir, install.ModsDir, install.SoulstormModsDir);

        /// <summary>The <c>*.module</c> files (any extension casing) directly inside <paramref name="directory"/>.</summary>
        public static IReadOnlyList<string> ModuleFiles(string directory)
        {
            if (!Directory.Exists(directory))
                return [];
            try
            {
                return Directory.GetFiles(directory, "*.module", ModuleEnumeration)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (IOException) { return []; }
            catch (UnauthorizedAccessException) { return []; }
        }

        private static readonly EnumerationOptions ModuleEnumeration = new()
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            IgnoreInaccessible = true,
        };

        /// <summary>Per-user data directory of the manager (modlist cache…), created on demand.</summary>
        public static string AppDataDirectory()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
                "DowModManager");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
