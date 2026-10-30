namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// The settings keys used in <c>DoW Mod Manager.ini</c> (verbatim from the legacy
    /// <c>ModManagerForm</c> constants).
    /// </summary>
    public static class SettingsKeys
    {
        public const string ActionState = "ActionState";
        public const string ChoiceIndex = "ChoiceIndex";
        public const string Dev = "Dev";
        public const string NoMovies = "NoMovies";
        public const string ForceHighPoly = "ForceHighPoly";
        public const string NoPrecacheModels = "NoPrecacheModels";
        public const string RemoveMapFog = "RemoveMapFog";
        public const string DowOptimizations = "DowOptimizations";
        // Kept so existing INI files round-trip; never acted on. The port has no self-updater:
        // upstream's would replace it with the Windows .exe from IgorTheLight/LatestStable.
        public const string Autoupdate = "Autoupdate";
        public const string MultithreadedJIT = "MultithreadedJIT";
        public const string AOTCompilation = "AOTCompilation";
        public const string IsGOGVersion = "IsGOGVersion";
        public const string DXVKUpdateCheck = "DXVKUpdateCheck";
        public const string DgVoodoo2UpdateCheck = "dgVoodoo2UpdateCheck";

        // Text settings (ISettingsStore.ReadString / WriteString).

        /// <summary>Definitive Edition: Soulstorm folder whose mods are also listed.</summary>
        public const string SoulstormDir = "SoulstormDir";
    }
}
