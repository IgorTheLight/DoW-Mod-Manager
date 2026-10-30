using System.Collections.Generic;

namespace DoW_Mod_Manager.Models;

/// <summary>State of a <c>RequiredMod</c> entry in the Mod Merger (legacy <c>ModMergerForm.ModState</c>).</summary>
public enum MergeModState
{
    /// <summary>Commented out (<c>//RequiredMod…</c>) — kept in the list but not loaded.</summary>
    Inactive,

    /// <summary>Loaded by the game.</summary>
    Active,

    /// <summary>Referenced by the module but no matching <c>.module</c> file is installed.</summary>
    Missing
}

/// <summary>One <c>RequiredMod</c> line as edited in the Mod Merger.</summary>
public sealed record MergeEntry(string Name, MergeModState State)
{
    public bool IsActive => State == MergeModState.Active;
    public bool IsInactive => State == MergeModState.Inactive;
    public bool IsMissing => State == MergeModState.Missing;

    public override string ToString() => $"{Name} ...{State}";
}

/// <summary>
/// A playable module that can be merged. When a <c>&lt;Name&gt;_MERGED.module</c> sibling exists,
/// the merger edits that copy and never touches the original.
/// </summary>
public sealed class MergeableModule
{
    public required string Name { get; init; }
    public required string FilePath { get; init; }

    /// <summary>Path of the <c>_MERGED</c> copy, or null when the module was never merged.</summary>
    public string? MergedFilePath { get; init; }

    public bool HasMerge => MergedFilePath is not null;

    /// <summary>The file the merger reads from: the merged copy if any, otherwise the original.</summary>
    public string EffectiveFilePath => MergedFilePath ?? FilePath;

    public override string ToString() => HasMerge ? $"{Name}   (merged)" : Name;
}

/// <summary>The required-mod layout of a module, as loaded by the Mod Merger.</summary>
public sealed class MergeDefinition
{
    /// <summary>Non-base required mods, in priority order.</summary>
    public IReadOnlyList<MergeEntry> Entries { get; init; } = [];

    /// <summary>Base-game modules (W40k/WXP/DXP2) referenced by the module; always written back first.</summary>
    public IReadOnlyList<string> BaseModules { get; init; } = [];
}
