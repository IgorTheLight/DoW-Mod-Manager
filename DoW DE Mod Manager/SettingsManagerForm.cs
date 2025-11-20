using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;

namespace DoW_DE_Nod_Manager
{
    public partial class SettingsManagerForm : Form
    {
        public sealed class Profile
        {
            public string ProfileName;
            public string PlayerName;

            public Profile(string profileName, string playerName)
            {
                ProfileName = profileName;
                PlayerName = playerName;
            }
        }

        const string CANCEL_LABEL = "CANCEL";
        const string CLOSE_LABEL = "CLOSE";

        readonly string SETTINGS_FILE = "";
        readonly string DRIVER_SETTINGS_FILE = Path.Combine(Directory.GetCurrentDirectory(), "Drivers", "spdx9_config.txt");

        // Here is the allohwcursor setting from the driver file
        const string ALLOWHWCURSOR = "allowhwcursor";

        // Here are all settings from Local.ini in correct order
        const string CAMERA_DETAIL = "cameradetail";
        const string DYNAMIC_LIGHTS = "dynamiclights";
        const string EVENT_DETAIL_LEVEL = "event_detail_level";
        const string FORCE_WATCH_MOVIES = "force_watch_movies";
        const string FX_DETAIL_LEVEL = "fx_detail_level";
        const string HUD_WIDTH = "hud_width";
        const string MODEL_DETAIL = "modeldetail";
        const string PARENTAL_CONTROL = "parentalcontrol";
        const string PERSISTENT_BODIES = "persistent_bodies";
        const string PERSISTENT_DECALS = "persistent_decals";
        const string PLAYER_PROFILE = "playerprofile";
        const string RL_SSO_NUM_TIMES_SHOWN = "rl_sso_num_times_shown";
        const string SCREEN_ADAPTER = "screenadapter";
        const string SCREEN_ANIALIAS = "screenantialias";
        const string SCREEN_DEVICE = "screendevice";
        const string SCREEN_GAMMA = "screengamma";
        const string SCREEN_HEIGHT = "screenheight";
        const string SCREEN_REFRESH = "screenrefresh";
        const string SCREEN_VSYNC = "screenvsync";
        const string SCREEN_WIDTH = "screenwidth";
        const string SCREEN_WINDOWED = "screenwindowed";
        const string SHADOW_MAP = "shadowmap";
        const string SOUND_ENABLED = "sound_enabled";
        const string SOUND_LIMIT_SAMPLES = "sound_limitsamples";
        const string SOUND_NR_CHANNELS = "sound_nrchannels";
        const string TERRAIN_ENABLE_FOW_BLUR = "terrainenablefowblur";
        const string TEXTURE_DETAIL = "texturedetail";
        const string TOTAL_MATCHES = "total_matches";
        const string UNIT_OCCLUSION = "unit_occlusion";

        readonly string PROFILES_PATH;
        const string NAME_DAT = "name.dat";
        const string UI_NAME_DAT = "ui_name.dat";
        const string PLAYERCONFIG = "playercfg.lua";
        const string PROFILE = "Profile";

        // Here are some usefull settings from playercfg.lua in correct order
        const string CURSOR_SCALE = "cursorScale";
        const string ENABLE_CHAT = "enableChat";
        const string HOTKEY_PRESET = "hotkey_preset";
        const string INVERT_DECLINATION = "invertDeclination";
        const string INVERT_PAN = "invertPan";
        const string LOCK_CURSOR = "lockCursor";
        const string SCROLL_RATE = "scrollRate";
        const string SHOW_HOTKEYS = "showHotkeys";

        const string SOUND_VOLUME_AMBIENT = "VolumeAmbient";
        const string SOUND_VOLUME_MUSIC = "VolumeMusic";
        const string SOUND_VOLUME_SFX = "VolumeSfx";
        const string SOUND_VOLUME_VOICE = "VolumeVoice";

        readonly ModManagerForm modManager;

        // Not the same settings as in ModManagerForm!
        Dictionary<string, string> settings;

        List<Profile> profiles;

        /// <summary>
        /// Creates the Form of the Settings Manager Window
        /// </summary>
        /// <param name="form"></param>
        public SettingsManagerForm(ModManagerForm form)
        {
            InitializeComponent();

            modManager = form;

            SETTINGS_FILE = Path.Combine(modManager.SettingsDir, "Local.ini");

            // Use the same icon as executable
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            // You could change PROFILES_PATH only in constructor because it's readonly
            PROFILES_PATH = Path.Combine(modManager.SettingsDir, "Profiles");

            InitializeSettingsWithDefaults();

            ReadSettingsFromLocalINI();

            FindAllProfilesInDirectory(clearProfiles: false);

            ReadSettingsFromPlayercfgLUA();

            InitializeGUIWithSettings(localINI: true, playercfgLUA: true);

            // We have to add those methods to the EventHandler here so we could avoid accidental firing of those methods after we would change the state of the CheckBox
            newPlayerTextBox.TextChanged += new EventHandler(NewPlayerTextBox_TextChanged);
            loginAttemptsComboBox.SelectedIndexChanged += new EventHandler(LoginAttemptsComboBox_SelectedIndexChanged);
            cursorScaleTrackBar.Scroll += new EventHandler(CursorScale_TrackBar_Scroll);
            enableChatCheckBox.CheckedChanged += new EventHandler(EnableChatCheckBox_CheckedChanged);
            hotkeyPresetComboBox.SelectedIndexChanged += new EventHandler(HotkeyPresetComboBox_SelectedIndexChanged);
            inverseDeclinationCheckBox.CheckedChanged += new EventHandler(InverseDeclinationCheckBox_CheckedChanged);
            inversePanCheckBox.CheckedChanged += new EventHandler(InversePanCheckBox_CheckedChanged);
            parentalControlCheckBox.CheckedChanged += new EventHandler(ParentalControlCheckBox_CheckedChanged);
            currentPlayerComboBox.SelectedIndexChanged += new EventHandler(CurrentPlayerComboBox_SelectedIndexChanged);
            scrollRateTrackBar.Scroll += new EventHandler(ScrollRateTrackBar_Scroll);
            showhotkeysCheckBox.CheckedChanged += new EventHandler(ShowhotkeysCheckBox_CheckedChanged);

            dynamicLightsComboBox.SelectedIndexChanged += new EventHandler(DynamicLightsComboBox_SelectedIndexChanged);
            effectsDetailComboBox.SelectedIndexChanged += new EventHandler(EffectsDetailComboBox_SelectedIndexChanged);
            HUDWidthTrackBar.Scroll += new EventHandler(HUDWidthTrackBar_Scroll);
            worldEventsComboBox.SelectedIndexChanged += new EventHandler(WorldEventsComboBox_SelectedIndexChanged);
            shadowsDetailComboBox.SelectedIndexChanged += new EventHandler(ShadowsDetailComboBox_SelectedIndexChanged);
            full3DCameraCheckBox.CheckedChanged += new EventHandler(Full3DCameraCheckBox_CheckedChanged);
            persistentScarringComboBox.SelectedIndexChanged += new EventHandler(PersistentScarringComboBox_SelectedIndexChanged);
            unitsOcclusionCheckBox.CheckedChanged += new EventHandler(UnitsOcclusionCheckBox_CheckedChanged);
            persistentBodiesComboBox.SelectedIndexChanged += new EventHandler(PersistentBodiesComboBox_SelectedIndexChanged);
            terrainDetailComboBox.SelectedIndexChanged += new EventHandler(TerrainDetailComboBox_SelectedIndexChanged);
            modelDetailComboBox.SelectedIndexChanged += new EventHandler(ModelDetailComboBox_SelectedIndexChanged);
            textureDetailComboBox.SelectedIndexChanged += new EventHandler(TextureDetailComboBox_SelectedIndexChanged);
            rendererComboBox.SelectedIndexChanged += new EventHandler(RendererComboBox_SelectedIndexChanged);
            activeVideocardComboBox.SelectedIndexChanged += new EventHandler(ActiveVideocardComboBox_SelectedIndexChanged);
            antialiasingCheckBox.CheckedChanged += new EventHandler(AntialiasingCheckBox_CheckedChanged);
            refreshRateComboBox.SelectedIndexChanged += new EventHandler(RefreshRateComboBox_SelectedIndexChanged);
            windowedCheckBox.CheckedChanged += new EventHandler(WindowedCheckBox_CheckedChanged);
            vSyncCheckBox.CheckedChanged += new EventHandler(VSyncCheckBox_CheckedChanged);
            gammaTrackBar.Scroll += new EventHandler(GammaTrackBar_Scroll);
            screenResolutionComboBox.SelectedIndexChanged += new EventHandler(ScreenResolutionComboBox_SelectedIndexChanged);

            soundEnabledCheckBox.CheckedChanged += new EventHandler(SoundEnabledCheckBox_CheckedChanged);
            musicVolumeTrackBar.Scroll += new EventHandler(MusicVolumeTrackBar_Scroll);
            voiceVolumeTrackBar.Scroll += new EventHandler(VoiceVolumeTrackBar_Scroll);
            effectsVolumeTrackBar.Scroll += new EventHandler(EffectsVolumeTrackBar_Scroll);
            ambientVolumeTrackBar.Scroll += new EventHandler(AmbientVolumeTarckBar_Scroll);
            soundChannelsComboBox.SelectedIndexChanged += new EventHandler(SoundChannelsComboBox_SelectedIndexChanged);
            randomizedSoundsCheckBox.CheckedChanged += new EventHandler(RandomizedSoundsCheckBox_CheckedChanged);

            closeButton.Text = CLOSE_LABEL;
            saveButton.Enabled = false;

            // Create the ToolTip and associate with the Form container.
            ToolTip toolTip1 = new ToolTip
            {
                // Set up the delays for the ToolTip.
                AutoPopDelay = 5000,
                InitialDelay = 100,
                ReshowDelay = 500,

                // Force the ToolTip text to be displayed whether or not the form is active.
                ShowAlways = true
            };

            // Set up the ToolTip text for the allowhwcursor checkbox.
            const string hwcursorTooltip = "This option toggles the usage of the DirectX 8 cursor (allowhwcursor).\nDisable this if you experience cursor flicker and immense FPS drops.";
            toolTip1.SetToolTip(hwCursorCheckBox, hwcursorTooltip);
            toolTip1.SetToolTip(allowHDCursorLabel, hwcursorTooltip);
        }

