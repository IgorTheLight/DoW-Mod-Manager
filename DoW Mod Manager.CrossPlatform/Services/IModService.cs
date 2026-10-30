using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Enumerates installed mods in a game directory and resolves required-mod dependencies.
    /// Cross-platform; takes the directory as a parameter and uses <see cref="System.IO"/>.
    /// </summary>
    public interface IModService
    {
        /// <summary>
        /// Enumerates <c>*.module</c> files in <paramref name="installDir"/>, skips the base
        /// modules (<c>W40k</c>, <c>WXP</c>, <c>DXP2</c>, <c>DXP3</c>, <c>DoWDE</c>) and returns the playable mods.
        /// Mirrors the legacy <c>GetMods()</c>.
        /// </summary>
        IReadOnlyList<ModInfo> GetPlayableMods(string installDir, bool isOldGame);

        /// <summary>
        /// Playable mods of <paramref name="install"/>: its install dir and, for the Definitive
        /// Edition, <see cref="GameInstall.ModsDir"/> (<c>Mods/*.module</c> and <c>Mods/*/*.module</c>).
        /// A mod name found in several places is listed once (install dir first).
        /// </summary>
        IReadOnlyList<ModInfo> GetPlayableMods(GameInstall install);

        /// <summary>
        /// Resolves the required-mod dependencies of <paramref name="mod"/> by checking, for
        /// each required mod, whether its declared <c>ModFolder</c> exists on disk.
        /// Mirrors the legacy <c>LoadModFoldersFromFile()</c> + <c>CheckforInstalledMods()</c>.
        /// </summary>
        IReadOnlyList<ModDependencyStatus> ResolveDependencies(string installDir, ModInfo mod);

        /// <summary>
        /// Like <see cref="ResolveDependencies(string, ModInfo)"/>, but looks required mods up in the
        /// mod's own directory, the install dir and the Definitive Edition Mods dir. Base modules
        /// always resolve from the install dir; each <c>ModFolder</c> is relative to its <c>.module</c>.
        /// </summary>
        IReadOnlyList<ModDependencyStatus> ResolveDependencies(GameInstall install, ModInfo mod);
    }
}
