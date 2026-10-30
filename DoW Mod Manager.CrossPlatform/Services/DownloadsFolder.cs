using System;
using System.IO;

namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// The folder the browser saves downloads to: <c>XDG_DOWNLOAD_DIR</c> from
    /// <c>~/.config/user-dirs.dirs</c> on Linux (localized, e.g. <c>~/Téléchargements</c>), else
    /// <c>~/Downloads</c> (also the Windows default).
    /// </summary>
    public static class DownloadsFolder
    {
        public static string? Resolve()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg
                ? xdg
                : Path.Combine(home, ".config");

            string? fromUserDirs = null;
            try
            {
                string userDirs = Path.Combine(configHome, "user-dirs.dirs");
                if (File.Exists(userDirs))
                    fromUserDirs = ParseXdgUserDirs(File.ReadAllText(userDirs), home);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            foreach (var candidate in new[] { fromUserDirs, Path.Combine(home, "Downloads") })
            {
                if (candidate is not null && Directory.Exists(candidate))
                    return candidate;
            }
            return null;
        }

        /// <summary>
        /// Reads <c>XDG_DOWNLOAD_DIR="$HOME/..."</c> from a <c>user-dirs.dirs</c> file. Null when
        /// absent or when it points at the home directory itself (XDG's way of disabling it).
        /// </summary>
        public static string? ParseXdgUserDirs(string text, string home)
        {
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith("XDG_DOWNLOAD_DIR=", StringComparison.Ordinal))
                    continue;

                string value = line["XDG_DOWNLOAD_DIR=".Length..].Trim().Trim('"');
                if (value.StartsWith("$HOME", StringComparison.Ordinal))
                    value = home + value["$HOME".Length..];
                value = value.TrimEnd('/');
                return value.Length == 0 || value == home.TrimEnd('/') ? null : value;
            }
            return null;
        }
    }
}
