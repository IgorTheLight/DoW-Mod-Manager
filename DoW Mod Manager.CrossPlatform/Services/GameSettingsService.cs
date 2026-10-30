using System.Text;
using DoW_Mod_Manager.Models;
using K = DoW_Mod_Manager.Services.GameSettingsKeys;

namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Cross-platform port of the file I/O of the legacy <c>SettingsManagerForm</c>. Paths are
    /// resolved case-insensitively (see <see cref="GamePaths"/>) and profiles are addressed by their
    /// directory name instead of their position in a combo box.
    /// </summary>
    public sealed class GameSettingsService : IGameSettingsService
    {
        private const string ProfilePrefix = "Profile";
        private const string NameDat = "name.dat";
        private const string PlayerConfig = "playercfg.lua";

        /// <summary>The <c>Local.ini</c> keys, in write order.</summary>
        private static readonly string[] LocalIniKeys =
        [
            K.CameraDetail, K.CurrentMod, K.DynamicLights, K.EventDetailLevel, K.ForceWatchMovies,
            K.FullResTeamColour, K.FxDetailLevel, K.ModelDetail, K.ParentalControl, K.PersistentBodies,
            K.PersistentDecals, K.PlayerProfile, K.RlSsoNumTimesShown, K.ScreenAdapter, K.ScreenAntialias,
            K.ScreenDepth, K.ScreenDevice, K.ScreenGamma, K.ScreenHeight, K.ScreenNoVsync, K.ScreenRefresh,
            K.ScreenWidth, K.ScreenWindowed, K.ShadowBlob, K.ShadowMap, K.ShadowVolume, K.SoundEnabled,
            K.SoundLimitSamples, K.SoundNrChannels, K.SoundQuality, K.TerrainEnableFowBlur, K.TextureDetail,
            K.TotalMatches, K.UnitOcclusion,
        ];

        /// <summary>The <c>playercfg.lua</c> keys, in file order.</summary>
        private static readonly string[] PlayerConfigKeys =
        [
            K.InvertDeclination, K.InvertPan, K.ScrollRate,
            K.VolumeAmbient, K.VolumeMusic, K.VolumeSfx, K.VolumeVoice,
        ];

        private static readonly Func<string, bool> Bool = v => v is "0" or "1";
        private static Func<string, bool> Range(int min, int max) =>
            v => int.TryParse(v, out int n) && n >= min && n <= max;

        /// <summary>
        /// Values accepted when reading <c>Local.ini</c>; anything else keeps the default. Resolution
        /// and refresh rate accept any positive number: the legacy whitelists rejected several values
        /// its own UI offered (1360, 864, 90 Hz…).
        /// </summary>
        private static readonly Dictionary<string, Func<string, bool>> LocalIniValidators = new()
        {
            [K.CameraDetail] = Bool,
            [K.CurrentMod] = v => v.Length > 0,
            [K.DynamicLights] = Range(0, 3),
            [K.EventDetailLevel] = Range(0, 2),
            [K.ForceWatchMovies] = Bool,
            [K.FullResTeamColour] = Bool,
            [K.FxDetailLevel] = Range(0, 2),
            [K.ModelDetail] = Range(0, 2),
            [K.ParentalControl] = Bool,
            [K.PersistentBodies] = Range(0, 3),
            [K.PersistentDecals] = Range(0, 2),
            [K.PlayerProfile] = v => v.Contains(ProfilePrefix),
            [K.RlSsoNumTimesShown] = Range(0, int.MaxValue),
            [K.ScreenAdapter] = Range(0, int.MaxValue),
            [K.ScreenAntialias] = Bool,
            [K.ScreenDepth] = v => v is "16" or "24" or "32",
            [K.ScreenDevice] = v => v == "Dx9 : Hardware TnL",
            [K.ScreenGamma] = Range(0, 30),
            [K.ScreenHeight] = Range(1, int.MaxValue),
            [K.ScreenNoVsync] = Bool,
            [K.ScreenRefresh] = Range(0, 1000),
            [K.ScreenWidth] = Range(1, int.MaxValue),
            [K.ScreenWindowed] = Bool,
            [K.ShadowBlob] = Bool,
            [K.ShadowMap] = Bool,
            [K.ShadowVolume] = Bool,
            [K.SoundEnabled] = Bool,
            [K.SoundLimitSamples] = Bool,
            [K.SoundNrChannels] = Range(1, 64),
            [K.SoundQuality] = Range(0, 2),
            [K.TerrainEnableFowBlur] = Range(0, 2),
            [K.TextureDetail] = Range(0, 2),
            [K.TotalMatches] = Range(0, int.MaxValue),
            [K.UnitOcclusion] = Bool,
        };

        /// <inheritdoc />
        public GameSettings GetDefaults() => GetDefaults(baseModule: "W40k");

        /// <inheritdoc />
        public GameSettings GetDefaults(GameInstall install) => GetDefaults(install.BaseGameModuleName);

        private static GameSettings GetDefaults(string baseModule) => new(new Dictionary<string, string>
        {
            [K.AllowHwCursor] = "1",

            [K.CameraDetail] = "1",
            [K.CurrentMod] = baseModule,
            [K.DynamicLights] = "2",
            [K.EventDetailLevel] = "2",
            [K.ForceWatchMovies] = "1",
            [K.FullResTeamColour] = "0",
            [K.FxDetailLevel] = "2",
            [K.ModelDetail] = "2",
            [K.ParentalControl] = "0",
            [K.PersistentBodies] = "0",
            [K.PersistentDecals] = "0",
            [K.PlayerProfile] = "Profile1",
            [K.RlSsoNumTimesShown] = "1",
            [K.ScreenAdapter] = "0",
            [K.ScreenAntialias] = "0",
            [K.ScreenDepth] = "32",
            [K.ScreenDevice] = "Dx9 : Hardware TnL",
            [K.ScreenGamma] = "10",
            [K.ScreenHeight] = "768",
            [K.ScreenNoVsync] = "0",
            [K.ScreenRefresh] = "0",
            [K.ScreenWidth] = "1024",
            [K.ScreenWindowed] = "0",
            [K.ShadowBlob] = "0",
            [K.ShadowMap] = "0",
            [K.ShadowVolume] = "0",
            [K.SoundEnabled] = "1",
            [K.SoundLimitSamples] = "0",
            [K.SoundNrChannels] = "32",
            [K.SoundQuality] = "2",
            [K.TerrainEnableFowBlur] = "0",
            [K.TextureDetail] = "1",
            [K.TotalMatches] = "0",
            [K.UnitOcclusion] = "0",

            [K.InvertDeclination] = "0",
            [K.InvertPan] = "1",
            [K.ScrollRate] = "1",
            [K.VolumeAmbient] = "0.75",
            [K.VolumeMusic] = "0.75",
            [K.VolumeSfx] = "0.75",
            [K.VolumeVoice] = "0.75",
        });

        /// <inheritdoc />
        public GameSettings Read(string installDir) => Read(installDir, installDir, GetDefaults());

        /// <inheritdoc />
        public GameSettings Read(GameInstall install) => Read(install.InstallDir, install.UserDataDir, GetDefaults(install));

        /// <summary>
        /// The driver config lives in the install dir; <c>Local.ini</c> and the profiles in the
        /// per-user data dir (the same directory for the classic games).
        /// </summary>
        private GameSettings Read(string installDir, string userDataDir, GameSettings settings)
        {
            ReadDriverConfig(installDir, settings);
            ReadLocalIni(userDataDir, settings);

            var profiles = GetProfiles(userDataDir);
            if (profiles.Count > 0 && profiles.All(p => p.ProfileName != settings[K.PlayerProfile]))
                settings[K.PlayerProfile] = profiles[0].ProfileName;

            ReadPlayerConfig(userDataDir, settings[K.PlayerProfile], settings);
            return settings;
        }

        private static void ReadDriverConfig(string installDir, GameSettings settings)
        {
            string path = GamePaths.DriverConfig(installDir);
            if (!File.Exists(path))
                return;

            // A config file without the line means the hardware cursor is off.
            string? line = File.ReadLines(path).FirstOrDefault(l => l.StartsWith(K.AllowHwCursor));
            string[] parts = line?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
            settings[K.AllowHwCursor] = parts.Length > 1 ? parts[1] : "0";
        }

        private static void ReadLocalIni(string userDataDir, GameSettings settings)
        {
            string path = GamePaths.LocalIni(userDataDir);
            if (!File.Exists(path))
                return;

            foreach (string raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');

                // There must be exactly one "=" in the line.
                if (eq <= 0 || eq != line.LastIndexOf('='))
                    continue;

                string key = line[..eq];
                string value = line[(eq + 1)..];

                if (LocalIniValidators.TryGetValue(key, out var isValid) && isValid(value))
                    settings[key] = value;
            }
        }

        /// <inheritdoc />
        public void ReadPlayerConfig(string userDataDir, string profileName, GameSettings settings)
        {
            string path = GamePaths.Resolve(GamePaths.Profiles(userDataDir), profileName, PlayerConfig);
            if (!File.Exists(path))
                return;

            foreach (string raw in File.ReadLines(path))
            {
                if (!raw.EndsWith(','))
                    continue;

                string line = raw.Replace(" ", "").Replace("\t", "");
                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                string key = line[..eq];
                if (PlayerConfigKeys.Contains(key))
                    settings[key] = line[(eq + 1)..^1];
            }
        }

        /// <inheritdoc />
        public void Write(string installDir, GameSettings settings) => Write(installDir, installDir, settings);

        /// <inheritdoc />
        public void Write(GameInstall install, GameSettings settings) => Write(install.InstallDir, install.UserDataDir, settings);

        private static void Write(string installDir, string userDataDir, GameSettings settings)
        {
            WriteLocalIni(userDataDir, settings);
            WriteDriverConfig(installDir, settings);
            WritePlayerConfig(userDataDir, settings);
        }

        /// <summary>
        /// Merges the settings into an existing <c>Local.ini</c> instead of rewriting it: lines the
        /// manager does not manage (e.g. the Definitive Edition's <c>screenvsync</c>) are kept
        /// verbatim, and a value the validators rejected on read (DE's <c>screenwindowed=2</c>, a
        /// real adapter name in <c>screendevice</c>) is kept unless the user changed it from the
        /// default it was read as. Missing keys are only added when they differ from the default,
        /// or when the file is created.
        /// </summary>
        private static void WriteLocalIni(string userDataDir, GameSettings settings)
        {
            string path = GamePaths.LocalIni(userDataDir);
            GameSettings defaults = GetDefaults(baseModule: settings[K.CurrentMod]);

            if (!File.Exists(path))
            {
                var sb = new StringBuilder("[global]").AppendLine();
                foreach (string key in LocalIniKeys)
                    sb.Append(key).Append('=').Append(settings[key]).AppendLine();

                WindowsText.WriteAllText(path, sb.ToString());
                return;
            }

            var lines = File.ReadAllLines(path).ToList();
            var written = new HashSet<string>();

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0 || eq != line.LastIndexOf('='))
                    continue;

                string key = line[..eq];
                if (!LocalIniKeys.Contains(key) || !written.Add(key))
                    continue;

                string existing = line[(eq + 1)..];
                bool readAsDefault = !LocalIniValidators[key](existing);
                if (readAsDefault && settings[key] == defaults[key])
                    continue;

                lines[i] = $"{key}={settings[key]}";
            }

            if (!lines.Any(l => l.Trim().Equals("[global]", StringComparison.OrdinalIgnoreCase)))
                lines.Insert(0, "[global]");

            foreach (string key in LocalIniKeys)
            {
                if (!written.Contains(key) && settings[key] != defaults[key])
                    lines.Add($"{key}={settings[key]}");
            }

            WindowsText.WriteAllLines(path, lines);
        }

        private static void WriteDriverConfig(string installDir, GameSettings settings)
        {
            string path = GamePaths.DriverConfig(installDir);
            string entry = $"{K.AllowHwCursor} {settings[K.AllowHwCursor]}";

            if (!File.Exists(path))
            {
                // Only create the file inside an existing Drivers directory: a fresh install that
                // never ran the game has no driver config yet and the game will generate it.
                if (Directory.Exists(Path.GetDirectoryName(path)))
                    File.WriteAllText(path, entry + WindowsText.NewLine);
                return;
            }

            var lines = File.ReadAllLines(path).ToList();
            int index = lines.FindIndex(l => l.StartsWith(K.AllowHwCursor));
            if (index >= 0)
                lines[index] = entry;
            else
                lines.Add(entry);

            WindowsText.WriteAllLines(path, lines);
        }

        private static void WritePlayerConfig(string userDataDir, GameSettings settings)
        {
            string profileDir = GamePaths.Resolve(GamePaths.Profiles(userDataDir), settings[K.PlayerProfile]);
            if (!Directory.Exists(profileDir))
                return;

            string path = GamePaths.Resolve(profileDir, PlayerConfig);
            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].EndsWith(','))
                        continue;

                    string key = lines[i].Trim().Split('=', 2)[0].Trim();
                    if (PlayerConfigKeys.Contains(key))
                        lines[i] = $"\t{key} = {settings[key]},";
                }
                WindowsText.WriteAllLines(path, lines);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Controls = ").AppendLine("{");
            foreach (string key in PlayerConfigKeys.Take(3))
                sb.AppendLine($"\t{key} = {settings[key]},");
            sb.AppendLine("}").AppendLine("Sound = ").AppendLine("{");
            foreach (string key in PlayerConfigKeys.Skip(3))
                sb.AppendLine($"\t{key} = {settings[key]},");
            sb.AppendLine("}")
              .AppendLine("player_preferences = ")
              .AppendLine("{")
              .AppendLine("\tcampaign_played_disorder = false,")
              .AppendLine("\tcampaign_played_order = false,")
              .AppendLine("\tforce_name = \"Blood Ravens\",")
              .AppendLine("\trace = \"space_marine_race\",")
              .AppendLine("}");
            WindowsText.WriteAllText(path, sb.ToString());
        }

        /// <inheritdoc />
        public IReadOnlyList<PlayerProfile> GetProfiles(string userDataDir)
        {
            string root = GamePaths.Profiles(userDataDir);
            if (!Directory.Exists(root))
                return [];

            return Directory.GetDirectories(root)
                .Select(dir => new PlayerProfile(Path.GetFileName(dir), ReadPlayerName(dir)))
                .OrderBy(p => ProfileIndex(p.ProfileName))
                .ThenBy(p => p.ProfileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <inheritdoc />
        public PlayerProfile CreateProfile(string userDataDir, string playerName)
        {
            string root = GamePaths.Profiles(userDataDir);
            Directory.CreateDirectory(root);

            var used = GetProfiles(userDataDir).Select(p => ProfileIndex(p.ProfileName)).ToHashSet();
            int index = 1;
            while (used.Contains(index))
                index++;

            string name = ProfilePrefix + index;
            string dir = Path.Combine(root, name);
            Directory.CreateDirectory(dir);
            WritePlayerName(dir, playerName);

            return new PlayerProfile(name, playerName);
        }

        /// <inheritdoc />
        public void RenameProfile(string userDataDir, string profileName, string newPlayerName) =>
            WritePlayerName(GamePaths.Resolve(GamePaths.Profiles(userDataDir), profileName), newPlayerName);

        /// <inheritdoc />
        public void DeleteProfile(string userDataDir, string profileName)
        {
            string dir = GamePaths.Resolve(GamePaths.Profiles(userDataDir), profileName);
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }

        /// <summary>
        /// Applies a graphics preset (legacy LOW- … ULTRA+ buttons) and returns the launch-option
        /// change it implies.
        /// </summary>
        public static LaunchFlagChange ApplyGraphicsPreset(GameSettings s, GraphicsPreset preset)
        {
            // Columns: camera, lights, events, teamcolour, fx, model, bodies, decals, AA, depth,
            //          blob, map, volume, terrain, texture
            string[] v = preset switch
            {
                GraphicsPreset.LowMinus or GraphicsPreset.Low =>
                    ["0", "0", "0", "0", "0", "0", "0", "0", "0", "16", "0", "0", "0", "0", "0"],
                GraphicsPreset.Medium =>
                    ["1", "1", "1", "0", "1", "1", "1", "1", "0", "32", "1", "0", "0", "1", "1"],
                GraphicsPreset.High =>
                    ["1", "2", "2", "0", "2", "2", "2", "2", "0", "32", "1", "1", "0", "2", "2"],
                GraphicsPreset.Ultra =>
                    ["1", "3", "2", "0", "2", "2", "3", "2", "0", "32", "1", "1", "1", "2", "2"],
                _ =>
                    ["1", "3", "2", "1", "2", "2", "3", "2", "1", "32", "1", "1", "1", "2", "2"],
            };

            string[] keys =
            [
                K.CameraDetail, K.DynamicLights, K.EventDetailLevel, K.FullResTeamColour, K.FxDetailLevel,
                K.ModelDetail, K.PersistentBodies, K.PersistentDecals, K.ScreenAntialias, K.ScreenDepth,
                K.ShadowBlob, K.ShadowMap, K.ShadowVolume, K.TerrainEnableFowBlur, K.TextureDetail,
            ];
            for (int i = 0; i < keys.Length; i++)
                s[keys[i]] = v[i];

            return new LaunchFlagChange(
                ForceHighPoly: preset == GraphicsPreset.UltraPlus,
                NoPrecacheModels: preset == GraphicsPreset.LowMinus);
        }

        /// <summary>Applies an audio preset (legacy LOW / MEDIUM / HIGH audio buttons).</summary>
        public static void ApplyAudioPreset(GameSettings s, AudioPreset preset)
        {
            s[K.SoundEnabled] = "1";
            (s[K.SoundLimitSamples], s[K.SoundNrChannels], s[K.SoundQuality]) = preset switch
            {
                AudioPreset.Low => ("1", "16", "0"),
                AudioPreset.Medium => ("0", "32", "1"),
                _ => ("0", "64", "2"),
            };
        }

        private static int ProfileIndex(string profileName) =>
            profileName.StartsWith(ProfilePrefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(profileName.AsSpan(ProfilePrefix.Length), out int n) ? n : int.MaxValue;

        /// <summary>
        /// <c>name.dat</c> is UTF-16. The game may write it without a BOM, which
        /// <see cref="File.ReadAllText(string)"/> would misread as UTF-8.
        /// </summary>
        private static string ReadPlayerName(string profileDir)
        {
            string path = GamePaths.Resolve(profileDir, NameDat);
            if (!File.Exists(path))
                return "Player";

            byte[] bytes = File.ReadAllBytes(path);
            bool hasBom = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE;
            bool looksUtf16 = hasBom || (bytes.Length >= 2 && bytes[1] == 0);

            string name = looksUtf16
                ? Encoding.Unicode.GetString(bytes, hasBom ? 2 : 0, bytes.Length - (hasBom ? 2 : 0))
                : Encoding.UTF8.GetString(bytes);

            return name.TrimEnd('\0', '\r', '\n');
        }

        private static void WritePlayerName(string profileDir, string playerName) =>
            File.WriteAllText(GamePaths.Resolve(profileDir, NameDat), playerName, Encoding.Unicode);
    }
}
