using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Faithful port of the legacy <c>.module</c> parsing performed inline in
    /// <c>GetMods()</c> and the required-mods loop in
    /// <c>InstalledModsList_SelectedIndexChanged</c>.
    /// </summary>
    public sealed class ModuleParser : IModuleParser
    {
        /// <inheritdoc />
        public ModInfo Parse(string moduleFilePath, bool isOldGame)
        {
            if (string.IsNullOrEmpty(moduleFilePath))
                throw new ArgumentException("Module file path must be provided.", nameof(moduleFilePath));

            string content = File.ReadAllText(moduleFilePath);
            string name = Path.GetFileNameWithoutExtension(moduleFilePath);

            return ParseContent(content, isOldGame, name, moduleFilePath);
        }

        /// <inheritdoc />
        public ModInfo ParseContent(string content, bool isOldGame, string name = "", string moduleFilePath = "")
        {
            content ??= string.Empty;

            bool isPlayable = isOldGame;
            string modVersion = "";
            string modFolderName = "";
            var requiredMods = new List<string>();

            using var reader = new StringReader(content);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                // Winter Assault or Original doesn't have a "Playable" state.
                if (line.Contains("Playable = 1"))
                    isPlayable = true;

                // Home mod folder of the mod.
                if (line.Contains("ModFolder"))
                    modFolderName = line.GetValueFromLine(false);

                // Version of the mod.
                if (line.Contains("ModVersion"))
                    modVersion = line.GetValueFromLine(false);

                // Required mods. Commented lines are ignored (legacy required-mods loop).
                if (line.StartsWith("//") || line.StartsWith(";;"))
                    continue;

                if (line.Contains("RequiredMod"))
                    requiredMods.Add(line.GetValueFromLine(false));
            }

            return new ModInfo
            {
                Name = name,
                ModuleFilePath = moduleFilePath,
                ModFolder = modFolderName,
                ModVersion = modVersion,
                IsPlayable = isPlayable,
                RequiredMods = requiredMods
            };
        }
    }
}
