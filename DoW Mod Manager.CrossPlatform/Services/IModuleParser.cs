using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Parses a <c>.module</c> file into a <see cref="ModInfo"/>.
    /// </summary>
    public interface IModuleParser
    {
        /// <summary>
        /// Parses the <c>.module</c> file at <paramref name="moduleFilePath"/>.
        /// </summary>
        /// <param name="moduleFilePath">Absolute path to a <c>.module</c> file.</param>
        /// <param name="isOldGame">
        /// True for Winter Assault / Original which have no Playable state and are
        /// therefore always considered playable.
        /// </param>
        ModInfo Parse(string moduleFilePath, bool isOldGame);

        /// <summary>
        /// Parses in-memory <c>.module</c> content (used primarily for tests).
        /// </summary>
        /// <param name="content">The raw textual content of a <c>.module</c> file.</param>
        /// <param name="isOldGame">See <see cref="Parse(string,bool)"/>.</param>
        /// <param name="name">Optional module name to assign to the result.</param>
        /// <param name="moduleFilePath">Optional source file path to record on the result.</param>
        ModInfo ParseContent(string content, bool isOldGame, string name = "", string moduleFilePath = "");
    }
}
