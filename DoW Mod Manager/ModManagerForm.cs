using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Security.Permissions;
using System.Reflection;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Runtime;
using System.Net;

namespace DoW_Mod_Manager
{
    /// <summary>
    /// This struct contains the module name and mod folder path for easy access.
    /// </summary>
    public readonly struct ModuleEntry
    {
        readonly string ModuleName;
        readonly string ModuleFolder;

        public ModuleEntry(string moduleName, string moduleFolder)
        {
            ModuleName = moduleName;
            ModuleFolder = moduleFolder;
        }

        public string GetName { get { return ModuleName; } }

        public string GetPath { get { return ModuleFolder; } }
    };

    public partial class ModManagerForm : Form
    {
        public struct GameExecutable
        {
            public const string ORIGINAL = "W40k.exe";
            public const string WINTER_ASSAULT = "W40kWA.exe";
            public const string DARK_CRUSADE = "DarkCrusade.exe";
            public const string SOULSTORM = "Soulstorm.exe";
        }

        const string CONFIG_FILE_NAME = "DoW Mod Manager.ini";
        const string JIT_PROFILE_FILE_NAME = "DoW Mod Manager.JITProfile";
        const string WARNINGS_LOG = "warnings.log";

        const string DXVK_URL = "https://raw.githubusercontent.com/IgorTheLight/DoW-Mod-Manager/master/DoW%20Mod%20Manager/DXVK/";
        const string CAMERA_URL = "https://raw.githubusercontent.com/IgorTheLight/DoW-Mod-Manager/master/DoW%20Mod%20Manager/CAMERA/";
        const string FONT_URL = "https://raw.githubusercontent.com/IgorTheLight/DoW-Mod-Manager/master/DoW%20Mod%20Manager/FONT/";

        // This is a State Machine which determines what action must be performed
        public enum Action { None, CreateNativeImage, CreateNativeImageAndDeleteJITProfile, DeleteJITProfile, DeleteNativeImage, DeleteJITProfileAndNativeImage }

        public const string ACTION_STATE = "ActionState";
        public const string CHOICE_INDEX = "ChoiceIndex";
        public const string DEV = "Dev";
        public const string NO_MOVIES = "NoMovies";
        public const string FORCE_HIGH_POLY = "ForceHighPoly";
        public const string NO_PRECACHE_MODELS = "NoPrecacheModels";
        public const string NO_FOG = "RemoveMapFog";
        public const string DOW_OPTIMIZATIONS = "DowOptimizations";
        public const string AUTOUPDATE = "Autoupdate";
        public const string MULTITHREADED_JIT = "MultithreadedJIT";
        public const string AOT_COMPILATION = "AOTCompilation";
        public const string IS_GOG_VERSION = "IsGOGVersion";
        public const string DXVK_UPDATE_CHECK = "DXVKUpdateCheck";

        // A boolean array that maps Index-wise to the filepaths indices. Index 0 checks if required mod at index 0 in the FilePaths is installed or not.
        bool[] isInstalled;
        bool isGameEXELAAPatched = false;
        bool isMessageBoxOnScreen = false;
        bool isOldGame;
        bool isNoFogTooltipShown = false;
        string dowProcessName = "";
        readonly ToolTip disabledNoFogTooltip = new ToolTip();
        Control currentToolTipControl;
        ModMergerForm modMerger = null;

        public readonly string CurrentDir = Directory.GetCurrentDirectory();
        public readonly string CurrentGameEXE = "";
        public string[] ModuleFilePaths;
        public string[] ModFolderPaths;
        public List<string> AllFoundModules;                                        // Contains the list of all available Mods that will be used by the Mod Merger
        public List<ModuleEntry> AllValidModules;                                   // Contains the list of all playable Mods that will be used by the Mod Merger
        public bool IsTimerResolutionLowered = false;

        readonly string cameraDirectory;
        readonly string fontDirectory;
        string currentModuleFilePath = "";                                          // Contains the name of the current selected Mod.
        bool isDXVKInstalled;
        bool isCameraInstalled;
        bool isFontInstalled;