        /// <summary>
        /// This method initializes settings Dictionary with default values
        /// </summary>
        // Request the inlining of this method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void InitializeSettingsWithDefaults()
        {
            settings = new Dictionary<string, string>
            {
                // For spdx9_config
                [ALLOWHWCURSOR] = "1",

                // For Local.ini
                [CAMERA_DETAIL] = "1",
                [DYNAMIC_LIGHTS] = "2",
                [EVENT_DETAIL_LEVEL] = "2",
                [FORCE_WATCH_MOVIES] = "1",
                [FX_DETAIL_LEVEL] = "2",
                [HUD_WIDTH] = "100",
                [MODEL_DETAIL] = "2",
                [PARENTAL_CONTROL] = "0",
                [PERSISTENT_BODIES] = "0",
                [PERSISTENT_DECALS] = "0",
                [PLAYER_PROFILE] = "Profile1",
                [RL_SSO_NUM_TIMES_SHOWN] = "1",
                [SCREEN_ADAPTER] = "0",
                [SCREEN_ANIALIAS] = "0",
                [SCREEN_DEVICE] = "Dx9 : Hardware TnL",
                [SCREEN_GAMMA] = "10",
                [SCREEN_HEIGHT] = "720",
                [SCREEN_REFRESH] = "0",
                [SCREEN_VSYNC] = "0",
                [SCREEN_WIDTH] = "1280",
                [SCREEN_WINDOWED] = "0",
                [SHADOW_MAP] = "4096",
                [SOUND_ENABLED] = "1",
                [SOUND_LIMIT_SAMPLES] = "0",
                [SOUND_NR_CHANNELS] = "64",
                [TERRAIN_ENABLE_FOW_BLUR] = "0",
                [TEXTURE_DETAIL] = "1",
                [TOTAL_MATCHES] = "0",
                [UNIT_OCCLUSION] = "0",

                // For playercfg.lua
                [CURSOR_SCALE] = "1",
                [ENABLE_CHAT] = "false",
                [HOTKEY_PRESET] = "keydefaults",
                [INVERT_DECLINATION] = "0",
                [INVERT_PAN] = "1",
                [LOCK_CURSOR] = "true",
                [SCROLL_RATE] = "1",
                [SHOW_HOTKEYS] = "true",

                [SOUND_VOLUME_AMBIENT] = "0.75",
                [SOUND_VOLUME_MUSIC] = "0.75",
                [SOUND_VOLUME_SFX] = "0.75",
                [SOUND_VOLUME_VOICE] = "0.75"
            };

            profiles = new List<Profile>();
        }

