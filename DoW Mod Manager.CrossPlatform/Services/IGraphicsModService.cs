namespace DoW_Mod_Manager.Services
{
    /// <summary>The Direct3D wrappers the main window can drop into the game folder.</summary>
    public enum GraphicsWrapper
    {
        /// <summary>DXVK: Direct3D 8/9 to Vulkan (<c>d3d9.dll</c>, <c>d3d8.dll</c>).</summary>
        Dxvk,

        /// <summary>dgVoodoo2: Direct3D 9 to Direct3D 11 (<c>D3D9.dll</c> + its control panel).</summary>
        DgVoodoo2
    }

    public enum GraphicsWrapperState
    {
        NotInstalled,
        Installed,

        /// <summary>Installed, but the upstream version file is newer than the local one.</summary>
        UpdateAvailable
    }

    /// <summary>
    /// Installs/removes DXVK and dgVoodoo2 in the game folder. Port of the legacy
    /// <c>DxvkButton_Click</c> / <c>DgVoodoo2Button_Click</c>: the same files, downloaded from the same
    /// upstream folders, on every OS. Both wrappers provide <c>d3d9.dll</c>, so installing one removes
    /// the other. On Linux the launcher adds the Wine DLL overrides that make the game load them
    /// (see <see cref="GraphicsModService.WineDllOverrides"/>).
    /// </summary>
    public interface IGraphicsModService
    {
        /// <summary>True when every file of <paramref name="wrapper"/> is in the game folder.</summary>
        bool IsInstalled(string installDir, GraphicsWrapper wrapper);

        /// <summary>
        /// Local state, plus an upstream version check when <paramref name="checkForUpdate"/> is set
        /// (the <c>DXVKUpdateCheck</c> / <c>dgVoodoo2UpdateCheck</c> settings). A failed check reports
        /// <see cref="GraphicsWrapperState.Installed"/>.
        /// </summary>
        Task<GraphicsWrapperState> GetStateAsync(string installDir, GraphicsWrapper wrapper, bool checkForUpdate, CancellationToken ct = default);

        /// <summary>Downloads (or updates) <paramref name="wrapper"/>, removing the other wrapper first.</summary>
        Task InstallAsync(string installDir, GraphicsWrapper wrapper, CancellationToken ct = default);

        void Remove(string installDir, GraphicsWrapper wrapper);
    }
}
