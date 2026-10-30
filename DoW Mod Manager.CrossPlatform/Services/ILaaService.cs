namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Whether the Large-Address-Aware ("4 GB RAM") patch may be applied to an executable.
    /// </summary>
    public enum LaaApplicability
    {
        /// <summary>A well-formed 32-bit PE32 image for x86 (machine 0x014C, magic 0x10B).</summary>
        Applicable,

        /// <summary>
        /// A 64-bit image (PE32+ and/or a 64-bit machine type, e.g. Dawn of War: Definitive
        /// Edition). It can already address all memory; the flag is mandatory and must not be touched.
        /// </summary>
        NotNeeded64Bit,

        /// <summary>Not a PE image we understand (no MZ/PE signature, truncated, other machine type).</summary>
        UnsupportedImage,

        /// <summary>The file is missing or could not be opened for reading.</summary>
        FileUnavailable,
    }

    /// <summary>
    /// Thrown by <see cref="ILaaService.ToggleLAA(string,bool)"/> when the executable must not be
    /// patched. The file is never modified when this is thrown.
    /// </summary>
    public sealed class LaaNotApplicableException : InvalidOperationException
    {
        public LaaApplicability Applicability { get; }

        public LaaNotApplicableException(string exePath, LaaApplicability applicability)
            : base(BuildMessage(exePath, applicability))
        {
            Applicability = applicability;
        }

        private static string BuildMessage(string exePath, LaaApplicability applicability)
        {
            string name = Path.GetFileName(exePath);
            return applicability switch
            {
                LaaApplicability.NotNeeded64Bit =>
                    $"{name} is a 64-bit executable; the 4 GB RAM patch is not needed and was not applied.",
                LaaApplicability.FileUnavailable =>
                    $"{name} could not be found or read.",
                _ =>
                    $"{name} is not a 32-bit Windows executable; the 4 GB RAM patch was not applied.",
            };
        }
    }

    /// <summary>
    /// Reads and toggles the PE <c>IMAGE_FILE_LARGE_ADDRESS_AWARE</c> flag of an executable.
    /// Pure byte/stream manipulation, fully cross-platform.
    /// </summary>
    public interface ILaaService
    {
        /// <summary>
        /// Inspects the PE header of <paramref name="exePath"/> and reports whether the LAA patch
        /// can be applied. Only a 32-bit PE32 x86 image is <see cref="LaaApplicability.Applicable"/>.
        /// Never throws.
        /// </summary>
        LaaApplicability GetLaaApplicability(string exePath);

        /// <summary>
        /// Shorthand for <c>GetLaaApplicability(exePath) == LaaApplicability.Applicable</c>.
        /// </summary>
        bool IsLaaApplicable(string exePath);

        /// <summary>
        /// Returns true when the executable at <paramref name="exePath"/> has the
        /// Large-Address-Aware flag set. For a 64-bit image the actual (normally set) flag is
        /// reported; for a file that is not a recognisable PE image this returns false.
        /// </summary>
        /// <param name="exePath">Path to a Windows PE executable.</param>
        /// <param name="gogVersion">
        /// True to use the legacy GOG algorithm (<c>LAATweaksOld</c>); false to use the
        /// Steam algorithm (<c>LAATweaks</c>).
        /// </param>
        bool IsLargeAddressAware(string exePath, bool gogVersion);

        /// <summary>
        /// Flips the Large-Address-Aware flag of the executable at <paramref name="exePath"/>
        /// and recomputes the PE checksum. Returns the new LAA state (true when now set).
        /// </summary>
        /// <param name="exePath">Path to a Windows PE executable.</param>
        /// <param name="gogVersion">See <see cref="IsLargeAddressAware(string,bool)"/>.</param>
        /// <exception cref="LaaNotApplicableException">
        /// The executable is not a 32-bit PE32 image (e.g. 64-bit); nothing is written.
        /// </exception>
        /// <exception cref="IOException">The executable is in use or read-only; nothing is written.</exception>
        bool ToggleLAA(string exePath, bool gogVersion);
    }
}
