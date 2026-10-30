using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Reads and writes the <c>RequiredMod</c> block of <c>*.module</c> files so several mods can be
    /// combined. Port of the file logic of the legacy <c>ModMergerForm</c>; edits always go to a
    /// <c>&lt;Name&gt;_MERGED.module</c> copy so the original module is never modified.
    /// </summary>
    public interface IModMergerService
    {
        /// <summary>Suffix appended to the module name of a merged copy.</summary>
        const string MergePostfix = "_MERGED";

        /// <summary>
        /// Playable modules of <paramref name="installDir"/> that can be merged, each paired with its
        /// existing <c>_MERGED</c> copy (legacy <c>GetLoadableMods</c>).
        /// </summary>
        IReadOnlyList<MergeableModule> GetMergeableModules(string installDir, bool isOldGame);

        /// <summary>
        /// Mergeable modules of <paramref name="install"/>, including the Definitive Edition Mods dir.
        /// Each <c>_MERGED</c> copy lives next to its original module.
        /// </summary>
        IReadOnlyList<MergeableModule> GetMergeableModules(GameInstall install);

        /// <summary>
        /// Names of every non-base module installed (legacy <c>AllFoundModules</c>): the pool of mods
        /// that can be added as <c>RequiredMod</c>.
        /// </summary>
        IReadOnlyList<string> GetAllModuleNames(string installDir);

        /// <summary>Like <see cref="GetAllModuleNames(string)"/>, also scanning the Definitive Edition Mods dir.</summary>
        IReadOnlyList<string> GetAllModuleNames(GameInstall install);

        /// <summary>Parses the required mods of <paramref name="moduleFilePath"/> (legacy <c>GetActiveModsFromFile</c>).</summary>
        MergeDefinition Load(string moduleFilePath, IReadOnlyCollection<string> installedModules);

        /// <summary>
        /// Writes <paramref name="definition"/> to the <c>_MERGED</c> copy of <paramref name="module"/>,
        /// keeping every non-<c>RequiredMod</c> line of the current file. Returns the path written.
        /// </summary>
        string Save(MergeableModule module, MergeDefinition definition);

        /// <summary>Deletes the <c>_MERGED</c> copy of <paramref name="module"/>, reverting to the original.</summary>
        void DeleteMerge(MergeableModule module);
    }
}