        /// <summary>
        /// This method reads settings from Local.ini file to settings Dictionary
        /// </summary>
        // Request the inlining of this method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ReadSettingsFromLocalINI()
        {
            if (File.Exists(DRIVER_SETTINGS_FILE))
            {
                string[] dlines = File.ReadAllLines(DRIVER_SETTINGS_FILE);
                string[] splitresult;
                bool weFoundIt = false;

                if (dlines.Length > 14)
                {
                    if (dlines[14].StartsWith("allowhwcursor"))
                    {
                        splitresult = dlines[14].Split(' ');
                        settings[ALLOWHWCURSOR] = splitresult[1];
                    }
                }
                else
                {
                    for (int i = 0; i < dlines.Length; i++)
                    {
                        if (dlines[i].StartsWith("allowhwcursor"))
                        {
                            splitresult = dlines[i].Split(' ');
                            settings[ALLOWHWCURSOR] = splitresult[1];
                            weFoundIt = true;
                            break;      // We found what we searched for
                        }
                    }

                    if (!weFoundIt)
                        settings[ALLOWHWCURSOR] = "0";
                }
            }

            if (File.Exists(SETTINGS_FILE))
            {
                string[] lines = File.ReadAllLines(SETTINGS_FILE);

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();

                    int firstIndexOfEqualSign = line.IndexOf('=');
                    int lastIndexOfEqualSign = line.LastIndexOf('=');

                    // There must be only one "=" in the line!
                    if (firstIndexOfEqualSign == lastIndexOfEqualSign)
                    {
                        if (firstIndexOfEqualSign > 0)
                        {
                            string setting = line.Substring(0, firstIndexOfEqualSign);
                            string value = line.Substring(firstIndexOfEqualSign + 1, line.Length - firstIndexOfEqualSign - 1);

                            switch (setting)
                            {
                                case CAMERA_DETAIL:
                                    if (value == "0" || value == "1")
                                        settings[CAMERA_DETAIL] = value;
                                    break;
                                case DYNAMIC_LIGHTS:
                                    if (value == "0" || value == "1" || value == "2" || value == "3")
                                        settings[DYNAMIC_LIGHTS] = value;
                                    break;
                                case EVENT_DETAIL_LEVEL:
                                    if (value == "0" || value == "1" || value == "2")
                                        settings[EVENT_DETAIL_LEVEL] = value;
                                    break;
                                case FORCE_WATCH_MOVIES:
                                    if (value == "0" || value == "1")
                                        settings[FORCE_WATCH_MOVIES] = value;
                                    break;
                                case FX_DETAIL_LEVEL:
                                    if (value == "0" || value == "1" || value == "2")
                                        settings[FX_DETAIL_LEVEL] = value;
                                    break;
                                case HUD_WIDTH:
                                    if (Convert.ToInt32(value) >= 0 && Convert.ToInt32(value) <= 100)
                                        settings[HUD_WIDTH] = value;
                                    break;
                                case MODEL_DETAIL:
                                    if (value == "0" || value == "1" || value == "2")
                                        settings[MODEL_DETAIL] = value;
                                    break;
                                case PARENTAL_CONTROL:
                                    if (value == "0" || value == "1")
                                        settings[PARENTAL_CONTROL] = value;
                                    break;
                                case PERSISTENT_BODIES:
                                    if (value == "0" || value == "1" || value == "2" || value == "3")
                                        settings[PERSISTENT_BODIES] = value;
                                    break;
                                case PERSISTENT_DECALS:
                                    if (value == "0" || value == "1" || value == "2")
                                        settings[PERSISTENT_DECALS] = value;
                                    break;
                                case PLAYER_PROFILE:
                                    if (value.Contains(PROFILE))
                                        settings[PLAYER_PROFILE] = value;
                                    break;
                                case RL_SSO_NUM_TIMES_SHOWN:
                                    if (Convert.ToInt32(value) >= 0)
                                        settings[RL_SSO_NUM_TIMES_SHOWN] = value;
                                    break;
                                case SCREEN_ADAPTER:
                                    if (Convert.ToInt32(value) >= 0)
                                        settings[SCREEN_ADAPTER] = value;
                                    break;
                                case SCREEN_ANIALIAS:
                                    if (value == "0" || value == "1")
                                        settings[SCREEN_ANIALIAS] = value;
                                    break;
                                case SCREEN_DEVICE:
                                    if (value == "Dx9 : Hardware TnL")
                                        settings[SCREEN_DEVICE] = value;
                                    break;
                                case SCREEN_GAMMA:
                                    if (Convert.ToInt32(value) >= 0 && Convert.ToInt32(value) <= 30)        // Range must be tested!
                                        settings[SCREEN_GAMMA] = value;
                                    break;
                                case SCREEN_HEIGHT:
                                    if (value == "600" || value == "664" || value == "720" || value == "768" || value == "800" || value == "900" || value == "960" || value == "1024" || value == "1050" || value == "1080" || value == "1200" || value == "1440" || value == "1600" || value == "2160" || value == "2400")
                                        settings[SCREEN_HEIGHT] = value;
                                    break;
                                case SCREEN_VSYNC:
                                    if (value == "0" || value == "1")
                                        settings[SCREEN_VSYNC] = value;
                                    break;
                                case SCREEN_REFRESH:
                                    if (value == "0" || value == "59" || value == "60" || value == "120" || value == "144")
                                        settings[SCREEN_REFRESH] = value;
                                    break;
                                case SCREEN_WIDTH:
                                    if (value == "800" || value == "1024" || value == "1152" || value == "1176" || value == "1280" || value == "1366" || value == "1400" || value == "1440" || value == "1600" || value == "1680" || value == "1920" || value == "2560" || value == "3840" || value == "4096")
                                        settings[SCREEN_WIDTH] = value;
                                    break;
                                case SCREEN_WINDOWED:
                                    if (value == "0" || value == "1")
                                        settings[SCREEN_WINDOWED] = value;
                                    break;
                                case SHADOW_MAP:
                                    if (value == "0" || value == "1024" || value == "2048" || value == "4096")
                                        settings[SHADOW_MAP] = value;
                                    break;
                                case SOUND_ENABLED:
                                    if (value == "0" || value == "1")
                                        settings[SOUND_ENABLED] = value;
                                    break;
                                case SOUND_LIMIT_SAMPLES:
                                    if (value == "0" || value == "1")
                                        settings[SOUND_LIMIT_SAMPLES] = value;
                                    break;
                                case SOUND_NR_CHANNELS:
                                    if (Convert.ToInt32(value) > 0 && Convert.ToInt32(value) < 65)        // Range must be tested!
                                        settings[SOUND_NR_CHANNELS] = value;
                                    break;
                                case TERRAIN_ENABLE_FOW_BLUR:
                                    if (value == "0" || value == "1" || value == "2")
                                        settings[TERRAIN_ENABLE_FOW_BLUR] = value;
                                    break;
                                case TEXTURE_DETAIL:
                                    if (value == "0" || value == "1" || value == "2")
                                        settings[TEXTURE_DETAIL] = value;
                                    break;
                                case TOTAL_MATCHES:
                                    if (Convert.ToInt32(value) >= 0)
                                        settings[TOTAL_MATCHES] = value;
                                    break;
                                case UNIT_OCCLUSION:
                                    if (value == "0" || value == "1")
                                        settings[UNIT_OCCLUSION] = value;
                                    break;
                            }
                        }
                    }
                }
            }
            else
                saveButton.Enabled = true;
        }

        /// <summary>
        /// This method finds all profiles stored in Profiles directory
        /// </summary>
        // Request the inlining of this method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void FindAllProfilesInDirectory(bool clearProfiles)
        {
            if (clearProfiles)
                profiles.Clear();

            if (Directory.Exists(PROFILES_PATH))
            {
                string[] profileDirectories = Directory.GetDirectories(PROFILES_PATH);

                // Check if there are at least one Profile directory
                if (profileDirectories.Length > 0)
                {
                    for (int i = 0; i < profileDirectories.Length; i++)
                    {
                        //TODO: Maybe use FileInfo instead? ;-)
                        int indexOfLastSlah = profileDirectories[i].LastIndexOf("\\");
                        string profileName = profileDirectories[i].Substring(indexOfLastSlah + 1);

                        string playerName = "Player";   // Default name
                        string playerNamePath1 = Path.Combine(profileDirectories[i], UI_NAME_DAT);
                        string playerNamePath2 = Path.Combine(profileDirectories[i], NAME_DAT);

                        if (File.Exists(playerNamePath1))
                            playerName = File.ReadAllText(playerNamePath1);
                        else if (File.Exists(playerNamePath2))
                            playerName = File.ReadAllText(playerNamePath2);

                        profiles.Add(new Profile(profileName, playerName));
                    }

                    // Checks if profile listed in settings Dictionary is really exists
                    bool isProfileExist = false;
                    for (int i = 0; i < profiles.Count; i++)
                    {
                        if (settings[PLAYER_PROFILE] == profiles[i].ProfileName)
                        {
                            isProfileExist = true;
                            break;
                        }
                    }

                    if (!isProfileExist)
                        settings[PLAYER_PROFILE] = profiles[0].ProfileName;
                }
            }
        }

