namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Reads <c>warnings.log</c> and surfaces the fatal data errors.
    /// </summary>
    public interface IWarningsLogService
    {
        /// <summary>
        /// Reads <c>warnings.log</c> in <paramref name="directory"/> and returns every line
        /// containing <c>"Fatal Data Error"</c>. Returns an empty list when the file is absent.
        /// Mirrors the legacy <c>CheckForErrorsButton_Click</c>.
        /// </summary>
        IReadOnlyList<string> GetFatalDataErrors(string directory);
    }
}
