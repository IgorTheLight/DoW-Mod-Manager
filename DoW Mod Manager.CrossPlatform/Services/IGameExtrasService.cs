namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Installs/removes the two optional tweaks of the main window: the "better camera" LUA files and
    /// the "larger fonts" mod. Both are plain file downloads, so this works the same on every OS.
    /// Port of the legacy <c>CameraButton_Click</c> / <c>FontButton_Click</c>.
    /// </summary>
    public interface IGameExtrasService
    {
        bool IsCameraInstalled(string installDir);
        Task InstallCameraAsync(string installDir, CancellationToken ct = default);
        void RemoveCamera(string installDir);

        bool IsLargerFontsInstalled(string installDir);
        Task InstallLargerFontsAsync(string installDir, CancellationToken ct = default);
        void RemoveLargerFonts(string installDir);
    }
}
