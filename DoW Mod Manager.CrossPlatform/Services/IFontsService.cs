namespace DoW_Mod_Manager.Services
{
    /// <summary>One Dawn of War <c>*.fnt</c> descriptor as shown in the Fonts Manager.</summary>
    public sealed record FontEntry(
        string DowFontFile,
        string Description,
        bool Exists,
        string FontFileName,
        bool FontFileFound,
        int Size);

    /// <summary>
    /// Edits the <c>*.fnt</c> descriptors installed by the "larger fonts" mod: which TTF file each game
    /// font uses and its size per UI width. Port of the legacy <c>FontsManagerForm</c> I/O.
    /// </summary>
    public interface IFontsService
    {
        /// <summary>UI widths offered by the manager, "Default" first.</summary>
        IReadOnlyList<string> ScreenWidths { get; }

        /// <summary>The <c>Engine/Locale/English/Data/font</c> directory of <paramref name="installDir"/>.</summary>
        string GetFontsDirectory(string installDir);

        /// <summary>True when the font directory exists (i.e. the larger-fonts mod is installed).</summary>
        bool IsAvailable(string installDir);

        /// <summary>Creates <c>defaults.zip</c> from the current fonts the first time the manager is opened.</summary>
        void EnsureBackup(string installDir);

        /// <summary>Reads every managed font for UI width <paramref name="screenWidth"/> (a value of <see cref="ScreenWidths"/>).</summary>
        IReadOnlyList<FontEntry> Read(string installDir, string screenWidth);

        /// <summary>Writes the font file name and size of each entry for UI width <paramref name="screenWidth"/>.</summary>
        void Write(string installDir, string screenWidth, IEnumerable<FontEntry> entries);

        /// <summary>Restores every font file from <c>defaults.zip</c>. Returns false when there is no backup.</summary>
        bool RestoreDefaults(string installDir);

        /// <summary>Maps the game's screen width to the UI width it actually uses for fonts (legacy heuristic).</summary>
        string MapScreenWidth(string screenWidth);
    }
}
