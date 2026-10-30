namespace DoW_Mod_Manager.Models;

/// <summary>
/// Describes a desktop shortcut/launcher to create. Realised as a Windows <c>.lnk</c>
/// (WS1) or a freedesktop <c>.desktop</c> file (WS2).
/// </summary>
public sealed class ShortcutSpec
{
    public required string Name { get; init; }
    public required string TargetPath { get; init; }
    public string? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? Description { get; init; }
    public string? IconPath { get; init; }
}
