namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Faithful port of the legacy <c>Extensions.GetValueFromLine</c>.
    /// Extracts the value part of a <c>key = value</c> line from a <c>.module</c> file.
    /// </summary>
    public static class ModuleLineExtensions
    {
        /// <summary>
        /// Gets the value from a <c>key = value</c> line: everything after the first
        /// <c>=</c> with all spaces removed. When <paramref name="deleteModule"/> is true
        /// the <c>.module</c> suffix is stripped as well.
        /// Returns an empty string when there is no <c>=</c> at a positive index
        /// (i.e. the original returned "" for lines like "=x" or lines without "=").
        /// </summary>
        public static string GetValueFromLine(this string line, bool deleteModule)
        {
            int indexOfEqualSign = line.IndexOf('=');

            if (indexOfEqualSign > 0)
            {
                // Delete all chars up to and including the equal sign.
                line = line.Substring(indexOfEqualSign + 1, line.Length - indexOfEqualSign - 1);

                return deleteModule
                    ? line.Replace(" ", "").Replace(".module", "")
                    : line.Replace(" ", "");
            }

            return "";
        }
    }
}