        // Don't make Settings readonly or it couldn't be changed from outside the class!
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "<Pending>")]
        Dictionary<string, int> settings = new Dictionary<string, int>
        {
            [ACTION_STATE] = (int)Action.CreateNativeImage,
            [CHOICE_INDEX] = 0,
            [DEV] = 0,
            [NO_MOVIES] = 1,
            [FORCE_HIGH_POLY] = 0,
            [NO_PRECACHE_MODELS] = 0,
            [NO_FOG] = 0,
            [DOW_OPTIMIZATIONS] = 0,
            [AUTOUPDATE] = 1,
            [MULTITHREADED_JIT] = 0,
            [AOT_COMPILATION] = 1,
            [IS_GOG_VERSION] = 0,
            [DXVK_UPDATE_CHECK] = 1
        };

        /// <summary>
        /// Initializes all the necessary components used by the GUI
        /// </summary>
        // We need this PermissionSet because we are using FilesystemWatcher
        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public ModManagerForm()
        {
            cameraDirectory = Path.Combine(CurrentDir, "Engine\\Data");
            fontDirectory = Path.Combine(CurrentDir, "Engine\\Locale\\English\\Data");

            ReadSettingsFromDoWModManagerINI();

            if (settings[MULTITHREADED_JIT] == 1)
            {
                // Enable Multithreaded JIT compilation. It's not a smart idea to use it with AOT compilation
                // So: Singethreaded JIT compilation < Multithreaded JIT compilation < AOT compilation < Native code (this is available in .NET 7+)
                // Defines where to store JIT profiles
                ProfileOptimization.SetProfileRoot(CurrentDir);
                // Enables Multicore JIT with the specified profile
                ProfileOptimization.StartProfile(JIT_PROFILE_FILE_NAME);
            }

            switch (settings[ACTION_STATE])
            {
                case (int)Action.CreateNativeImage:
                    CreateNativeImage();
                    break;
                case (int)Action.CreateNativeImageAndDeleteJITProfile:
                    CreateNativeImage();
                    DeleteJITProfile();
                    settings[ACTION_STATE] = (int)Action.CreateNativeImage;
                    break;
                case (int)Action.DeleteJITProfile:
                    if (settings[MULTITHREADED_JIT] == 0)
                        DeleteJITProfile();
                    settings[ACTION_STATE] = (int)Action.None;
                    break;
                case (int)Action.DeleteNativeImage:
                    DeleteNativeImage();
                    settings[ACTION_STATE] = (int)Action.None;
                    break;
                case (int)Action.DeleteJITProfileAndNativeImage:
                    DeleteJITProfile();
                    DeleteNativeImage();
                    settings[ACTION_STATE] = (int)Action.None;
                    break;
            }

            InitializeComponent();

            // TODO: apply this only if OS = Windows 7
            // Windows 7 download fix
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Sets Title of the form to be the same as Assembly Name
            Text = Assembly.GetExecutingAssembly().GetName().Name;

            // Use the same icon as executable
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            // Initialize checkboxes with settings
            devCheckBox.Checked = settings[DEV] == 1;
            nomoviesCheckBox.Checked = settings[NO_MOVIES] == 1;
            highpolyCheckBox.Checked = settings[FORCE_HIGH_POLY] == 1;
            noprecachemodelsCheckBox.Checked = settings[NO_PRECACHE_MODELS] == 1;
            optimizationsCheckBox.Checked = settings[DOW_OPTIMIZATIONS] == 1;
            noFogCheckbox.Checked = settings[NO_FOG] == 1;
            SteamRadioButton.Checked = settings[IS_GOG_VERSION] == 0;
            GOGRadioButton.Checked = settings[IS_GOG_VERSION] == 1;

            CurrentGameEXE = GetCurrentGameEXE();

            currentDirTextBox.Text = CurrentDir;
            SetUpAllNecessaryMods();

            isGameEXELAAPatched = LAATweaks.IsLargeAddressAware(Directory.GetFiles(CurrentDir, CurrentGameEXE)[0]);
            SetGameLAALabelText();

            // Watch for any changes in game directory
            AddFileSystemWatcher();

            // Sets the focus to the mod list
            installedModsListBox.Select();

            // We have to add those methods to the EventHandler here so we could avoid accidental firing of those methods after we would change the state of the CheckBox
            requiredModsList.DrawItem += new DrawItemEventHandler(RequiredModsList_DrawItem);

            // Checkbox event handlers.
            devCheckBox.CheckedChanged += new EventHandler(DevCheckBox_CheckedChanged);
            nomoviesCheckBox.CheckedChanged += new EventHandler(NomoviesCheckBox_CheckedChanged);
            highpolyCheckBox.CheckedChanged += new EventHandler(HighpolyCheckBox_CheckedChanged);
            optimizationsCheckBox.CheckedChanged += new EventHandler(OptimizationsCheckBox_CheckedChanged);
            noFogCheckbox.CheckedChanged += new EventHandler(NoFogCheckboxCheckedChanged);
            noprecachemodelsCheckBox.CheckedChanged += new EventHandler(NoprecachemodelsCheckBox_CheckedChanged);

            // Disable no Fog checkbox if it's not Soulstorm because it only works on Soulstorm.
            if (CurrentGameEXE != GameExecutable.SOULSTORM)
            {
                noFogCheckbox.Enabled = false;
                noFogCheckbox.Checked = false;
                flowLayoutPanel1.MouseMove += new MouseEventHandler(NoFogCheckbox_hover);
            }

            // Check for an update
            if (settings[AUTOUPDATE] == 1)
            {
                // Threads could work even if application would be closed (IsBackground = true by default)
                new Thread(() =>
                {
                    // Once all is done - check for an updates.
                    DialogResult result = DownloadHelper.CheckForExeUpdate(silently: true);

                    if (result == DialogResult.OK && settings[AOT_COMPILATION] == 1)
                        settings[ACTION_STATE] = (int)Action.CreateNativeImage;
                }
                ).Start();
            }

            // Checking DXVK existing and updates
            if (File.Exists("dxvk.conf") && File.Exists("d3d9.dll") && File.Exists("dxgi.dll") && File.Exists("dxvk.version"))
            {
                try
                {
                    if (settings[DXVK_UPDATE_CHECK] == 1)
                    {
                        string stringVersion = DownloadHelper.DownloadString(DXVK_URL + "dxvk.version");
                        var version = new Version(stringVersion);

                        string currentStringVersion = File.ReadAllText("dxvk.version");
                        var currentVersion = new Version(currentStringVersion);

                        if (currentVersion < version)
                        {
                            dxvkButton.Text = "Update DXVK";
                            isDXVKInstalled = false;
                            DXVKStatusLabel.Text = "Disabled";
                            DXVKStatusLabel.ForeColor = Color.Red;
                        }
                        else
                        {
                            dxvkButton.Text = "Remove DXVK";
                            isDXVKInstalled = true;
                            DXVKStatusLabel.Text = "Enabled";
                            DXVKStatusLabel.ForeColor = Color.LimeGreen;
                        }
                    }
                    else
                    {
                        dxvkButton.Text = "Remove DXVK";
                        isDXVKInstalled = true;
                        DXVKStatusLabel.Text = "DXVK is enabled";
                        DXVKStatusLabel.ForeColor = Color.LimeGreen;
                    }
                }
                catch (Exception)
                {
                    dxvkButton.Enabled = false;
                    return;
                }
            }
            else
            {
                dxvkButton.Text = "Install DXVK";
                isDXVKInstalled = false;
                DXVKStatusLabel.Text = "DXVK is disabled";
                DXVKStatusLabel.ForeColor = Color.Red;
            }

            // Checking is better camera installed
            if (File.Exists(Path.Combine(cameraDirectory, "camera_high.lua")) && File.Exists(Path.Combine(cameraDirectory, "camera_low.lua")))
            {
                try
                {
                    cameraButton.Text = "Remove a better camera";
                    isCameraInstalled = true;
                    cameraStatusLabel.Text = "Enabled";
                    cameraStatusLabel.ForeColor = Color.LimeGreen;
                }
                catch (Exception)
                {
                    cameraButton.Enabled = false;
                    return;
                }
            }
            else
            {
                cameraButton.Text = "Install a better camera";
                isCameraInstalled = false;
                cameraStatusLabel.Text = "Disabled";
                cameraStatusLabel.ForeColor = Color.Red;
            }

            // Checking are larger fonts installed
            if (Directory.Exists(Path.Combine(fontDirectory, "art")) && Directory.Exists(Path.Combine(fontDirectory, "font")))
            {
                try
                {
                    fontButton.Text = "Remove larger fonts";
                    isFontInstalled = true;
                    fontStatusLabel.Text = "Enabled";
                    fontStatusLabel.ForeColor = Color.LimeGreen;
                }
                catch (Exception)
                {
                    fontButton.Enabled = false;
                    return;
                }
            }
            else
            {
                fontButton.Text = "Install larger fonts";
                isFontInstalled = false;
                fontStatusLabel.Text = "Disabled";
                fontStatusLabel.ForeColor = Color.Red;
            }
        }

        /// <summary>
        /// This function shows a tooltip, should the disable fog checkbox be disabled due to a wrong game version used.
        /// It has to be done since using normal tooltips won't work on disabled controls.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0019:Use pattern matching", Justification = "<Pending>")]
        void NoFogCheckbox_hover(object sender, MouseEventArgs e)
        {
            // It could be refactored with Pattern Matching but it looks less clean in my opinion
            Control parent = sender as Control;
            if (parent == null)
                return;

            Control ctrl = parent.GetChildAtPoint(e.Location);
            if (ctrl != null)
            {
                if (ctrl == noFogCheckbox && !isNoFogTooltipShown)
                {
                    disabledNoFogTooltip.Show(
                        "Disable Fog only works in Dawn of War: Soulstorm",
                        noFogCheckbox,
                        noFogCheckbox.Width / 2,
                        noFogCheckbox.Height / 2);
                    isNoFogTooltipShown = true;
                }
            }
            else
            {
                disabledNoFogTooltip.Hide(noFogCheckbox);
                isNoFogTooltipShown = false;
            }
        }

        /// <summary>
        /// This method creates Native Image using ngen tool
        /// </summary>
        static void CreateNativeImage()
        {
            // To enable AOT compilation we have to register DoW Mod Manager for NativeImage generation using ngen.exe
            string ModManagerName = AppDomain.CurrentDomain.FriendlyName;

            Process.Start(Environment.GetFolderPath(
                Environment.SpecialFolder.Windows) + @"\Microsoft.NET\Framework\v4.0.30319\ngen.exe",
                $"install \"{ModManagerName}\"");
        }

        /// <summary>
        /// This method deletes Native Image using ngen tool
        /// </summary>
        static void DeleteNativeImage()
        {
            // To disable AOT compilation we have to unregister DoW Mod Manager for NativeImage generation using ngen.exe
            string ModManagerName = AppDomain.CurrentDomain.FriendlyName;

            Process.Start(Environment.GetFolderPath(
                Environment.SpecialFolder.Windows) + @"\Microsoft.NET\Framework\v4.0.30319\ngen.exe",
                $"uninstall \"{ModManagerName}\"");
        }

        /// <summary>
        /// This method deletes JIT profile cache file
        /// </summary>
        void DeleteJITProfile()
        {
            string JITProfilePath = CurrentDir + "\\" + JIT_PROFILE_FILE_NAME;

            if (File.Exists(JITProfilePath))
                File.Delete(JITProfilePath);
        }

        /// <summary>
        /// This method reads DoW Mod Manager.ini file and loads settings in memory
        /// </summary>
        // Request the inlining of this method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ReadSettingsFromDoWModManagerINI()
        {
            if (File.Exists(CONFIG_FILE_NAME))
            {
                // Read every line of config file and try to ignore or correct all common mistakes
                string[] lines = File.ReadAllLines(CONFIG_FILE_NAME);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    line = line.Replace(" ", "");

                    int firstIndexOfEqualSign = line.IndexOf('=');
                    int lastIndexOfEqualSign = line.LastIndexOf('=');

                    // There must be only one "=" in the line!
                    if (firstIndexOfEqualSign == lastIndexOfEqualSign)
                    {
                        if (firstIndexOfEqualSign > 0)
                        {
                            string setting = Convert.ToString(line.Substring(0, firstIndexOfEqualSign));
                            int value;
                            try
                            {
                                value = Convert.ToInt32(line.Substring(firstIndexOfEqualSign + 1, line.Length - firstIndexOfEqualSign - 1));
                            }
                            catch (Exception)
                            {
                                value = 0;
                            }

                            switch (setting)
                            {
                                case ACTION_STATE:
                                    if (value < 6)
                                        // if value < 6 (we have only 6 states) - do the same as in CHOICE_INDEX case
                                        goto case CHOICE_INDEX;
                                    break;
                                case CHOICE_INDEX:
                                case DEV:
                                case NO_MOVIES:
                                case FORCE_HIGH_POLY:
                                case NO_PRECACHE_MODELS:
                                case DOW_OPTIMIZATIONS:
                                case AUTOUPDATE:
                                case MULTITHREADED_JIT:
                                case AOT_COMPILATION:
                                case NO_FOG:
                                case IS_GOG_VERSION:
                                case DXVK_UPDATE_CHECK:
                                    if (value > 0)
                                        settings[setting] = value;
                                    else
                                        settings[setting] = 0;
                                    break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This method handles the reselection of a previously selected mod.
        /// </summary>
        // Request the inlining of this method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ReselectSavedMod()
        {
            int index = GetSelectedModIndex();

            if (installedModsListBox.Items.Count > index)
                installedModsListBox.SelectedIndex = index;
            else
                installedModsListBox.SelectedIndex = installedModsListBox.Items.Count - 1;
        }

        /// <summary>
        /// This method scans for either the Soulstorm, Dark Crusade, Winter Assault or Original version of the game.
        /// </summary>
        /// <returns>string</returns>
        // Request the inlining of this method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        string GetCurrentGameEXE()
        {
            if (File.Exists(Path.Combine(CurrentDir, GameExecutable.SOULSTORM)))
            {
                currentDirectoryLabel.Text = "Your current Soulstorm directory:";
                isOldGame = false;
                return GameExecutable.SOULSTORM;
            }

            if (File.Exists(Path.Combine(CurrentDir, GameExecutable.DARK_CRUSADE)))
            {
                currentDirectoryLabel.Text = "Your current Dark Crusade directory:";
                isOldGame = false;
                return GameExecutable.DARK_CRUSADE;
            }

            if (File.Exists(Path.Combine(CurrentDir, GameExecutable.WINTER_ASSAULT)))
            {
                currentDirectoryLabel.Text = "Your current Winter Assault directory:";
                isOldGame = true;
                return GameExecutable.WINTER_ASSAULT;
            }

            // That part of the code will never be reached if you have Original + WA
            if (File.Exists(Path.Combine(CurrentDir, GameExecutable.ORIGINAL)))
            {
                currentDirectoryLabel.Text = "   Your current Dawn of War directory";
                isOldGame = true;
                return GameExecutable.ORIGINAL;
            }

            if (!isMessageBoxOnScreen)
            {
                ThemedMessageBox.Show("Neither found the Soulstorm, Dark Crusade, Winter Assault nor Original in this directory!", "ERROR:");
                isMessageBoxOnScreen = true;
                Program.TerminateApp();
            }

            return "";
        }

        /// <summary>
        /// A refactored wrapper method that is used to initialize or refresh the Mod Managers main form
        /// </summary>
        public void SetUpAllNecessaryMods()
        {
            GetMods();
            LoadModFoldersFromFile();
            ReselectSavedMod();
        }

        /// <summary>
        /// This method draws the LAA text for the game label depending on whether the flag is true (Green) or false (Red).
        /// </summary>
        void SetGameLAALabelText()
        {
            if (isGameEXELAAPatched)
            {
                gameLAAStatusLabel.Text = "Enabled";
                gameLAAStatusLabel.ForeColor = Color.LimeGreen;
            }
            else
            {
                gameLAAStatusLabel.Text = "Disabled";
                gameLAAStatusLabel.ForeColor = Color.Red;
            }
        }

        /// <summary>
        /// This method adds FileSystem watcher to capture any file changes in the game directories.
        /// </summary>
        // Request the inlining of this method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddFileSystemWatcher()
        {
            fileSystemWatcher1.Path = CurrentDir;

            fileSystemWatcher1.NotifyFilter = NotifyFilters.LastAccess
                                    | NotifyFilters.LastWrite
                                    | NotifyFilters.FileName
                                    | NotifyFilters.DirectoryName;

            fileSystemWatcher1.Changed += FileSystemWatcherOnChanged;
            fileSystemWatcher1.Created += FileSystemWatcherOnChanged;
            fileSystemWatcher1.Deleted += FileSystemWatcherOnChanged;
            fileSystemWatcher1.Renamed += FileSystemWatcherOnChanged;

            // Begin watching.
            fileSystemWatcher1.EnableRaisingEvents = true;
        }

        /// <summary>
        /// This method finds all installed *.module files and displays them in the Installed Mods Listbox without extension
        /// </summary>
        // Request the inlining of this method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void GetMods()
        {
            installedModsListBox.Items.Clear();

            // Make a new list for the new Pathitems
            List<string> newfilePathsList = new List<string>();
            AllFoundModules = new List<string>();
            AllValidModules = new List<ModuleEntry>();

            ModuleFilePaths = Directory.GetFiles(CurrentDir, "*.module");
            if (ModuleFilePaths.Length > 0)
            {
                for (int i = 0; i < ModuleFilePaths.Length; i++)
                {
                    // Check if the file actually exists
                    if (!File.Exists(ModuleFilePaths[i]))
                        continue;

                    string filePath = ModuleFilePaths[i];
                    string fileName = Path.GetFileNameWithoutExtension(filePath);

                    // There is no point of adding base modules to the list
                    switch (fileName)
                    {
                        case "W40k":
                        case "WXP":
                        case "DXP2":
                            continue;
                    }

                    // Find the List of ALL found module files for the Mod Merger available Mods List
                    AllFoundModules.Add(fileName);

                    // Read the *.module file to see the version and if the mod is playable
                    using (StreamReader file = new StreamReader(filePath))
                    {
                        string line;
                        bool isPlayable = false;
                        string modVersion = "";
                        string modFolderName = "";

                        // Filter the unplayable mods and populate the List only with playable mods
                        while ((line = file.ReadLine()) != null)
                        {
                            // Winter Assault or Original doesn't have a "Playable" state
                            if (line.Contains("Playable = 1") || isOldGame)
                                isPlayable = true;

                            // Add information about the home mod folder of a mod
                            if (line.Contains("ModFolder"))
                                modFolderName = Extensions.GetValueFromLine(line, false);

                            // Add information about a version of a mod
                            if (line.Contains("ModVersion"))
                                modVersion = Extensions.GetValueFromLine(line, false);
                        }

                        if (isPlayable)
                        {
                            ModuleEntry module = new ModuleEntry(fileName, modFolderName);

                            newfilePathsList.Add(ModuleFilePaths[i]);
                            AllValidModules.Add(module);

                            // Append version number to filename string for display
                            if (modVersion.Length > 0)
                                fileName += $"   (Version{modVersion})";

                            installedModsListBox.Items.Add(fileName);
                        }
                    }
                }

                // Override the old array that contained unplayable mods with the new one.
                ModuleFilePaths = newfilePathsList.ToArray();
            }
            else
            {
                if (!isMessageBoxOnScreen)
                {
                    ThemedMessageBox.Show("No mods were found in the specified directory! Please check your current directory again!", "Warning!");
                    isMessageBoxOnScreen = true;
                    Program.TerminateApp();
                }
            }
        }

        /// <summary>
        /// This method is called when Form is about to be closed.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        void ModManagerForm_Closing(object sender, EventArgs e)
        {
            using (StreamWriter sw = File.CreateText(CONFIG_FILE_NAME))
            {
                sw.WriteLine($"{ACTION_STATE}={settings[ACTION_STATE]}");
                sw.WriteLine($"{CHOICE_INDEX}={settings[CHOICE_INDEX]}");
                sw.WriteLine($"{DEV}={settings[DEV]}");
                sw.WriteLine($"{NO_MOVIES}={settings[NO_MOVIES]}");
                sw.WriteLine($"{FORCE_HIGH_POLY}={settings[FORCE_HIGH_POLY]}");
                sw.WriteLine($"{NO_PRECACHE_MODELS}={settings[NO_PRECACHE_MODELS]}");
                sw.WriteLine($"{DOW_OPTIMIZATIONS}={settings[DOW_OPTIMIZATIONS]}");
                sw.WriteLine($"{AUTOUPDATE}={settings[AUTOUPDATE]}");
                sw.WriteLine($"{MULTITHREADED_JIT}={settings[MULTITHREADED_JIT]}");
                sw.WriteLine($"{AOT_COMPILATION}={settings[AOT_COMPILATION]}");
                sw.WriteLine($"{NO_FOG}={settings[NO_FOG]}");
                sw.WriteLine($"{IS_GOG_VERSION}={settings[IS_GOG_VERSION]}");
                sw.Write($"{DXVK_UPDATE_CHECK}={settings[DXVK_UPDATE_CHECK]}");
            }

            // If Timer Resolution was lowered we have to keep DoW Mod Manager alive or Timer Resolution will be reset
            if (IsTimerResolutionLowered)
            {
                // Threads could work even if application would be closed
                new Thread(() =>
                {
                    int triesCount = 0;
                    string procName = "";

                    // Set it to ModManager first
                    Process dowOrModManager = Process.GetCurrentProcess();

                    // Remember DoW process Name but if DoW is not launched - just terminate the Thread
                    if (dowProcessName.Length > 0)
                        procName = dowProcessName;
                    else
                        return;

                    // We will try 30 times and then Thread will be terminated regardless
                    while (triesCount < 30)
                    {
                        Thread.Sleep(1000);
                        try
                        {
                            Process[] dowCandidate = Process.GetProcessesByName(procName);
                            dowOrModManager = dowCandidate[0];

                            // We've done what we intended to do
                            break;
                        }
                        catch (Exception)
                        {
                            triesCount++;
                        }
                    }

                    // Wait until DoW would exit and then terminate the Thread
                    while (!dowOrModManager.HasExited)
                        Thread.Sleep(10000);
                }
                ).Start();
            }
        }

        /// <summary>
        /// This method defines the event handlers for when some file was changed.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        void FileSystemWatcherOnChanged(object source, FileSystemEventArgs e)
        {
            SetUpAllNecessaryMods();

            // Invoke Mod Merger refresh should it exist.
            modMerger?.refreshAllModEntries();

            // Or using the old way of checking that
            //if (modMerger != null) modMerger.refreshAllModEntries();
        }

        /// <summary>
        /// This method updates the required mods Listbox when selecting a different installed Mod
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void InstalledModsList_SelectedIndexChanged(object sender, EventArgs e)
        {
            startModButton.Enabled = true;

            int index = installedModsListBox.SelectedIndex;

            if (index < 0 || index >= installedModsListBox.Items.Count)
            {
                index = GetSelectedModIndex();
                installedModsListBox.SelectedIndex = index;
            }
            else
                SetSelectedModIndex(index);

            currentModuleFilePath = ModuleFilePaths[index];

            requiredModsList.Items.Clear();

            // Read the file and display it line by line.
            using (StreamReader file = new StreamReader(currentModuleFilePath))
            {
                string line;

                // Populate the Required Mods List with entries from the .module file
                while ((line = file.ReadLine()) != null)
                {
                    // This line is commented
                    if (line.StartsWith("//") || line.StartsWith(";;"))
                        continue;

                    if (line.Contains("RequiredMod"))
                    {
                        line = line.GetValueFromLine(deleteModule: false);

                        requiredModsList.Items.Add(line);
                    }
                }

                LoadModFoldersFromFile();
                CheckforInstalledMods();
            }
        }

        /// <summary>
        /// Gets the numerical index of the currently selected Mod.
        /// </summary>
        /// <returns></returns>
        // Request the inlining of this method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetSelectedModIndex()
        {
            return settings[CHOICE_INDEX];
        }

        /// <summary>
        /// Sets the numerical index of the currently selected Mod.
        /// </summary>
        /// <param name="index"></param>
        // Request the inlining of this method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetSelectedModIndex(int index)
        {
            settings[CHOICE_INDEX] = index;
        }

        /// <summary>
        /// This method Checks if there is a "ModFolder" attribute in each *.module file
        /// </summary>
        // Request the inlining of this method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void LoadModFoldersFromFile()
        {
            int requiredModsCount = requiredModsList.Items.Count;
            ModFolderPaths = new string[requiredModsCount];

            // Read the file line by line and check for "ModFolder" attribute
            for (int i = 0; i < requiredModsCount; i++)
            {
                string moduleFilePath = Path.Combine(CurrentDir, requiredModsList.Items[i].ToString()) + ".module";

                if (File.Exists(moduleFilePath))
                {
                    using (StreamReader file = new StreamReader(moduleFilePath))
                    {
                        string line;

                        while ((line = file.ReadLine()) != null)
                        {
                            if (line.Contains("ModFolder"))
                                ModFolderPaths[i] = line.GetValueFromLine(deleteModule: true);
                        }
                    }
                }
                else
                    ModFolderPaths[i] = "MISSING";
            }
        }

        /// <summary>
        /// This method checks if the Mods are actually REALLY installed by checking if their asset folders are present by the name specified within the *.module files "Modfolder" tagline
        /// </summary>
        // Request the inlining of this method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CheckforInstalledMods()
        {
            startModButton.Enabled = true;

            Stream myStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DoW_Mod_Manager.Resources.checkmark.png");
            pictureBox.Image = Image.FromStream(myStream);

            string folderPath;
            int itemsCount = requiredModsList.Items.Count;
            isInstalled = new bool[itemsCount];

            for (int i = 0; i < itemsCount; i++)
            {
                folderPath = Path.Combine(CurrentDir, ModFolderPaths[i]);

                if (Directory.Exists(folderPath))
                {
                    requiredModsList.Items[i] += " ...INSTALLED!";
                    isInstalled[i] = true;
                }
                else
                {
                    requiredModsList.Items[i] += " ...MISSING!";
                    isInstalled[i] = false;
                    startModButton.Enabled = false;

                    // Select missed mod so user could find it more easily
                    requiredModsList.SelectedIndex = i;

                    myStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DoW_Mod_Manager.Resources.cross.png");
                    pictureBox.Image = Image.FromStream(myStream);
                }
            }
            myStream.Close();
        }

        /// <summary>
        /// When selecting a different required Mod, check if fixMod button is needed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void RequiredModsList_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = requiredModsList.SelectedIndex;

            if (index < 0 || index > requiredModsList.Items.Count)
                return;

            try
            {
                if (requiredModsList.SelectedItem.ToString().Contains("MISSING"))
                    fixMissingModButton.Enabled = true;
                else
                    fixMissingModButton.Enabled = false;
            }
            catch (Exception)
            {
                requiredModsList.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// This is the button to start the vanilla unmodded base game.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void StartVanillaGameButton_Click(object sender, EventArgs e)
        {
            StartGameWithOptions("W40k");
        }

        /// <summary>
        /// This is the actual start button with which you can start the game with the currently selected mod
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void StartButton_Click(object sender, EventArgs e)
        {
            StartGameWithOptions(installedModsListBox.SelectedItem.ToString());
        }

        /// <summary>
        /// This method handles starting an instance of CurrentGameEXE with arguments
        /// </summary>
        /// <param name="modName"></param>
        void StartGameWithOptions(string modName)
        {
            string arguments = "-modname " + modName;

            // Add additional arguments if needed
            if (settings[DEV] == 1)
                arguments += " -dev";
            if (settings[NO_MOVIES] == 1)
                arguments += " -nomovies";
            if (settings[FORCE_HIGH_POLY] == 1)
                arguments += " -forcehighpoly";
            if (settings[NO_PRECACHE_MODELS] == 1)
                arguments += " -noprecachemodels";

            Process proc = new Process();
            proc.StartInfo.FileName = CurrentGameEXE;
            proc.StartInfo.Arguments = arguments;
            proc.Start();

            dowProcessName = proc.ProcessName;

            // Create new thread to change the process CPU affinity after the game has started.
            if (settings[DOW_OPTIMIZATIONS] == 1)
            {
                // Threads could work even if application would be closed
                new Thread(() =>
                {
                    int triesCount = 0;
                    string procName = dowProcessName;

                    // We will try 30 times and then Thread will be terminated regardless
                    while (triesCount < 30)
                    {
                        Thread.Sleep(1000);
                        try
                        {
                            Process[] dow = Process.GetProcessesByName(procName);
                            dow[0].PriorityClass = ProcessPriorityClass.High;
                            dow[0].ProcessorAffinity = (IntPtr)0x0006;          // Affinity 6 means using only CPU threads 2 and 3 (6 = 0b0110)
                            break;    // We've done what we intended to do
                        }
                        catch (Exception)
                        {
                            triesCount++;
                        }
                    }
                }
                ).Start();
            }

            // Create a new thread for the fog removal which manipulates the process memory after the game has started.
            if (settings[NO_FOG] == 1)
            {
                new Thread(() =>
                {
                    int timeOutCounter = 0;
                    string procName = dowProcessName;

                    // We will try 30 times and then Thread will be terminated regardless
                    while (timeOutCounter < 30)
                    {
                        Thread.Sleep(1000);
                        try
                        {
                            Process[] dow = Process.GetProcessesByName(procName);
                            FogRemover.DisableFog(dow[0]);
                            break;     // We've done what we intended to do
                        }
                        catch (Exception)
                        {
                            timeOutCounter++;
                        }
                    }
                }
                ).Start();
            }
        }

        /// <summary>
        /// This is the checkbox that controls the starting option '-dev'. 
        /// It allows for additional debug options in-game and log files.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void DevCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (devCheckBox.Checked)
                settings[DEV] = 1;
            else
                settings[DEV] = 0;
        }

        /// <summary>
        /// This is the checkbox that controls the starting option '-nomovies'. 
        /// It prevents any movies/intros from being played.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void NomoviesCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (nomoviesCheckBox.Checked)
                settings[NO_MOVIES] = 1;
            else
                settings[NO_MOVIES] = 0;
        }

        /// <summary>
        /// This is the checkbox that controls the starting option '-forcehighpoly'. 
        /// This disabled the LOD system and will display the highes mesh detail at any distance.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void HighpolyCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (highpolyCheckBox.Checked)
                settings[FORCE_HIGH_POLY] = 1;
            else
                settings[FORCE_HIGH_POLY] = 0;
        }

        /// <summary>
        /// This is the checkbox that controls the starting option '-noprecachemodels'. 
        /// This disable preceaching of models for better performance (but teamcolors would be ignored!)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NoprecachemodelsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (noprecachemodelsCheckBox.Checked)
                settings[NO_PRECACHE_MODELS] = 1;
            else
                settings[NO_PRECACHE_MODELS] = 0;
        }

        /// <summary>
        /// This is the checkbox that sets the starting options '/high /affinity 6'. 
        /// This sets Dawn of War executable to High priority and use CPU1 and CPU2 (6 = 0110 in binary)
        /// You need at least 3 cores to make a difference (DoW would use CPU1 and CPU2, 
        /// CPU0 would be for any other application)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OptimizationsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (optimizationsCheckBox.Checked)
                settings[DOW_OPTIMIZATIONS] = 1;
            else
                settings[DOW_OPTIMIZATIONS] = 0;
        }

        /// <summary>
        /// This checkbox removes the long distance fog in from the game, without having to remove it manually from each map.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void NoFogCheckboxCheckedChanged(object sender, EventArgs e)
        {
            if (noFogCheckbox.Checked)
                settings[NO_FOG] = 1;
            else
                settings[NO_FOG] = 0;
        }

        /// <summary>
        /// This method collects and displays the list of required mods for a selected mod in order to function correctly.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void RequiredModsList_DrawItem(object sender, DrawItemEventArgs e)
        {
            // No need to draw anything if there are no required mods
            if (requiredModsList.Items.Count == 0)
                return;

            // Draw the background of the ListBox control for each item.
            e.DrawBackground();

            Brush myBrush;

            if (isInstalled[e.Index])
                myBrush = Brushes.LimeGreen;
            else
                myBrush = Brushes.Red;

            // Draw the current item text based on the current font and the custom brush settings.
            e.Graphics.DrawString(((ListBox)sender).Items[e.Index].ToString(),
                                  e.Font,
                                  myBrush,
                                  e.Bounds);

            // If the ListBox has focus, draw a focus rectangle around the selected item.
            e.DrawFocusRectangle();
        }

        /// <summary>
        /// This method opens the Mod Downloader form when the button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void DownloadButton_Click(object sender, EventArgs e)
        {
            new ModDownloaderForm(this).Show();
        }

        /// <summary>
        /// This method opens the Mod Downloader form and gives it a mod name to search for
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void FixMissingModButton_Click(object sender, EventArgs e)
        {
            if (requiredModsList.SelectedItem == null)
                return;

            string modName = requiredModsList.SelectedItem.ToString();

            int indexOfSpace = modName.IndexOf(" ");
            modName = modName.Substring(0, indexOfSpace);

            if (modName == "DXP2" || modName == "WXP" || modName == "W40k")
            {
                ThemedMessageBox.Show("You are missing one of the base modules! Reinstall the game to fix it", "Warning!");
                return;
            }

            new ModDownloaderForm(this, modName).Show();
        }

        /// <summary>
        /// This method opens the Mod Merger form when the button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ModMergeButton_Click(object sender, EventArgs e)
        {
            //TODO: ModMerger is causing problems for the noobs - we should do something! xD
            modMerger = new ModMergerForm(this);
            modMerger.Show();
        }

        /// <summary>
        /// This method opens the Settings Manager form when the button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void SettingsButton_Click(object sender, EventArgs e)
        {
            new SettingsManagerForm(this).Show();
        }

        /// <summary>
        /// This method check for errors in warnings.log and shows them to user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void CheckForErrorsButton_Click(object sender, EventArgs e)
        {
            if (File.Exists(WARNINGS_LOG))
            {
                string errors = "";

                using (StreamReader logFile = new StreamReader(WARNINGS_LOG))
                {
                    string line;

                    while ((line = logFile.ReadLine()) != null)
                    {
                        if (line.Contains("Fatal Data Error"))
                            errors += line + Environment.NewLine;
                    }
                }

                if (errors.Length > 0)
                    ThemedMessageBox.Show(errors, "Errors:");
                else
                    ThemedMessageBox.Show($"No errors were found in {WARNINGS_LOG}!", "Errors:");
            }
            else
                ThemedMessageBox.Show($"There is no {WARNINGS_LOG} file{Environment.NewLine}That means that there is no errors!", "Errors:");
        }

        /// <summary>
        /// This method checks if a file is yet still opened and thus blocked.
        /// It prevents crashes when attempting to write to files not yet closed.
        /// </summary>
        /// <param name="file"></param>
        /// <returns>bool</returns>
        bool IsFileLocked(string file)
        {
            FileStream fs = null;
            try
            {
                fs = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                // The file is unavailable because it is:
                // still being written to
                // or being processed by another thread
                // or does not exist (has already been processed)
                return true;
            }
            finally
            {
                fs?.Close();

                // Or using the old way of checking that
                //if (fs != null) fs.Close();
            }

            // File is not locked
            return false;
        }

        /// <summary>
        /// This method handles the proper toggling of the LAA flag for the Soulstorm.exe and the GraphicsConfig.exe.
        /// It can handle the cases when users have previously patched the EXE files only partially.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ButtonToggleLAA_Click(object sender, EventArgs e)
        {
            // Check if the Game is LAA Patched and fill in the Label properly
            string currentGamePath = Path.Combine(CurrentDir, CurrentGameEXE);

            if (!IsFileLocked(currentGamePath))
            {
                if (settings[IS_GOG_VERSION] == 1)
                    isGameEXELAAPatched = LAATweaks.ToggleLAA(currentGamePath);
                else
                    isGameEXELAAPatched = LAATweaksOld.ToggleLAA(currentGamePath);

                SetGameLAALabelText();
            }
        }

        /// <summary>
        /// This method can be used ouside this class to get a setting
        /// </summary>
        /// <param name="setting"></param>
        public int GetSetting(string setting)
        {
            if (settings.ContainsKey(setting))
                return settings[setting];
            else
                return -1;
        }

        /// <summary>
        /// This method can be used ouside this class to change a setting and update the GUI
        /// </summary>
        /// <param name="setting"></param>
        /// <param name="newValue"></param>
        public void ChangeSetting(string setting, int newValue)
        {
            if (setting != ACTION_STATE)
            {
                // Makes sure that newValue is in range of acceptable values. Basically a Clamp() method
                if (newValue < 0)
                    newValue = 0;
                else if (newValue > 1)
                    newValue = 1;
            }

            switch (setting)
            {
                case ACTION_STATE:
                    settings[ACTION_STATE] = newValue;
                    break;
                case DEV:
                    settings[DEV] = newValue;
                    devCheckBox.Checked = Convert.ToBoolean(newValue);
                    break;
                case NO_MOVIES:
                    settings[NO_MOVIES] = newValue;
                    nomoviesCheckBox.Checked = Convert.ToBoolean(newValue);
                    break;
                case FORCE_HIGH_POLY:
                    settings[FORCE_HIGH_POLY] = newValue;
                    highpolyCheckBox.Checked = Convert.ToBoolean(newValue);
                    break;
                case NO_PRECACHE_MODELS:
                    settings[NO_PRECACHE_MODELS] = newValue;
                    noprecachemodelsCheckBox.Checked = Convert.ToBoolean(newValue);
                    break;
                case DOW_OPTIMIZATIONS:
                    settings[DOW_OPTIMIZATIONS] = newValue;
                    optimizationsCheckBox.Checked = Convert.ToBoolean(newValue);
                    break;
                case AUTOUPDATE:
                    settings[AUTOUPDATE] = newValue;
                    break;
                case MULTITHREADED_JIT:
                    settings[MULTITHREADED_JIT] = newValue;
                    break;
                case AOT_COMPILATION:
                    settings[AOT_COMPILATION] = newValue;
                    break;
                case IS_GOG_VERSION:
                    settings[IS_GOG_VERSION] = newValue;
                    if (newValue == 1)
                        GOGRadioButton.Checked = Convert.ToBoolean(newValue);
                    else
                        SteamRadioButton.Checked = Convert.ToBoolean(newValue);
                    break;
                case DXVK_UPDATE_CHECK:
                    settings[DXVK_UPDATE_CHECK] = newValue;
                    break;
            }
        }

        /// <summary>
        /// This method opens an AboutForm
        /// </summary>
        void HomePageLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            new AboutForm(this).Show();
        }

        /// <summary>
        /// This event handles the case when the no fog checkbox is disabled, to show a tooltip why it is disabled.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ModManagerForm_MouseMove(object sender, MouseEventArgs e)
        {
            Control control = GetChildAtPoint(e.Location);
            if (control != null)
            {
                if (!control.Enabled && control == noFogCheckbox)
                {
                    string toolTipString = disabledNoFogTooltip.GetToolTip(control);
                    disabledNoFogTooltip.Show(toolTipString, control, control.Width / 2, control.Height / 2);
                    currentToolTipControl = control;
                }
            }
            else
            {
                if (currentToolTipControl != null) disabledNoFogTooltip.Hide(currentToolTipControl);
                currentToolTipControl = null;
            }
        }

        /// <summary>
        /// This event opens a new browser window of the currently selected Mod upon pressing the linked button. 
        /// This code is based on this post: https://www.codeproject.com/Questions/852563/How-to-open-file-explorer-at-given-location-in-csh
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OpenFolderButton_Click(object sender, EventArgs e)
        {
            // Maybe there are no mods
            if (AllValidModules.Count == 0)
            {
                Process.Start("explorer.exe", CurrentDir);
                return;
            }

            string pathToMod = Path.Combine(CurrentDir, AllValidModules[GetSelectedModIndex()].GetPath);
            try
            {
                if (Directory.Exists(pathToMod))
                    Process.Start("explorer.exe", pathToMod);
                else
                    ThemedMessageBox.Show($"Directory: \"{pathToMod}\" does not exist!");
            }
            catch (Exception)
            {
                ThemedMessageBox.Show($"Permission to access the folder: \"{pathToMod}\" denied! \nMake sure you have the necessary access rights!");
            }
        }

        void SteamRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (SteamRadioButton.Checked)
                settings[IS_GOG_VERSION] = 0;
        }

        void GOGRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (GOGRadioButton.Checked)
                settings[IS_GOG_VERSION] = 1;
        }

        void DxvkButton_Click(object sender, EventArgs e)
        {
            if (isDXVKInstalled)
            {
                File.Delete("dxvk.version");
                File.Delete("dxvk.conf");
                File.Delete("d3d9.dll");
                File.Delete("dxgi.dll");

                dxvkButton.Text = "Install DXVK";
                isDXVKInstalled = false;
                DXVKStatusLabel.Text = "Disabled";
                DXVKStatusLabel.ForeColor = Color.Red;

                ThemedMessageBox.Show("DXVK is disabled and deleted!", "Information:");
            }
            else
            {
                dxvkButton.Enabled = false;
                var client = new WebClient();

                try
                {
                    client.DownloadFile(DXVK_URL + "dxvk.version", "dxvk.version");
                    client.DownloadFile(DXVK_URL + "dxvk.conf", "dxvk.conf");
                    client.DownloadFile(DXVK_URL + "d3d9.dll", "d3d9.dll");
                    client.DownloadFile(DXVK_URL + "dxgi.dll", "dxgi.dll");

                    dxvkButton.Text = "Remove DXVK";
                    isDXVKInstalled = true;
                    DXVKStatusLabel.Text = "Enabled";
                    DXVKStatusLabel.ForeColor = Color.LimeGreen;

                    ThemedMessageBox.Show("DXVK is downloaded and enabled!", "Information:");
                }
                catch (Exception)
                {
                    ThemedMessageBox.Show("We can't download files!", "Warning!");
                }
                finally
                {
                    client.Dispose();
                    dxvkButton.Enabled = true;
                }
            }
        }

        private void CameraButton_Click(object sender, EventArgs e)
        {
            if (isCameraInstalled)
            {
                File.Delete(Path.Combine(cameraDirectory, "camera_high.lua"));
                File.Delete(Path.Combine(cameraDirectory, "camera_low.lua"));

                cameraButton.Text = "Install a better camera";
                isCameraInstalled = false;
                cameraStatusLabel.Text = "Disabled";
                cameraStatusLabel.ForeColor = Color.Red;

                ThemedMessageBox.Show("A Better camera is disabled and deleted!", "Information:");
            }
            else
            {
               cameraButton.Enabled = false;
                var client = new WebClient();

                try
                {
                    client.DownloadFile(CAMERA_URL + "camera_high.lua", Path.Combine(cameraDirectory, "camera_high.lua"));
                    client.DownloadFile(CAMERA_URL + "camera_low.lua", Path.Combine(cameraDirectory, "camera_low.lua"));

                    cameraButton.Text = "Remove a better camera";
                    isCameraInstalled = true;
                    cameraStatusLabel.Text = "Enabled";
                    cameraStatusLabel.ForeColor = Color.LimeGreen;

                    ThemedMessageBox.Show("A better camera is downloaded and enabled!", "Information:");
                }
                catch (Exception)
                {
                    ThemedMessageBox.Show("We can't download files!", "Warning!");
                }
                finally
                {
                    client.Dispose();
                    cameraButton.Enabled = true;
                }
            }
        }

        private void FontButton_Click(object sender, EventArgs e)
        {
            if (isFontInstalled)
            {
                Directory.Delete(Path.Combine(fontDirectory, "art"), true);
                Directory.Delete(Path.Combine(fontDirectory, "font"), true);

                fontButton.Text = "Install larger fonts";
                isFontInstalled = false;
                fontStatusLabel.Text = "Disabled";
                fontStatusLabel.ForeColor = Color.Red;

                ThemedMessageBox.Show("Larger fonts are disabled and deleted!", "Information:");
            }
            else
            {
                fontButton.Enabled = false;
                var client = new WebClient();

                try
                {
                    string artPath = Path.Combine(fontDirectory, "art\\ui\\swf");
                    if (!Directory.Exists(artPath))
                        Directory.CreateDirectory(artPath);

                    string artURL = FONT_URL + "art/ui/swf/";
                    client.DownloadFile(artURL + "font_glyphs.gfx", Path.Combine(artPath, "font_glyphs.gfx"));
                    client.DownloadFile(artURL + "fontaux.gfx", Path.Combine(artPath, "fontaux.gfx"));
                    client.DownloadFile(artURL + "fontbody.gfx", Path.Combine(artPath, "fontbody.gfx"));
                    client.DownloadFile(artURL + "fontdecor.gfx", Path.Combine(artPath, "fontdecor.gfx"));
                    client.DownloadFile(artURL + "fonthead.gfx", Path.Combine(artPath, "fonthead.gfx"));

                    string fontPath = Path.Combine(fontDirectory, "font");
                    if (!Directory.Exists(fontPath))
                        Directory.CreateDirectory(fontPath);

                    string fontURL = FONT_URL + "font/";
                    client.DownloadFile(fontURL + "albertus extra bold12.fnt", Path.Combine(fontPath, "albertus extra bold12.fnt"));
                    client.DownloadFile(fontURL + "albertus extra bold14.fnt", Path.Combine(fontPath, "albertus extra bold14.fnt"));
                    client.DownloadFile(fontURL + "albertus extra bold16.fnt", Path.Combine(fontPath, "albertus extra bold16.fnt"));
                    client.DownloadFile(fontURL + "ansnb___.ttf", Path.Combine(fontPath, "ansnb___.ttf"));
                    client.DownloadFile(fontURL + "engo.ttf", Path.Combine(fontPath, "engo.ttf"));
                    client.DownloadFile(fontURL + "engravers old english mt30.fnt", Path.Combine(fontPath, "engravers old english mt30.fnt"));
                    client.DownloadFile(fontURL + "gil_____.ttf", Path.Combine(fontPath, "gil_____.ttf"));
                    client.DownloadFile(fontURL + "gillsans_11.fnt", Path.Combine(fontPath, "gillsans_11.fnt"));
                    client.DownloadFile(fontURL + "gillsans_11b.fnt", Path.Combine(fontPath, "gillsans_11b.fnt"));
                    client.DownloadFile(fontURL + "gillsans_16.fnt", Path.Combine(fontPath, "gillsans_16.fnt"));
                    client.DownloadFile(fontURL + "gillsans_bold_16.fnt", Path.Combine(fontPath, "gillsans_bold_16.fnt"));
                    client.DownloadFile(fontURL + "quorum medium bold13.fnt", Path.Combine(fontPath, "quorum medium bold13.fnt"));
                    client.DownloadFile(fontURL + "quorum medium bold16.fnt", Path.Combine(fontPath, "quorum medium bold16.fnt"));

                    fontButton.Text = "Remove larger fonts";
                    isFontInstalled = true;
                    fontStatusLabel.Text = "Enabled";
                    fontStatusLabel.ForeColor = Color.LimeGreen;

                    ThemedMessageBox.Show("Larger fonts are downloaded and enabled!", "Information:");
                }
                catch (Exception)
                {
                    ThemedMessageBox.Show("We can't download files!", "Warning!");
                }
                finally
                {
                    client.Dispose();
                    cameraButton.Enabled = true;
                }
            }
        }
    }
}
