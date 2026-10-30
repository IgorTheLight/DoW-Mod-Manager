using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Platform.Linux;

/// <summary>
/// Folder / shortcut / file-properties operations for Linux. Uses <c>xdg-open</c> for opening
/// files and folders and writes freedesktop <c>.desktop</c> launchers for shortcuts.
/// </summary>
public sealed class LinuxFileSystemService : IFileSystemService
{
    public void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        // If a file was passed, open its containing directory.
        var target = Directory.Exists(path)
            ? path
            : Path.GetDirectoryName(path) ?? path;

        XdgOpen(target);
    }

    /// <summary>
    /// Linux has no universal native "file properties" dialog, so we approximate the Windows
    /// behaviour by opening the containing folder in the file manager.
    /// </summary>
    public void ShowFileProperties(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var containing = File.Exists(path)
            ? Path.GetDirectoryName(path) ?? path
            : path;

        XdgOpen(containing);
    }

    public void CreateShortcut(ShortcutSpec spec)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var safeName = SanitizeFileName(spec.Name);

        var applicationsDir = Path.Combine(home, ".local", "share", "applications");
        var desktopDir = Path.Combine(home, "Desktop");

        var contents = BuildDesktopEntry(spec);

        // Always install into the applications menu; also drop one on the Desktop if it exists.
        var applicationsFile = WriteDesktopFile(applicationsDir, safeName, contents, createDir: true);
        if (applicationsFile is not null)
            MakeExecutable(applicationsFile);

        if (Directory.Exists(desktopDir))
        {
            var desktopFile = WriteDesktopFile(desktopDir, safeName, contents, createDir: false);
            if (desktopFile is not null)
                MakeExecutable(desktopFile);
        }
    }

    private static string BuildDesktopEntry(ShortcutSpec spec)
    {
        // Build the Exec line: quote the target and append arguments verbatim.
        var exec = new StringBuilder();
        exec.Append('"').Append(spec.TargetPath).Append('"');
        if (!string.IsNullOrWhiteSpace(spec.Arguments))
            exec.Append(' ').Append(spec.Arguments);

        var sb = new StringBuilder();
        sb.Append("[Desktop Entry]\n");
        sb.Append("Type=Application\n");
        sb.Append("Name=").Append(EscapeValue(spec.Name)).Append('\n');
        if (!string.IsNullOrWhiteSpace(spec.Description))
            sb.Append("Comment=").Append(EscapeValue(spec.Description!)).Append('\n');
        sb.Append("Exec=").Append(exec).Append('\n');
        if (!string.IsNullOrWhiteSpace(spec.WorkingDirectory))
            sb.Append("Path=").Append(EscapeValue(spec.WorkingDirectory!)).Append('\n');
        if (!string.IsNullOrWhiteSpace(spec.IconPath))
            sb.Append("Icon=").Append(EscapeValue(spec.IconPath!)).Append('\n');
        sb.Append("Terminal=false\n");

        return sb.ToString();
    }

    private static string? WriteDesktopFile(string directory, string safeName, string contents, bool createDir)
    {
        try
        {
            if (createDir)
                Directory.CreateDirectory(directory);

            var file = Path.Combine(directory, safeName + ".desktop");
            File.WriteAllText(file, contents);
            return file;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static void MakeExecutable(string file)
    {
        // SetUnixFileMode is a Unix-only API; this service only ever runs on Linux, but guard
        // the call so the analyzer is satisfied on the cross-platform net10.0 target.
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            // chmod +x equivalent (owner/group/other execute, keep read/write).
            File.SetUnixFileMode(file,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch
        {
            // Non-fatal: the launcher still works, it just won't be marked trusted.
        }
    }

    private static void XdgOpen(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = Quote(target),
                UseShellExecute = false
            });
        }
        catch
        {
            // Swallow: a missing xdg-open (headless box) must not crash the UI.
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string EscapeValue(string value)
        // Desktop-entry values: escape backslashes and newlines per the spec.
        => value.Replace("\\", "\\\\").Replace("\n", "\\n");

    private static string SanitizeFileName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(c is '/' or '\\' or '\0' ? '_' : c);
        var result = sb.ToString().Trim();
        return string.IsNullOrEmpty(result) ? "shortcut" : result;
    }
}