        /// <summary>
        /// This method reads settings from player.cfg to settimgs Dictionary
        /// </summary>
        // TODO: Investigate why this method is called twice after SettingManagerForm is launched
        // Request the inlining of this method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ReadSettingsFromPlayercfgLUA()
        {
            string profileName = settings[PLAYER_PROFILE];
            int index = currentPlayerComboBox.SelectedIndex;
            if (index > -1)
                profileName = PROFILE + (index + 1);

            string pathToPlayerConfig = Path.Combine(PROFILES_PATH, profileName, PLAYERCONFIG);

            if (File.Exists(pathToPlayerConfig))
            {
                using (StreamReader file = new StreamReader(pathToPlayerConfig))
                {
                    string line;

                    while ((line = file.ReadLine()) != null)
                    {
                        if (line.EndsWith(","))
                        {
                            (string setting, string value) = Extensions.GetSettingAndValueFromLine(line, true);

                            if (setting.Contains(CURSOR_SCALE))
                                settings[CURSOR_SCALE] = value;
                            else if (setting.Contains(ENABLE_CHAT))
                                settings[ENABLE_CHAT] = value;
                            else if (setting.Contains(HOTKEY_PRESET))
                                settings[HOTKEY_PRESET] = value.Trim('\"');
                            else if (setting.Contains(INVERT_DECLINATION))
                                settings[INVERT_DECLINATION] = value;
                            else if (setting.Contains(LOCK_CURSOR))
                                settings[LOCK_CURSOR] = value;
                            else if (setting.Contains(INVERT_PAN))
                                settings[INVERT_PAN] = value;
                            else if (setting.Contains(SCROLL_RATE))
                                settings[SCROLL_RATE] = value;
                            else if (setting.Contains(SHOW_HOTKEYS))
                                settings[SHOW_HOTKEYS] = value;
                            else if (setting.Contains(SOUND_VOLUME_AMBIENT))
                                settings[SOUND_VOLUME_AMBIENT] = value;
                            else if (setting.Contains(SOUND_VOLUME_MUSIC))
                                settings[SOUND_VOLUME_MUSIC] = value;
                            else if (setting.Contains(SOUND_VOLUME_SFX))
                                settings[SOUND_VOLUME_SFX] = value;
                            else if (setting.Contains(SOUND_VOLUME_VOICE))
                            {
                                settings[SOUND_VOLUME_VOICE] = value;

                                // We found all the setting we need
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This method initializes all GUI elements with options from settings Dictioary
        /// </summary>
        /// <param name="localINI"></param>
        /// <param name="playercfgLUA"></param>
        void InitializeGUIWithSettings(bool localINI, bool playercfgLUA)
        {
            if (localINI)
            {
                // Now we could set all ComboBoxes (METALLBAWHKSESS!!!) and CheckBoxes in our Form
                // Fun fact: Convert.ToBoolean("true") works but Convert.ToBoolean("1") fails. Only Convert.ToBoolean(1) is a good alternative
                full3DCameraCheckBox.Checked = settings[CAMERA_DETAIL] == "1";
                hwCursorCheckBox.Checked = settings[ALLOWHWCURSOR] == "1";
                // Skip CurrentMod setting
                dynamicLightsComboBox.SelectedIndex = Convert.ToInt32(settings[DYNAMIC_LIGHTS]);
                worldEventsComboBox.SelectedIndex = Convert.ToInt32(settings[EVENT_DETAIL_LEVEL]);
                // Skip Force Watch Movies setting because it doesn't really work
                effectsDetailComboBox.SelectedIndex = Convert.ToInt32(settings[FX_DETAIL_LEVEL]);
                HUDWidthTrackBar.Value = Convert.ToInt32(settings[HUD_WIDTH]);
                modelDetailComboBox.SelectedIndex = Convert.ToInt32(settings[MODEL_DETAIL]);
                parentalControlCheckBox.Checked = settings[PARENTAL_CONTROL] == "1";
                persistentBodiesComboBox.SelectedIndex = Convert.ToInt32(settings[PERSISTENT_BODIES]);
                persistentScarringComboBox.SelectedIndex = Convert.ToInt32(settings[PERSISTENT_DECALS]);
                if (profiles.Count > 0)
                {
                    currentPlayerComboBox.Items.Clear();

                    for (int i = 0; i < profiles.Count; i++)
                    {
                        currentPlayerComboBox.Items.Add(profiles[i].PlayerName);

                        if (settings[PLAYER_PROFILE] == profiles[i].ProfileName)
                            currentPlayerComboBox.SelectedIndex = i;
                    }
                }
                else
                    deleteProfileButton.Enabled = false;

                loginAttemptsComboBox.SelectedItem = settings[RL_SSO_NUM_TIMES_SHOWN];
                //TODO: Test for performance! start
                List<string> videocards = GetAllVideocards();
                int currentScreenAdapter = Convert.ToInt32(settings[SCREEN_ADAPTER]);

                activeVideocardComboBox.Items.AddRange(videocards.ToArray());
                if (currentScreenAdapter < videocards.Count)
                    activeVideocardComboBox.SelectedIndex = currentScreenAdapter;
                else
                {
                    activeVideocardComboBox.SelectedIndex = 0;
                    settings[SCREEN_ADAPTER] = "0";
                    WriteSettings(localINI: true, playercgfLUA: false);
                }
                //TODO: Test for performance! end
                antialiasingCheckBox.Checked = settings[SCREEN_ANIALIAS] == "1";
                rendererComboBox.SelectedItem = settings[SCREEN_DEVICE];
                gammaTrackBar.Value = Convert.ToInt32(settings[SCREEN_GAMMA]);
                screenResolutionComboBox.SelectedItem = settings[SCREEN_WIDTH] + "×" + settings[SCREEN_HEIGHT];
                vSyncCheckBox.Checked = settings[SCREEN_VSYNC] == "1";
                if (settings[SCREEN_REFRESH] == "0")
                    refreshRateComboBox.SelectedItem = "Auto";
                else
                    refreshRateComboBox.SelectedItem = settings[SCREEN_REFRESH] + " Hz";
                windowedCheckBox.Checked = settings[SCREEN_WINDOWED] == "1";
                switch (settings[SHADOW_MAP])
                {
                    case "0":
                        shadowsDetailComboBox.SelectedItem = "None";
                        break;
                    case "1024":
                        shadowsDetailComboBox.SelectedItem = "Low (1024)";
                        break;
                    case "2048":
                        shadowsDetailComboBox.SelectedItem = "Medium (2048)";
                        break;
                    case "4096":
                        shadowsDetailComboBox.SelectedItem = "High (4096)";
                        break;
                    default:
                        break;
                }
                soundEnabledCheckBox.Checked = settings[SOUND_ENABLED] == "1";
                if (settings[SOUND_LIMIT_SAMPLES] == "1")       // We have to invert it for covienience
                    randomizedSoundsCheckBox.Checked = false;
                else
                    randomizedSoundsCheckBox.Checked = true;
                switch (settings[SOUND_NR_CHANNELS])
                {
                    case "64":
                        soundChannelsComboBox.SelectedIndex = 2;
                        break;
                    case "32":
                        soundChannelsComboBox.SelectedIndex = 1;
                        break;
                    case "16":
                        soundChannelsComboBox.SelectedIndex = 0;
                        break;
                }
                soundQualityComboBox.Enabled = false;
                terrainDetailComboBox.SelectedIndex = Convert.ToInt32(settings[TERRAIN_ENABLE_FOW_BLUR]);
                textureDetailComboBox.SelectedIndex = Convert.ToInt32(settings[TEXTURE_DETAIL]);
                // Skip TotalMatches setting
                unitsOcclusionCheckBox.Checked = settings[UNIT_OCCLUSION] == "1";
            }

            if (playercfgLUA)
            {
                cursorScaleTrackBar.Value = Convert.ToInt32(settings[CURSOR_SCALE]);
                enableChatCheckBox.Checked = settings[ENABLE_CHAT] == "true";
                // TODO: Add other values!
                switch (settings[HOTKEY_PRESET])
                {
                    case "keydefaults":
                        hotkeyPresetComboBox.SelectedIndex = 0;
                        break;
                    case "keydefaults_grid":
                        hotkeyPresetComboBox.SelectedIndex = 1;
                        break;
                    case "keydefaults_grid_azerty":
                        hotkeyPresetComboBox.SelectedIndex = 2;
                        break;
                    case "keydefaults_grid_qwertz":
                        hotkeyPresetComboBox.SelectedIndex = 3;
                        break;
                    case "keydefaults_modern":
                        hotkeyPresetComboBox.SelectedIndex = 4;
                        break;
                }
                inverseDeclinationCheckBox.Checked = settings[INVERT_DECLINATION] == "1";
                inversePanCheckBox.Checked = settings[INVERT_PAN] == "1";
                double doubleValue = Convert.ToDouble(settings[SCROLL_RATE], new CultureInfo("en-US"));
                lockMoouseCheckBox.Checked = settings[LOCK_CURSOR] == "true";
                scrollRateTrackBar.Value = Convert.ToInt32(doubleValue * 100d);
                showhotkeysCheckBox.Checked = settings[SHOW_HOTKEYS] == "true";
                
                doubleValue = Convert.ToDouble(settings[SOUND_VOLUME_AMBIENT], new CultureInfo("en-US"));
                ambientVolumeTrackBar.Value = Convert.ToInt32(doubleValue * 100d);
                doubleValue = Convert.ToDouble(settings[SOUND_VOLUME_MUSIC], new CultureInfo("en-US"));
                musicVolumeTrackBar.Value = Convert.ToInt32(doubleValue * 100d);
                doubleValue = Convert.ToDouble(settings[SOUND_VOLUME_SFX], new CultureInfo("en-US"));
                effectsVolumeTrackBar.Value = Convert.ToInt32(doubleValue * 100d);
                doubleValue = Convert.ToDouble(settings[SOUND_VOLUME_VOICE], new CultureInfo("en-US"));
                voiceVolumeTrackBar.Value = Convert.ToInt32(doubleValue * 100d);
            }
        }

        /// <summary>
        /// This method will find all the videocards
        /// </summary>
        /// <returns>List<string></returns>
        // TODO: this method may benefit from an optimization pass!
        List<string> GetAllVideocards()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            List<string> videocards = new List<string>();

            foreach (var mbo in searcher.Get())
            {
                PropertyData currentBitsPerPixel = mbo.Properties["CurrentBitsPerPixel"];
                PropertyData description = mbo.Properties["Description"];

                if (currentBitsPerPixel != null && description != null)
                {
                    if (currentBitsPerPixel.Value != null)
                        videocards.Add(description.Value.ToString());
                }
            }

            return videocards;
        }

        /// <summary>
        /// This method reacts on SaveButton press
        /// </summary>
        void SaveButton_Click(object sender, EventArgs e)
        {
            WriteSettings(localINI: true, playercgfLUA: true);

            closeButton.Text = CLOSE_LABEL;
            saveButton.Enabled = false;
        }

        /// <summary>
        /// This method saves all settings to their respective files
        /// </summary>
        /// <param name="localINI"></param>
        /// <param name="playercgfLUA"></param>
        void WriteSettings(bool localINI, bool playercgfLUA)
        {
            // There is no Profile selected
            if (currentPlayerComboBox.Text.Length == 0)
            {
                closeButton.Text = CLOSE_LABEL;
                saveButton.Enabled = false;
                return;
            }

            if (localINI)
            {
                // Save settings that are stored in Local.ini
                using (StreamWriter sw = File.CreateText(SETTINGS_FILE))
                {
                    sw.WriteLine($"[global]");
                    sw.WriteLine($"{CAMERA_DETAIL}={settings[CAMERA_DETAIL]}");
                    sw.WriteLine($"{DYNAMIC_LIGHTS}={settings[DYNAMIC_LIGHTS]}");
                    sw.WriteLine($"{EVENT_DETAIL_LEVEL}={settings[EVENT_DETAIL_LEVEL]}");
                    sw.WriteLine($"{FORCE_WATCH_MOVIES}={settings[FORCE_WATCH_MOVIES]}");
                    sw.WriteLine($"{FX_DETAIL_LEVEL}={settings[FX_DETAIL_LEVEL]}");
                    sw.WriteLine($"{HUD_WIDTH}={settings[HUD_WIDTH]}");
                    sw.WriteLine($"{MODEL_DETAIL}={settings[MODEL_DETAIL]}");
                    sw.WriteLine($"{PARENTAL_CONTROL}={settings[PARENTAL_CONTROL]}");
                    sw.WriteLine($"{PERSISTENT_BODIES}={settings[PERSISTENT_BODIES]}");
                    sw.WriteLine($"{PERSISTENT_DECALS}={settings[PERSISTENT_DECALS]}");
                    sw.WriteLine($"{PLAYER_PROFILE}={settings[PLAYER_PROFILE]}");
                    sw.WriteLine($"{RL_SSO_NUM_TIMES_SHOWN}={settings[RL_SSO_NUM_TIMES_SHOWN]}");
                    sw.WriteLine($"{SCREEN_ADAPTER}={settings[SCREEN_ADAPTER]}");
                    sw.WriteLine($"{SCREEN_ANIALIAS}={settings[SCREEN_ANIALIAS]}");
                    sw.WriteLine($"{SCREEN_DEVICE}={settings[SCREEN_DEVICE]}");
                    sw.WriteLine($"{SCREEN_GAMMA}={settings[SCREEN_GAMMA]}");
                    sw.WriteLine($"{SCREEN_HEIGHT}={settings[SCREEN_HEIGHT]}");
                    sw.WriteLine($"{SCREEN_REFRESH}={settings[SCREEN_REFRESH]}");
                    sw.WriteLine($"{SCREEN_VSYNC}={settings[SCREEN_VSYNC]}");
                    sw.WriteLine($"{SCREEN_WIDTH}={settings[SCREEN_WIDTH]}");
                    sw.WriteLine($"{SCREEN_WINDOWED}={settings[SCREEN_WINDOWED]}");
                    sw.WriteLine($"{SHADOW_MAP}={settings[SHADOW_MAP]}");
                    sw.WriteLine($"{SOUND_ENABLED}={settings[SOUND_ENABLED]}");
                    sw.WriteLine($"{SOUND_LIMIT_SAMPLES}={settings[SOUND_LIMIT_SAMPLES]}");
                    sw.WriteLine($"{SOUND_NR_CHANNELS}={settings[SOUND_NR_CHANNELS]}");
                    sw.WriteLine($"{TERRAIN_ENABLE_FOW_BLUR}={settings[TERRAIN_ENABLE_FOW_BLUR]}");
                    sw.WriteLine($"{TEXTURE_DETAIL}={settings[TEXTURE_DETAIL]}");
                    sw.WriteLine($"{TOTAL_MATCHES}={settings[TOTAL_MATCHES]}");
                    sw.WriteLine($"{UNIT_OCCLUSION}={settings[UNIT_OCCLUSION]}");
                }

                // Write the driversettings to file
                string[] wDD;

                if (File.Exists(DRIVER_SETTINGS_FILE))
                {
                    wDD = File.ReadAllLines(DRIVER_SETTINGS_FILE);

                    if (wDD.Length > 14)
                    {
                        if (wDD[14].StartsWith("allowhwcursor"))
                        {
                            wDD[14] = ALLOWHWCURSOR + " " + settings[ALLOWHWCURSOR];
                            File.WriteAllLines(DRIVER_SETTINGS_FILE, wDD);
                        }
                        else
                            SearchForThatString(ref wDD);
                    }
                    else
                        SearchForThatString(ref wDD);
                }
                else
                {
                    // File doesn't exist - create a new one just with one setting
                    File.WriteAllText(DRIVER_SETTINGS_FILE, ALLOWHWCURSOR + " " + settings[ALLOWHWCURSOR]);
                }
            }

            if (playercgfLUA)
            {
                // Save settings that are stored in playercfg.lua
                // TODO: Use Streams insted of reading and writing the whoile file at once
                string pathToPlayerConfig = Path.Combine(PROFILES_PATH, PROFILE + (currentPlayerComboBox.SelectedIndex + 1).ToString(), PLAYERCONFIG);

                if (File.Exists(pathToPlayerConfig))
                {
                    string[] lines = File.ReadAllLines(pathToPlayerConfig);

                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].EndsWith(","))
                        {
                            if (lines[i].Contains(CURSOR_SCALE))
                                lines[i] = $"\t{CURSOR_SCALE} = {settings[CURSOR_SCALE]},";
                            else if (lines[i].Contains(ENABLE_CHAT))
                                lines[i] = $"\t{ENABLE_CHAT} = {settings[ENABLE_CHAT]},";
                            else if (lines[i].Contains(HOTKEY_PRESET))
                                lines[i] = $"\t{HOTKEY_PRESET} = \"{settings[HOTKEY_PRESET]}\",";
                            else if (lines[i].Contains(INVERT_DECLINATION))
                                lines[i] = $"\t{INVERT_DECLINATION} = {settings[INVERT_DECLINATION]},";
                            else if (lines[i].Contains(INVERT_PAN))
                                lines[i] = $"\t{INVERT_PAN} = {settings[INVERT_PAN]},";
                            else if (lines[i].Contains(LOCK_CURSOR))
                                lines[i] = $"\t{LOCK_CURSOR} = {settings[LOCK_CURSOR]},";
                            else if (lines[i].Contains(SCROLL_RATE))
                                lines[i] = $"\t{SCROLL_RATE} = {settings[SCROLL_RATE]},";
                            else if (lines[i].Contains(SHOW_HOTKEYS))
                                lines[i] = $"\t{SHOW_HOTKEYS} = {settings[SHOW_HOTKEYS]},";
                            else if (lines[i].Contains(SOUND_VOLUME_AMBIENT))
                                lines[i] = $"\t{SOUND_VOLUME_AMBIENT} = {settings[SOUND_VOLUME_AMBIENT]},";
                            else if (lines[i].Contains(SOUND_VOLUME_MUSIC))
                                lines[i] = $"\t{SOUND_VOLUME_MUSIC} = {settings[SOUND_VOLUME_MUSIC]},";
                            else if (lines[i].Contains(SOUND_VOLUME_SFX))
                                lines[i] = $"\t{SOUND_VOLUME_SFX} = {settings[SOUND_VOLUME_SFX]},";
                            else if (lines[i].Contains(SOUND_VOLUME_VOICE))
                            {
                                lines[i] = $"\t{SOUND_VOLUME_VOICE} = {settings[SOUND_VOLUME_VOICE]},";

                                // We found all the settings we searched for
                                break;
                            }
                        }
                    }
                    File.WriteAllLines(pathToPlayerConfig, lines);
                }
                // playercfg.lua doesn't exist!
                else
                {
                    using (StreamWriter sw = File.CreateText(pathToPlayerConfig))
                    {
                        sw.WriteLine("Controls = ");
                        sw.WriteLine("{");
                        sw.WriteLine($"\t{CURSOR_SCALE} = {settings[CURSOR_SCALE]},");
                        sw.WriteLine($"\t{LOCK_CURSOR} = {settings[LOCK_CURSOR]},");
                        sw.WriteLine("}");
                        sw.WriteLine("Sound = ");
                        sw.WriteLine("{");
                        sw.WriteLine($"\t{SOUND_VOLUME_AMBIENT} = {settings[SOUND_VOLUME_AMBIENT]},");
                        sw.WriteLine($"\t{SOUND_VOLUME_MUSIC} = {settings[SOUND_VOLUME_MUSIC]},");
                        sw.WriteLine($"\t{SOUND_VOLUME_SFX} = {settings[SOUND_VOLUME_SFX]},");
                        sw.WriteLine($"\t{SOUND_VOLUME_VOICE} = {settings[SOUND_VOLUME_VOICE]},");
                        sw.WriteLine("}");
                        sw.WriteLine("player_preferences = ");
                        sw.WriteLine("{");
                        sw.WriteLine("\tcampaign_played_disorder = false,");
                        sw.WriteLine("\tcampaign_played_order = false,");
                        sw.WriteLine("\tforce_name = \"Blood Ravens\",");
                        sw.WriteLine("\trace = \"space_marine_race\",");
                        sw.WriteLine("}");
                    }
                }
            }

            void SearchForThatString(ref string[] strArray)
            {
                bool weFoundIt = false;

                for (int i = 0; i < strArray.Length; i++)
                {
                    if (strArray[i].StartsWith("allowhwcursor"))
                    {
                        strArray[i] = ALLOWHWCURSOR + " " + settings[ALLOWHWCURSOR];
                        weFoundIt = true;
                        break;      // We found what we searched for
                    }
                }

                if (!weFoundIt)
                {
                    using (StreamWriter sw = File.CreateText(DRIVER_SETTINGS_FILE))
                    {
                        // TODO - maybe just write the new line ;-)

                        // Write all that was in file before
                        for (int i = 0; i < strArray.Length; i++)
                            sw.WriteLine(strArray[i]);

                        // And then add this new line
                        sw.WriteLine(ALLOWHWCURSOR + " " + settings[ALLOWHWCURSOR]);
                    }
                }
            }
        }

        /// <summary>
        /// This method restores all settings to their default values
        /// </summary>
        void DefaultsButton_Click(object sender, EventArgs e)
        {
            InitializeSettingsWithDefaults();
            InitializeGUIWithSettings(localINI: true, playercfgLUA: true);

            saveButton.Enabled = true;
            defaultsButton.Enabled = false;
            saveButton.Focus();
        }

        /// <summary>
        /// This method closes the SettingManagerForm
        /// </summary>
        void CloseButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// This method reacts to changes in currentPlayerComboBox.SelectedIndex
        /// </summary>
        void CurrentPlayerComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ReadSettingsFromPlayercfgLUA();
            InitializeGUIWithSettings(localINI: false, playercfgLUA: true);

            settings[PLAYER_PROFILE] = PROFILE + (currentPlayerComboBox.SelectedIndex + 1).ToString();

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in parentalControlCheckBox.Checked
        /// </summary>
        void ParentalControlCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (parentalControlCheckBox.Checked)
                settings[PARENTAL_CONTROL] = "1";
            else
                settings[PARENTAL_CONTROL] = "0";

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        void CursorScale_TrackBar_Scroll(object sender, EventArgs e)
        {
            settings[CURSOR_SCALE] = cursorScaleTrackBar.Value.ToString();

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in inversePanCheckBox.Checked
        /// </summary>
        void InversePanCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (inversePanCheckBox.Checked)
                settings[INVERT_PAN] = "1";
            else
                settings[INVERT_PAN] = "0";

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in inverseDeclinationCheckBox.Checked
        /// </summary>
        void InverseDeclinationCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (inverseDeclinationCheckBox.Checked)
                settings[INVERT_DECLINATION] = "1";
            else
                settings[INVERT_DECLINATION] = "0";

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in lockMoouseCheckBox.Checked
        /// </summary>
        void LockMoouseCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (lockMoouseCheckBox.Checked)
                settings[LOCK_CURSOR] = "true";
            else
                settings[LOCK_CURSOR] = "false";

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in scrollRateTrackBar.Value
        /// </summary>
        void ScrollRateTrackBar_Scroll(object sender, EventArgs e)
        {
            double doubleValue = Convert.ToDouble(scrollRateTrackBar.Value);
            double settingValue = doubleValue / 100d;

            settings[SCROLL_RATE] = settingValue.ToString("F5", new CultureInfo("en-US"));

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in loginAttemptsComboBox.SelectedItem
        /// </summary>
        void LoginAttemptsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings[RL_SSO_NUM_TIMES_SHOWN] = loginAttemptsComboBox.SelectedItem.ToString();

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in activeVideocardComboBox.SelectedItem
        /// </summary>
        void ActiveVideocardComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings[SCREEN_ADAPTER] = activeVideocardComboBox.SelectedIndex.ToString();

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in rendererComboBox.SelectedItem
        /// </summary>
        void RendererComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings[SCREEN_DEVICE] = rendererComboBox.SelectedItem.ToString();

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in screenResolutionComboBox.SelectedItem
        /// </summary>
        void ScreenResolutionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string str = screenResolutionComboBox.SelectedItem.ToString();
            int x = str.IndexOf('×');

            settings[SCREEN_WIDTH] = str.Substring(0, x);
            settings[SCREEN_HEIGHT] = str.Substring(x + 1, str.Length - x - 1);

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in refreshRateComboBox.SelectedItem
        /// </summary>
        void RefreshRateComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string str = refreshRateComboBox.SelectedItem.ToString();
            int indexOfSpace = str.IndexOf(" ");

            if (str == "Auto")
                settings[SCREEN_REFRESH] = "0";
            else
                settings[SCREEN_REFRESH] = str.Substring(0, indexOfSpace);

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in gammaTrackBar.Value
        /// </summary>
        void GammaTrackBar_Scroll(object sender, EventArgs e)
        {
            double doubleValue = Convert.ToDouble(gammaTrackBar.Value);
            double settingValue = doubleValue / 100d;

            settings[SCREEN_GAMMA] = settingValue.ToString("F5", new CultureInfo("en-US"));

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in vSyncCheckBox.Checked
        /// </summary>
        void VSyncCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (vSyncCheckBox.Checked)
                settings[SCREEN_VSYNC] = "1";
            else
                settings[SCREEN_VSYNC] = "0";

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in windowedCheckBox.Checked
        /// </summary>
        void WindowedCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (windowedCheckBox.Checked)
                settings[SCREEN_WINDOWED] = "1";
            else
                settings[SCREEN_WINDOWED] = "0";

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in antialiasingCheckBox.Checked
        /// </summary>
        void AntialiasingCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (antialiasingCheckBox.Checked)
                settings[SCREEN_ANIALIAS] = "1";
            else
                settings[SCREEN_ANIALIAS] = "0";

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in textureDetailComboBox.SelectedIndex
        /// </summary>
        void TextureDetailComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings[TEXTURE_DETAIL] = textureDetailComboBox.SelectedIndex.ToString();

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in modelDetailComboBox.SelectedIndex
        /// </summary>
        void ModelDetailComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings[MODEL_DETAIL] = modelDetailComboBox.SelectedIndex.ToString();

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in terrainDetailComboBox.SelectedIndex
        /// </summary>
        void TerrainDetailComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings[TERRAIN_ENABLE_FOW_BLUR] = terrainDetailComboBox.SelectedIndex.ToString();

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in shadowsDetailComboBox.SelectedIndex
        /// </summary>
        void ShadowsDetailComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Using goto we could fall through even if case is not empty!
            switch (shadowsDetailComboBox.SelectedIndex)
            {
                case 0:
                    settings[SHADOW_MAP] = "0";
                    break;
                case 1:
                    settings[SHADOW_MAP] = "1024";
                    break;
                case 2:
                    settings[SHADOW_MAP] = "2048";
                    break;
                case 3:
                    settings[SHADOW_MAP] = "4096";
                    break;
            }

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in worldEventsComboBox.SelectedIndex
        /// </summary>
        void WorldEventsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings[EVENT_DETAIL_LEVEL] = worldEventsComboBox.SelectedIndex.ToString();

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in effectsDetailComboBox.SelectedIndex
        /// </summary>
        void EffectsDetailComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings[FX_DETAIL_LEVEL] = effectsDetailComboBox.SelectedIndex.ToString();

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in persistentBodiesComboBox.SelectedIndex
        /// </summary>
        void PersistentBodiesComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings[PERSISTENT_BODIES] = persistentBodiesComboBox.SelectedIndex.ToString();

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in persistentScarringComboBox.SelectedIndex
        /// </summary>
        void PersistentScarringComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings[PERSISTENT_DECALS] = persistentScarringComboBox.SelectedIndex.ToString();

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in dynamicLightsComboBox.SelectedIndex
        /// </summary>
        void DynamicLightsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings[DYNAMIC_LIGHTS] = dynamicLightsComboBox.SelectedIndex.ToString();

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in full3DCameraCheckBox.Checked
        /// </summary>
        void Full3DCameraCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (full3DCameraCheckBox.Checked)
                settings[CAMERA_DETAIL] = "1";
            else
                settings[CAMERA_DETAIL] = "0";

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in unitsOcclusionCheckBox.Checked
        /// </summary>
        void UnitsOcclusionCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (unitsOcclusionCheckBox.Checked)
                settings[UNIT_OCCLUSION] = "1";
            else
                settings[UNIT_OCCLUSION] = "0";

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in soundEnabledCheckBox.Checked
        /// </summary>
        void SoundEnabledCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (soundEnabledCheckBox.Checked)
                settings[SOUND_ENABLED] = "1";
            else
                settings[SOUND_ENABLED] = "0";

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in randomizedSoundsCheckBox.Checked
        /// </summary>
        void RandomizedSoundsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (randomizedSoundsCheckBox.Checked)       // We have to invert it for covienience
                settings[SOUND_LIMIT_SAMPLES] = "0";
            else
                settings[SOUND_LIMIT_SAMPLES] = "1";

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }
        /// <summary>
        /// This method reactos to changes in the hw_cursor_checkbox.Checked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Hw_cursor_checkbox_CheckedChanged(object sender, EventArgs e)
        {
            if (hwCursorCheckBox.Checked)
                settings[ALLOWHWCURSOR] = "1";
            else
                settings[ALLOWHWCURSOR] = "0";

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in soundChannelsComboBox.SelectedIndex
        /// </summary>
        void SoundChannelsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (soundChannelsComboBox.SelectedIndex)
            {
                case 2:
                    settings[SOUND_NR_CHANNELS] = "64";
                    break;
                case 1:
                    settings[SOUND_NR_CHANNELS] = "32";
                    break;
                case 0:
                    settings[SOUND_NR_CHANNELS] = "16";
                    break;
            }

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in ambientVolumeTrackBar.Value
        /// </summary>
        void AmbientVolumeTarckBar_Scroll(object sender, EventArgs e)
        {
            double doubleValue = Convert.ToDouble(ambientVolumeTrackBar.Value);
            double settingValue = doubleValue / 100d;

            settings[SOUND_VOLUME_AMBIENT] = settingValue.ToString("F5", new CultureInfo("en-US"));

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in effectsVolumeTrackBar.Value
        /// </summary>
        void EffectsVolumeTrackBar_Scroll(object sender, EventArgs e)
        {
            double doubleValue = Convert.ToDouble(effectsVolumeTrackBar.Value);
            double settingValue = doubleValue / 100d;

            settings[SOUND_VOLUME_SFX] = settingValue.ToString("F5", new CultureInfo("en-US"));

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in voiceVolumeTrackBar.Value
        /// </summary>
        void VoiceVolumeTrackBar_Scroll(object sender, EventArgs e)
        {
            double doubleValue = Convert.ToDouble(voiceVolumeTrackBar.Value);
            double settingValue = doubleValue / 100d;

            settings[SOUND_VOLUME_VOICE] = settingValue.ToString("F5", new CultureInfo("en-US"));

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method reacts to changes in musicVolumeTrackBar.Value
        /// </summary>
        void MusicVolumeTrackBar_Scroll(object sender, EventArgs e)
        {
            double doubleValue = Convert.ToDouble(musicVolumeTrackBar.Value);
            double settingValue = doubleValue / 100d;

            settings[SOUND_VOLUME_MUSIC] = settingValue.ToString("F5", new CultureInfo("en-US"));

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method sets the graphics to "Low" preset
        /// </summary>
        void LowGraphicsButton_Click(object sender, EventArgs e)
        {
            settings[CAMERA_DETAIL] = "0";
            settings[DYNAMIC_LIGHTS] = "0";
            settings[EVENT_DETAIL_LEVEL] = "0";
            settings[FX_DETAIL_LEVEL] = "0";
            settings[MODEL_DETAIL] = "0";
            settings[PERSISTENT_BODIES] = "0";
            settings[PERSISTENT_DECALS] = "0";
            settings[SCREEN_ANIALIAS] = "0";
            settings[SHADOW_MAP] = "0";
            settings[TERRAIN_ENABLE_FOW_BLUR] = "0";
            settings[TEXTURE_DETAIL] = "0";

            InitializeGUIWithSettings(localINI: true, playercfgLUA: false);

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            saveButton.Focus();
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method sets the graphics to "Medium" preset
        /// </summary>
        void MediumGraphicsButton_Click(object sender, EventArgs e)
        {
            settings[CAMERA_DETAIL] = "1";
            settings[DYNAMIC_LIGHTS] = "1";
            settings[EVENT_DETAIL_LEVEL] = "1";
            settings[FX_DETAIL_LEVEL] = "1";
            settings[MODEL_DETAIL] = "1";
            settings[PERSISTENT_BODIES] = "1";
            settings[PERSISTENT_DECALS] = "1";
            settings[SCREEN_ANIALIAS] = "0";
            settings[SHADOW_MAP] = "1024";
            settings[TERRAIN_ENABLE_FOW_BLUR] = "1";
            settings[TEXTURE_DETAIL] = "1";

            InitializeGUIWithSettings(localINI: true, playercfgLUA: false);

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            saveButton.Focus();
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method sets the graphics to "High" preset
        /// </summary>
        void HighGraphicsButton_Click(object sender, EventArgs e)
        {
            settings[CAMERA_DETAIL] = "1";
            settings[DYNAMIC_LIGHTS] = "2";
            settings[EVENT_DETAIL_LEVEL] = "2";
            settings[FX_DETAIL_LEVEL] = "2";
            settings[MODEL_DETAIL] = "2";
            settings[PERSISTENT_BODIES] = "2";
            settings[PERSISTENT_DECALS] = "2";
            settings[SCREEN_ANIALIAS] = "0";
            settings[SHADOW_MAP] = "2048";
            settings[TERRAIN_ENABLE_FOW_BLUR] = "2";
            settings[TEXTURE_DETAIL] = "2";

            InitializeGUIWithSettings(localINI: true, playercfgLUA: false);

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            saveButton.Focus();
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method sets the graphics to "Ultra" preset
        /// </summary>
        void UltraGraphicsButton_Click(object sender, EventArgs e)
        {
            settings[CAMERA_DETAIL] = "1";
            settings[DYNAMIC_LIGHTS] = "3";
            settings[EVENT_DETAIL_LEVEL] = "2";
            settings[FX_DETAIL_LEVEL] = "2";
            settings[MODEL_DETAIL] = "2";
            settings[PERSISTENT_BODIES] = "3";
            settings[PERSISTENT_DECALS] = "2";
            settings[SCREEN_ANIALIAS] = "1";
            settings[SHADOW_MAP] = "4096";
            settings[TERRAIN_ENABLE_FOW_BLUR] = "2";
            settings[TEXTURE_DETAIL] = "2";

            InitializeGUIWithSettings(localINI: true, playercfgLUA: false);

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            saveButton.Focus();
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method sets the sound quality to "Low" preset
        /// </summary>
        void LowAudioButton_Click(object sender, EventArgs e)
        {
            settings[SOUND_ENABLED] = "1";
            settings[SOUND_LIMIT_SAMPLES] = "1";
            settings[SOUND_NR_CHANNELS] = "16";

            InitializeGUIWithSettings(localINI: true, playercfgLUA: false);

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            saveButton.Focus();
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method sets the sound quality to "Medium" preset
        /// </summary>
        void MediumAudioButton_Click(object sender, EventArgs e)
        {
            settings[SOUND_ENABLED] = "1";
            settings[SOUND_LIMIT_SAMPLES] = "0";
            settings[SOUND_NR_CHANNELS] = "32";

            InitializeGUIWithSettings(localINI: true, playercfgLUA: false);

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            saveButton.Focus();
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method sets the sound quality to "High" preset
        /// </summary>
        void HighAudioButton_Click(object sender, EventArgs e)
        {
            settings[SOUND_ENABLED] = "1";
            settings[SOUND_LIMIT_SAMPLES] = "0";
            settings[SOUND_NR_CHANNELS] = "64";

            InitializeGUIWithSettings(localINI: true, playercfgLUA: false);

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            saveButton.Focus();
            defaultsButton.Enabled = true;
        }

        /// <summary>
        /// This method starts the FontsManagerForm
        /// </summary>
        void FontsManagerButton_Click(object sender, EventArgs e)
        {
            new FontsManagerForm(settings[SCREEN_WIDTH]).Show();
        }

        /// <summary>
        /// This method starts the SystemPerformanceManagerForm
        /// </summary>
        void SystemPerformanceManagerButton_Click(object sender, EventArgs e)
        {
            new SystemPerformanceManagerForm(modManager).Show();
        }

        /// <summary>
        /// This method deletes the current selected Player Profile
        /// </summary>
        void DeleteProfileButton_Click(object sender, EventArgs e)
        {
            string playerNameToDelete = currentPlayerComboBox.SelectedItem.ToString();

            for (int i = 0; i < profiles.Count; i++)
            {
                if (playerNameToDelete == profiles[i].PlayerName)
                {
                    string profilePathToDelete = PROFILES_PATH + "\\" + profiles[i].ProfileName;

                    if (Directory.Exists(profilePathToDelete))
                    {
                        Directory.Delete(profilePathToDelete, true);

                        currentPlayerComboBox.Items.RemoveAt(i);
                    }

                    break;
                }
            }

            FindAllProfilesInDirectory(clearProfiles: true);
            InitializeGUIWithSettings(localINI: true, playercfgLUA: true);
        }

        /// <summary>
        /// This method creates a new Player Profile
        /// </summary>
        void CreateProfileButton_Click(object sender, EventArgs e)
        {
            int indexOfNewProfile = 1;

            if (Directory.Exists(PROFILES_PATH))
            {
                string[] profiles = Directory.GetDirectories(PROFILES_PATH);
                int[] profilesIndexes = new int[profiles.Length];

                for (int i = 0; i < profiles.Length; i++)
                {
                    // Delete the full patch
                    int indexOfLastSlah = profiles[i].LastIndexOf("\\");
                    profiles[i] = profiles[i].Substring(indexOfLastSlah + 1);

                    // Delete the "Profile" part of the string and convert the rest to int
                    profilesIndexes[i] = Convert.ToInt32(profiles[i].Substring(7));

                    if (indexOfNewProfile == profilesIndexes[i])
                        indexOfNewProfile++;
                }
            }

            string newProfileName = PROFILE + indexOfNewProfile;
            string newProfilePath = Path.Combine(PROFILES_PATH, newProfileName);

            try
            {
                Directory.CreateDirectory(newProfilePath);

                File.WriteAllText(newProfilePath + "\\" + NAME_DAT, newPlayerTextBox.Text, Encoding.GetEncoding("utf-16"));

                newPlayerTextBox.Text = "";
                deleteProfileButton.Enabled = true;

                FindAllProfilesInDirectory(clearProfiles: true);
                InitializeGUIWithSettings(localINI: true, playercfgLUA: true);

                // Create an empty DoWDE directory
                Directory.CreateDirectory(Path.Combine(newProfilePath, "DoWDE"));

                // Create a PLAYERCONFIG file and fill it with default values
                //TODO: Code duplication
                string pathToPlayerConfig = Path.Combine(newProfilePath, PLAYERCONFIG);
                using (StreamWriter sw = File.CreateText(pathToPlayerConfig))
                {
                    sw.WriteLine("Controls = ");
                    sw.WriteLine("{");
                    sw.WriteLine($"\t{CURSOR_SCALE} = {settings[CURSOR_SCALE]},");
                    sw.WriteLine($"\t{LOCK_CURSOR} = {settings[LOCK_CURSOR]},");
                    sw.WriteLine("}");
                    sw.WriteLine("Sound = ");
                    sw.WriteLine("{");
                    sw.WriteLine($"\t{SOUND_VOLUME_AMBIENT} = {settings[SOUND_VOLUME_AMBIENT]},");
                    sw.WriteLine($"\t{SOUND_VOLUME_MUSIC} = {settings[SOUND_VOLUME_MUSIC]},");
                    sw.WriteLine($"\t{SOUND_VOLUME_SFX} = {settings[SOUND_VOLUME_SFX]},");
                    sw.WriteLine($"\t{SOUND_VOLUME_VOICE} = {settings[SOUND_VOLUME_VOICE]},");
                    sw.WriteLine("}");
                    sw.WriteLine("player_preferences = ");
                    sw.WriteLine("{");
                    sw.WriteLine("\tcampaign_played_disorder = false,");
                    sw.WriteLine("\tcampaign_played_order = false,");
                    sw.WriteLine("\tforce_name = \"Blood Ravens\",");
                    sw.WriteLine("\trace = \"space_marine_race\",");
                    sw.WriteLine("}");
                }
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(ex.Message, "Error:");
            }
        }

        /// <summary>
        /// This method renames the current selected Player Profile
        /// </summary>
        void RenameProfileButton_Click(object sender, EventArgs e)
        {
            string currentPLayerName = currentPlayerComboBox.SelectedItem.ToString();

            for (int i = 0; i < profiles.Count; i++)
            {
                if (currentPLayerName == profiles[i].PlayerName)
                {
                    string profilePathToRename = PROFILES_PATH + "\\" + profiles[i].ProfileName + "\\" + NAME_DAT;
                    string playersNewName = newPlayerTextBox.Text;
                    try
                    {
                        File.WriteAllText(profilePathToRename, playersNewName, Encoding.GetEncoding("utf-16"));

                        profiles[i].PlayerName = playersNewName;
                        newPlayerTextBox.Text = "";

                        int selectedIndex = currentPlayerComboBox.SelectedIndex;
                        currentPlayerComboBox.Items.RemoveAt(i);
                        currentPlayerComboBox.Items.Insert(i, playersNewName);
                        currentPlayerComboBox.SelectedIndex = selectedIndex;
                    }
                    catch (Exception ex)
                    {
                        ThemedMessageBox.Show(ex.Message, "Error:");
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// This method reacts to chanes in newPlayerTextBox.Text
        /// </summary>
        void NewPlayerTextBox_TextChanged(object sender, EventArgs e)
        {
            if (newPlayerTextBox.TextLength > 0)
            {
                createProfileButton.Enabled = true;
                if (currentPlayerComboBox.Text.Length > 0)
                    renameProfileButton.Enabled = true;
            }
            else
            {
                createProfileButton.Enabled = false;
                renameProfileButton.Enabled = false;
            }
        }

        private void EnableChatCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (enableChatCheckBox.Checked)
                settings[ENABLE_CHAT] = "true";
            else
                settings[ENABLE_CHAT] = "false";

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        private void ShowhotkeysCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (showhotkeysCheckBox.Checked)
                settings[SHOW_HOTKEYS] = "true";
            else
                settings[SHOW_HOTKEYS] = "false";

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        private void HotkeyPresetComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (hotkeyPresetComboBox.SelectedIndex)
            {
                case 0:
                    settings[HOTKEY_PRESET] = "keydefaults";
                    break;
                case 1:
                    settings[HOTKEY_PRESET] = "keydefaults_grid";
                    break;
                case 2:
                    settings[HOTKEY_PRESET] = "keydefaults_grid_azerty";
                    break;
                case 3:
                    settings[HOTKEY_PRESET] = "keydefaults_grid_qwertz";
                    break;
                case 4:
                    settings[HOTKEY_PRESET] = "keydefaults_modern";
                    break;
            }

            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }

        void HUDWidthTrackBar_Scroll(object sender, EventArgs e)
        {
            settings[HUD_WIDTH] = HUDWidthTrackBar.Value.ToString();
            closeButton.Text = CANCEL_LABEL;
            saveButton.Enabled = true;
            defaultsButton.Enabled = true;
        }
    }
}