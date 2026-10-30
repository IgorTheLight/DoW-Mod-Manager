using System;
using System.Collections.Generic;
using System.Linq;

namespace DoW_Mod_Manager.Models;

/// <summary>
/// The engine's own modules: the base game and its expansions, plus the Definitive Edition's
/// DXP3 and DoWDE. They are never listed as playable or mergeable mods.
/// </summary>
public static class BaseModules
{
    /// <summary>Base module names (classic games first, then the Definitive Edition ones).</summary>
    public static IReadOnlyList<string> Names { get; } = ["W40k", "WXP", "DXP2", "DXP3", "DoWDE"];

    /// <summary>True for a base module name, with or without the ".module" suffix, ignoring case.</summary>
    public static bool Contains(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        string trimmed = name.Trim();
        if (trimmed.EndsWith(".module", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^".module".Length];

        return Names.Contains(trimmed, StringComparer.OrdinalIgnoreCase);
    }
}
