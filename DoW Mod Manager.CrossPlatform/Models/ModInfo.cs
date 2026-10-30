using System.Collections.Generic;

namespace DoW_Mod_Manager.Models;

/// <summary>
/// Parsed representation of a Dawn of War <c>*.module</c> file. Produced by the cross-platform
/// module parser (WS3). Mirrors the per-module information the legacy <c>GetMods()</c> extracted.
/// </summary>
public sealed class ModInfo
{
    /// <summary>Module file name without extension (the value passed to <c>-modname</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Absolute path of the <c>.module</c> file (empty when parsed from a string).</summary>
    public string ModuleFilePath { get; init; } = string.Empty;

    /// <summary>Value of the <c>ModFolder</c> attribute — the asset folder backing the mod.</summary>
    public string ModFolder { get; init; } = string.Empty;

    /// <summary>Value of the <c>ModVersion</c> attribute, if present.</summary>
    public string ModVersion { get; init; } = string.Empty;

    /// <summary>True when <c>Playable = 1</c> (always true for the old games).</summary>
    public bool IsPlayable { get; init; }

    /// <summary>Names referenced by <c>RequiredMod.N =</c> lines, in declared order.</summary>
    public IReadOnlyList<string> RequiredMods { get; init; } = new List<string>();

    /// <summary>Display string used by the mod list (appends the version like the legacy UI).</summary>
    public override string ToString() =>
        ModVersion.Length > 0 ? $"{Name}   (Version {ModVersion})" : Name;
}

/// <summary>
/// Installation status of a single required-mod dependency, resolved by checking whether the
/// required mod's <c>ModFolder</c> exists on disk. Mirrors the legacy
/// <c>LoadModFoldersFromFile()</c> + <c>CheckforInstalledMods()</c>.
/// </summary>
public sealed class ModDependencyStatus
{
    /// <summary>The required-mod name from the <c>RequiredMod.N =</c> line (no <c>.module</c>).</summary>
    public string RequiredModName { get; init; } = string.Empty;

    /// <summary>The asset folder the required mod declares; <see cref="Missing"/> if its module is absent.</summary>
    public string ModFolder { get; init; } = string.Empty;

    /// <summary>True when the required mod's asset folder exists on disk.</summary>
    public bool IsInstalled { get; init; }

    /// <summary>Sentinel the legacy code used when a required mod's <c>.module</c> was not found.</summary>
    public const string Missing = "MISSING";
}
