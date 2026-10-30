using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Cross-platform port of the mod-enumeration and dependency-resolution logic from
    /// the legacy <c>ModManagerForm</c> (<c>GetMods()</c>, <c>LoadModFoldersFromFile()</c>,
    /// <c>CheckforInstalledMods()</c>).
    /// </summary>
    public sealed class ModService : IModService
    {
        private readonly IModuleParser _parser;

        public ModService(IModuleParser parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        /// <inheritdoc />
        public IReadOnlyList<ModInfo> GetPlayableMods(string installDir, bool isOldGame)
        {
            if (string.IsNullOrEmpty(installDir))
                throw new ArgumentException("Install directory must be provided.", nameof(installDir));

            return GetPlayableMods(GamePaths.ModuleDirectories(installDir, modsDir: null), isOldGame);
        }

        /// <inheritdoc />
        public IReadOnlyList<ModInfo> GetPlayableMods(GameInstall install)
        {
            ArgumentNullException.ThrowIfNull(install);
            if (string.IsNullOrEmpty(install.InstallDir))
                throw new ArgumentException("Install directory must be provided.", nameof(install));

            return GetPlayableMods(GamePaths.ModuleDirectories(install), install.IsOldGame);
        }

        private List<ModInfo> GetPlayableMods(IReadOnlyList<string> moduleDirectories, bool isOldGame)
        {
            var result = new List<ModInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string directory in moduleDirectories)
            {
                foreach (string filePath in GamePaths.ModuleFiles(directory))
                {
                    if (!File.Exists(filePath))
                        continue;

                    string fileName = Path.GetFileNameWithoutExtension(filePath);

                    // There is no point adding base modules (W40k, ..., DoWDE) to the list. A mod
                    // present in several places is listed once: the first directory wins.
                    if (BaseModules.Contains(fileName) || seen.Contains(fileName))
                        continue;

                    ModInfo mod = _parser.Parse(filePath, isOldGame);

                    // Filter the unplayable mods and populate the list only with playable mods.
                    if (mod.IsPlayable)
                    {
                        result.Add(mod);
                        seen.Add(fileName);
                    }
                }
            }

            return result;
        }

        /// <inheritdoc />
        public IReadOnlyList<ModDependencyStatus> ResolveDependencies(string installDir, ModInfo mod)
        {
            if (string.IsNullOrEmpty(installDir))
                throw new ArgumentException("Install directory must be provided.", nameof(installDir));
            ArgumentNullException.ThrowIfNull(mod);

            return ResolveDependencies(installDir, [installDir], mod);
        }

        /// <inheritdoc />
        public IReadOnlyList<ModDependencyStatus> ResolveDependencies(GameInstall install, ModInfo mod)
        {
            ArgumentNullException.ThrowIfNull(install);
            if (string.IsNullOrEmpty(install.InstallDir))
                throw new ArgumentException("Install directory must be provided.", nameof(install));
            ArgumentNullException.ThrowIfNull(mod);

            // A mod's own directory first (Mods/X/ holds X and the mods shipped with it), then the
            // install dir, then the Definitive Edition Mods dir and its subfolders, then the
            // Soulstorm folder the Definitive Edition may borrow mods from.
            var searchDirs = new List<string>();
            string? ownDir = string.IsNullOrEmpty(mod.ModuleFilePath) ? null : Path.GetDirectoryName(mod.ModuleFilePath);
            if (!string.IsNullOrEmpty(ownDir))
                searchDirs.Add(ownDir);
            searchDirs.AddRange(GamePaths.ModuleDirectories(install));

            return ResolveDependencies(install.InstallDir, searchDirs, mod);
        }

        /// <summary>
        /// Base modules always come from <paramref name="installDir"/>; other required mods from the
        /// first of <paramref name="searchDirs"/> holding their <c>.module</c>. A dependency's
        /// <c>ModFolder</c> resolves relative to the directory its <c>.module</c> lives in.
        /// </summary>
        private static List<ModDependencyStatus> ResolveDependencies(string installDir, IReadOnlyList<string> searchDirs, ModInfo mod)
        {
            var result = new List<ModDependencyStatus>(mod.RequiredMods.Count);

            foreach (string requiredModName in mod.RequiredMods)
            {
                // RequiredMod values may or may not carry the ".module" suffix; normalise it
                // so the file lookup matches the legacy "<name>.module" behaviour.
                string normalisedName = requiredModName.Replace(".module", "");
                // Mods are authored on Windows: "RequiredMod.1 = dxp2" must find "DXP2.module" on a
                // case-sensitive file system too.
                string moduleFilePath = FindModuleFile(installDir, searchDirs, normalisedName);

                string modFolder;
                string modRoot = installDir;
                if (File.Exists(moduleFilePath))
                {
                    // Mirror LoadModFoldersFromFile: read ModFolder with ".module" stripped.
                    modFolder = ReadModFolder(moduleFilePath);
                    modRoot = Path.GetDirectoryName(moduleFilePath) ?? installDir;
                }
                else
                {
                    modFolder = ModDependencyStatus.Missing;
                }

                // Mirror CheckforInstalledMods: the dependency is installed when the asset
                // folder exists on disk. A MISSING folder name resolves to "<dir>/MISSING"
                // which will not exist, so it is reported as not installed.
                string folderPath = GamePaths.Resolve(modRoot, modFolder);
                bool isInstalled = Directory.Exists(folderPath);

                result.Add(new ModDependencyStatus
                {
                    RequiredModName = requiredModName,
                    ModFolder = modFolder,
                    IsInstalled = isInstalled
                });
            }

            return result;
        }

        /// <summary>The first existing <c>&lt;name&gt;.module</c>, or the install-dir path when none exists.</summary>
        private static string FindModuleFile(string installDir, IReadOnlyList<string> searchDirs, string name)
        {
            string inInstallDir = GamePaths.Resolve(installDir, name + ".module");
            if (BaseModules.Contains(name))
                return inInstallDir;

            foreach (string dir in searchDirs)
            {
                string candidate = GamePaths.Resolve(dir, name + ".module");
                if (File.Exists(candidate))
                    return candidate;
            }

            return inInstallDir;
        }

        /// <summary>
        /// Directory a mod's <c>ModFolder</c> is relative to: the directory of its <c>.module</c>
        /// (the install dir for the classic games, <c>Mods/…</c> for Definitive Edition mods).
        /// </summary>
        public static string ModRoot(GameInstall install, ModInfo mod)
        {
            string? dir = string.IsNullOrEmpty(mod.ModuleFilePath) ? null : Path.GetDirectoryName(mod.ModuleFilePath);
            return string.IsNullOrEmpty(dir) ? install.InstallDir : dir;
        }

        /// <summary>
        /// Reads the last <c>ModFolder</c> attribute from a <c>.module</c> file (legacy
        /// <c>LoadModFoldersFromFile</c> kept the last match), with <c>.module</c> stripped.
        /// </summary>
        private static string ReadModFolder(string moduleFilePath)
        {
            string modFolder = "";

            foreach (string line in File.ReadLines(moduleFilePath))
            {
                if (line.Contains("ModFolder"))
                    modFolder = line.GetValueFromLine(deleteModule: true);
            }

            return modFolder;
        }
    }
}
