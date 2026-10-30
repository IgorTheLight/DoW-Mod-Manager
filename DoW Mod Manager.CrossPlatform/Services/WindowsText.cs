namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Writes text files read by the Windows game (or the legacy Windows manager) with CRLF line
    /// endings on every platform. <see cref="File.WriteAllLines(string, IEnumerable{string})"/> and
    /// <see cref="Environment.NewLine"/> would produce bare LF on Linux. The encoding is the same as
    /// the <see cref="File"/> helpers they replace (UTF-8 without BOM).
    /// </summary>
    public static class WindowsText
    {
        public const string NewLine = "\r\n";

        /// <summary>CRLF counterpart of <see cref="File.WriteAllLines(string, IEnumerable{string})"/>.</summary>
        public static void WriteAllLines(string path, IEnumerable<string> lines)
        {
            using var writer = new StreamWriter(path);
            writer.NewLine = NewLine;
            foreach (string line in lines)
                writer.WriteLine(line);
        }

        /// <summary>
        /// CRLF counterpart of <see cref="File.WriteAllText(string, string)"/>: any LF or CRLF in
        /// <paramref name="contents"/> is written as CRLF.
        /// </summary>
        public static void WriteAllText(string path, string contents) =>
            File.WriteAllText(path, ToCrlf(contents));

        /// <summary>Normalises every line ending in <paramref name="text"/> to CRLF.</summary>
        public static string ToCrlf(string text) =>
            text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", NewLine);
    }
}
