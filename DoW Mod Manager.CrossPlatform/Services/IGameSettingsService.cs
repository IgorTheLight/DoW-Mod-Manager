using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Services
{
    /// <summary>Setting names used in <c>Local.ini</c>, <c>spdx9_config.txt</c> and <c>playercfg.lua</c>.</summary>
    public static class GameSettingsKeys
    {
        // Drivers/spdx9_config.txt
        public const string AllowHwCursor = "allowhwcursor";

        // Local.ini, in the order the game (and the legacy writer) uses.
        public const string CameraDetail = "cameradetail";
        public const string CurrentMod = "currentmoddc";
        public const string DynamicLights = "dynamiclights";
        public const string EventDetailLevel = "event_detail_level";
        public const string ForceWatchMovies = "force_watch_movies";
        public const string FullResTeamColour = "fullres_teamcolour";
        public const string FxDetailLevel = "fx_detail_level";
        public const string ModelDetail = "modeldetail";
        public const string ParentalControl = "parentalcontrol";
        public const string PersistentBodies = "persistent_bodies";
        public const string PersistentDecals = "persistent_decals";
        public const string PlayerProfile = "playerprofile";
        public const string RlSsoNumTimesShown = "rl_sso_num_times_shown";
        public const string ScreenAdapter = "screenadapter";
        public const string ScreenAntialias = "screenantialias";
        public const string ScreenDepth = "screendepth";
        public const string ScreenDevice = "screendevice";
        public const string ScreenGamma = "screengamma";
        public const string ScreenHeight = "screenheight";
        public const string ScreenNoVsync = "screennovsync";
        public const string ScreenRefresh = "screenrefresh";
        public const string ScreenWidth = "screenwidth";
        public const string ScreenWindowed = "screenwindowed";
        public const string ShadowBlob = "shadowblob";
        public const string ShadowMap = "shadowmap";
        public const string ShadowVolume = "shadowvolume";
        public const string SoundEnabled = "sound_enabled";
        public const string SoundLimitSamples = "sound_limitsamples";
        public const string SoundNrChannels = "sound_nrchannels";
        public const string SoundQuality = "sound_quality";
        public const string TerrainEnableFowBlur = "terrainenablefowblur";
        public const string TextureDetail = "texturedetail";
        public const string TotalMatches = "total_matches";
        public const string UnitOcclusion = "unit_occlusion";

        // Profiles/<ProfileN>/playercfg.lua
        public const string InvertDeclination = "invertDeclination";
        public const string InvertPan = "invertPan";
        public const string ScrollRate = "scrollRate";
        public const string VolumeAmbient = "VolumeAmbient";
        public const string VolumeMusic = "VolumeMusic";
        public const string VolumeSfx = "VolumeSfx";
        public const string VolumeVoice = "VolumeVoice";
    }

    /// <summary>A player profile: its <c>Profiles/ProfileN</c> directory and the name in <c>name.dat</c>.</summary>
    public sealed record PlayerProfile(string ProfileName, string PlayerName)
    {
        public override string ToString() => PlayerName;
    }

    /// <summary>Graphics presets of the Settings Manager (LOW- … ULTRA+).</summary>
    public enum GraphicsPreset { LowMinus, Low, Medium, High, Ultra, UltraPlus }

    /// <summary>Audio presets of the Settings Manager.</summary>
    public enum AudioPreset { Low, Medium, High }

    /// <summary>
    /// Launch-option side effect of a graphics preset: some presets also toggle the
    /// <c>-forcehighpoly</c> / <c>-noprecachemodels</c> start options stored in <c>DoW Mod Manager.ini</c>.
    /// </summary>
    public sealed record LaunchFlagChange(bool? ForceHighPoly, bool? NoPrecacheModels);

    /// <summary>The game's own settings as a string key/value bag (legacy <c>SettingsManagerForm.settings</c>).</summary>
    public sealed class GameSettings
    {
        private readonly Dictionary<string, string> _values;

        public GameSettings(IDictionary<string, string> values)
        {
            _values = new Dictionary<string, string>(values, StringComparer.Ordinal);
        }

        public string this[string key]
        {
            get => _values[key];
            set => _values[key] = value;
        }

        public IReadOnlyDictionary<string, string> Values => _values;

        public GameSettings Clone() => new(_values);
    }

    /// <summary>
    /// Reads and writes the Dawn of War game settings edited by the Settings Manager: <c>Local.ini</c>,
    /// the <c>allowhwcursor</c> line of <c>Drivers/spdx9_config.txt</c>, player profiles and each
    /// profile's <c>playercfg.lua</c>. Cross-platform port of the legacy <c>SettingsManagerForm</c> I/O.
    /// </summary>
    public interface IGameSettingsService
    {
        GameSettings GetDefaults();

        /// <summary>Defaults for <paramref name="install"/>: <c>currentmoddc</c> is its base game module.</summary>
        GameSettings GetDefaults(GameInstall install);

        /// <summary>
        /// Reads every settings file of a classic install whose user data lives in
        /// <paramref name="installDir"/> over the defaults. The <c>playercfg.lua</c> values come from
        /// the profile named in <c>Local.ini</c> (or the first existing profile if that one is gone).
        /// </summary>
        GameSettings Read(string installDir);

        /// <summary>
        /// Like <see cref="Read(string)"/>: <c>spdx9_config.txt</c> from <see cref="GameInstall.InstallDir"/>,
        /// <c>Local.ini</c> and profiles from <see cref="GameInstall.UserDataDir"/>.
        /// </summary>
        GameSettings Read(GameInstall install);

        /// <summary>Overwrites the <c>playercfg.lua</c> values of <paramref name="settings"/> with <paramref name="profileName"/>'s.</summary>
        void ReadPlayerConfig(string userDataDir, string profileName, GameSettings settings);

        /// <summary>Writes <c>Local.ini</c>, <c>spdx9_config.txt</c> and the current profile's <c>playercfg.lua</c>.</summary>
        void Write(string installDir, GameSettings settings);

        /// <summary>Like <see cref="Write(string, GameSettings)"/>, honouring <see cref="GameInstall.UserDataDir"/>.</summary>
        void Write(GameInstall install, GameSettings settings);

        // Profiles live in <userDataDir>/Profiles (GameInstall.UserDataDir).
        IReadOnlyList<PlayerProfile> GetProfiles(string userDataDir);
        PlayerProfile CreateProfile(string userDataDir, string playerName);
        void RenameProfile(string userDataDir, string profileName, string newPlayerName);
        void DeleteProfile(string userDataDir, string profileName);
    }
}
