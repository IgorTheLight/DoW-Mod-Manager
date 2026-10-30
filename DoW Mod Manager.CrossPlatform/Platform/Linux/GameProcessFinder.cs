using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DoW_Mod_Manager.Platform.Linux;

/// <summary>Finds running Windows game processes (under Wine/Proton) by executable name.</summary>
public interface IGameProcessFinder
{
    /// <summary>Pids of running processes whose program is <paramref name="exeName"/> (e.g. "Soulstorm.exe").</summary>
    IReadOnlyList<int> FindProcesses(string exeName);
}

/// <summary>
/// <see cref="IGameProcessFinder"/> backed by <c>/proc</c>. Under Wine/Proton the game process
/// rewrites its <c>argv[0]</c> to the Windows path of the exe (e.g. <c>Z:\...\Soulstorm.exe</c>
/// or <c>C:\...\Soulstorm.exe</c>) and its <c>comm</c> to the exe name. Only <c>argv[0]</c> and
/// <c>comm</c> are matched: wrappers such as Steam's <c>reaper</c>, <c>proton</c> (python) or
/// <c>wine</c> carry the exe path in later arguments and must not be mistaken for the game.
/// The proc root is injectable for tests.
/// </summary>
public sealed class ProcGameProcessFinder(string procRoot = "/proc") : IGameProcessFinder
{
    /// <summary>Linux truncates <c>comm</c> to 15 characters (TASK_COMM_LEN - 1).</summary>
    private const int CommMaxLength = 15;

    public IReadOnlyList<int> FindProcesses(string exeName)
    {
        var result = new List<int>();
        if (string.IsNullOrEmpty(exeName) || !Directory.Exists(procRoot))
            return result;

        IEnumerable<string> entries;
        try { entries = Directory.EnumerateDirectories(procRoot); }
        catch (IOException) { return result; }
        catch (UnauthorizedAccessException) { return result; }

        foreach (var dir in entries)
        {
            if (!int.TryParse(Path.GetFileName(dir), out var pid))
                continue;
            if (Matches(dir, exeName))
                result.Add(pid);
        }

        result.Sort();
        return result;
    }

    private static bool Matches(string procDir, string exeName)
    {
        var argv0 = ReadArgv0(Path.Combine(procDir, "cmdline"));
        if (argv0 is not null && string.Equals(WindowsOrUnixFileName(argv0), exeName, StringComparison.OrdinalIgnoreCase))
            return true;

        var comm = ReadText(Path.Combine(procDir, "comm"))?.TrimEnd('\n', '\r', '\0');
        if (string.IsNullOrEmpty(comm))
            return false;

        var expected = exeName.Length > CommMaxLength ? exeName[..CommMaxLength] : exeName;
        return string.Equals(comm, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadArgv0(string cmdlinePath)
    {
        var raw = ReadText(cmdlinePath);
        if (string.IsNullOrEmpty(raw))
            return null;
        var nul = raw.IndexOf('\0');
        return nul >= 0 ? raw[..nul] : raw;
    }

    /// <summary>File name of a Windows (<c>\</c>) or Unix (<c>/</c>) path.</summary>
    private static string WindowsOrUnixFileName(string path)
    {
        var cut = path.LastIndexOfAny(['/', '\\']);
        return cut >= 0 ? path[(cut + 1)..] : path;
    }

    private static string? ReadText(string path)
    {
        try { return File.ReadAllText(path, Encoding.UTF8); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}

/// <summary>Polling helpers on top of <see cref="IGameProcessFinder"/>.</summary>
public static class GameProcessFinderExtensions
{
    /// <summary>
    /// Polls until a process for <paramref name="exeName"/> that is not in
    /// <paramref name="ignore"/> appears; returns the newest such pid, or null on timeout or when
    /// <paramref name="giveUp"/> returns true. Honours <paramref name="ct"/> (throws on cancellation).
    /// </summary>
    public static async Task<int?> WaitForNewProcessAsync(
        this IGameProcessFinder finder,
        string exeName,
        IReadOnlyCollection<int> ignore,
        TimeSpan timeout,
        TimeSpan pollInterval,
        Func<bool>? giveUp = null,
        CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var found = finder.FindProcesses(exeName).Where(pid => !ignore.Contains(pid)).ToList();
            if (found.Count > 0)
                return found.Max();

            if (giveUp?.Invoke() == true || DateTime.UtcNow >= deadline)
                return null;

            await Task.Delay(pollInterval, ct).ConfigureAwait(false);
        }
    }
}
