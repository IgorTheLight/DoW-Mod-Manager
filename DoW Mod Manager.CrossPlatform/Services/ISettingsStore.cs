namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Reads and writes the <c>DoW Mod Manager.ini</c> <c>key=value</c> settings.
    /// Mirrors the legacy <c>ReadSettingsFromDoWModManagerINI</c> and the writer in
    /// <c>ModManagerForm_Closing</c>.
    /// </summary>
    public interface ISettingsStore
    {
        /// <summary>
        /// Reads the settings file located in <paramref name="directory"/> and returns the
        /// settings dictionary. Defaults are applied first, then valid values from the file
        /// override them. Malformed lines are tolerated exactly as the legacy reader was.
        /// </summary>
        IDictionary<string, int> Read(string directory);

        /// <summary>
        /// Writes the supplied <paramref name="settings"/> to the settings file in
        /// <paramref name="directory"/>, preserving the legacy key order.
        /// </summary>
        void Write(string directory, IDictionary<string, int> settings);

        /// <summary>
        /// The default settings dictionary (legacy defaults from <c>ModManagerForm</c>).
        /// </summary>
        IDictionary<string, int> GetDefaults();

        /// <summary>
        /// Reads a text setting such as <see cref="SettingsKeys.SoulstormDir"/> (spaces kept).
        /// Null when the file or the key is missing or empty.
        /// </summary>
        string? ReadString(string directory, string key);

        /// <summary>
        /// Sets (or, with a null/empty <paramref name="value"/>, removes) a text setting, keeping
        /// every other setting. <see cref="Write"/> keeps text settings too.
        /// </summary>
        void WriteString(string directory, string key, string? value);
    }
}
