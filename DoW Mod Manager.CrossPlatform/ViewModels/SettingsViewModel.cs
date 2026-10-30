using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Services;
using K = DoW_Mod_Manager.Services.GameSettingsKeys;

namespace DoW_Mod_Manager.ViewModels;

/// <summary>
/// MVVM port of the legacy <c>SettingsManagerForm</c>: edits the game's <c>Local.ini</c>, hardware
/// cursor, player profiles and per-profile <c>playercfg.lua</c>. Every control is a thin property over
/// the <see cref="GameSettings"/> bag; nothing is written until SAVE.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly GameInstall _install;
    private readonly IGameSettingsService _service;
    private readonly IGpuInfoService _gpus;
    private readonly IDialogService _dialogs;
    private readonly IFeatureViewModelFactory _factory;
    private readonly Action<LaunchFlagChange> _applyLaunchFlags;

    private GameSettings _s;
    private LaunchFlagChange? _pendingLaunchFlags;
    private bool _loading;

    public static IReadOnlyList<string> LoginAttemptOptions { get; } = ["0", "1", "2", "3", "4", "5", "6"];

    public static IReadOnlyList<string> Resolutions { get; } =
    [
        "4096×2160", "3840×2400", "3840×2160", "2560×1600", "2560×1440", "1920×1200", "1920×1080",
        "1680×1050", "1600×1024", "1600×900", "1440×900", "1400×1050", "1366×768", "1360×768",
        "1280×1024", "1280×960", "1280×800", "1280×768", "1280×720", "1176×664", "1152×864",
        "1024×768", "800×600",
    ];

    public static IReadOnlyList<string> RefreshRates { get; } =
        ["Auto", "144 Hz", "120 Hz", "90 Hz", "85 Hz", "75 Hz", "60 Hz", "59 Hz"];

    public static IReadOnlyList<string> ColorDepths { get; } =
        ["16 bit", "24 bit (HighColor - experimental)", "32 bit (TrueColor)"];

    public static IReadOnlyList<string> Renderers { get; } = ["Dx9 : Hardware TnL"];
    public static IReadOnlyList<string> LowMediumHigh { get; } = ["Low", "Medium", "High"];
    public static IReadOnlyList<string> NoneToHigh { get; } = ["None", "Low", "Medium", "High"];

    public static IReadOnlyList<string> ShadowLevels { get; } =
        ["None", "Low (Blob)", "Medium (Blob + Map)", "High (Blob + Map + Volume)"];

    public static IReadOnlyList<string> SoundChannelOptions { get; } = ["Low (16)", "Medium (32)", "High (64)"];

    private static readonly string[] ColorDepthValues = ["16", "24", "32"];
    private static readonly string[] SoundChannelValues = ["16", "32", "64"];

    public ObservableCollection<PlayerProfile> Profiles { get; } = [];
    public ObservableCollection<string> Videocards { get; } = [];

    [NotifyCanExecuteChangedFor(nameof(RenameProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    [ObservableProperty] private PlayerProfile? _selectedProfile;

    [NotifyCanExecuteChangedFor(nameof(CreateProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameProfileCommand))]
    [ObservableProperty] private string _newPlayerName = "";

    [NotifyPropertyChangedFor(nameof(CloseLabel))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [ObservableProperty] private bool _isDirty;

    [ObservableProperty] private string _status = "";

    public string CloseLabel => IsDirty ? "CANCEL" : "CLOSE";

    public SettingsViewModel(
        GameInstall install,
        Action<LaunchFlagChange> applyLaunchFlags,
        IGameSettingsService service,
        IGpuInfoService gpus,
        IDialogService dialogs,
        IFeatureViewModelFactory factory)
    {
        _install = install;
        _applyLaunchFlags = applyLaunchFlags;
        _service = service;
        _gpus = gpus;
        _dialogs = dialogs;
        _factory = factory;

        try
        {
            _s = _service.Read(install);
            Status = $"Settings of {install.UserDataDir}";
        }
        catch (Exception ex)
        {
            _s = _service.GetDefaults(install);
            Status = $"Could not read the game settings, showing defaults: {ex.Message}";
        }

        LoadProfiles();
        _ = LoadVideocardsAsync();
    }

    // ---------------------------------------------------------------- helpers

    private string Get(string key) => _s[key];

    private void Set(string key, string value, [CallerMemberName] string? property = null)
    {
        if (_s[key] == value)
            return;
        _s[key] = value;
        OnPropertyChanged(property);
        MarkDirty();
    }

    private bool GetBool(string key) => _s[key] == "1";
    private void SetBool(string key, bool value, [CallerMemberName] string? property = null) =>
        Set(key, value ? "1" : "0", property);

    private int GetInt(string key) => int.TryParse(_s[key], out int n) ? n : 0;

    /// <summary>Combo boxes transiently report -1 while (re)binding; never store that.</summary>
    private void SetIndex(string key, int index, [CallerMemberName] string? property = null)
    {
        if (index >= 0)
            Set(key, index.ToString(Invariant), property);
    }

    /// <summary>playercfg.lua stores ratios with five decimals (legacy "F5" format).</summary>
    private int GetPercent(string key) =>
        double.TryParse(_s[key], NumberStyles.Float, Invariant, out double d) ? (int)Math.Round(d * 100) : 0;

    private void SetPercent(string key, double percent, [CallerMemberName] string? property = null) =>
        Set(key, (Math.Round(percent) / 100d).ToString("F5", Invariant), property);

    private void MarkDirty()
    {
        if (!_loading)
            IsDirty = true;
    }

    /// <summary>Re-reads every bound property after a bulk change (preset, defaults, profile switch).</summary>
    private void RefreshAll()
    {
        _loading = true;
        try
        {
            foreach (var property in typeof(SettingsViewModel).GetProperties())
                OnPropertyChanged(property.Name);
        }
        finally
        {
            _loading = false;
        }
    }

    // ---------------------------------------------------------------- General tab

    public bool ParentalControl { get => GetBool(K.ParentalControl); set => SetBool(K.ParentalControl, value); }
    public bool InversePan { get => GetBool(K.InvertPan); set => SetBool(K.InvertPan, value); }
    public bool InverseDeclination { get => GetBool(K.InvertDeclination); set => SetBool(K.InvertDeclination, value); }

    /// <summary>Slider 10..210 (legacy track bar), stored as a ratio.</summary>
    public double ScrollRate { get => GetPercent(K.ScrollRate); set => SetPercent(K.ScrollRate, value); }

    public string? LoginAttempts
    {
        get => Get(K.RlSsoNumTimesShown);
        set { if (value is not null) Set(K.RlSsoNumTimesShown, value); }
    }

    private void LoadProfiles(string? select = null)
    {
        _loading = true;
        try
        {
            Profiles.Clear();
            foreach (var p in _service.GetProfiles(_install.UserDataDir))
                Profiles.Add(p);

            string wanted = select ?? Get(K.PlayerProfile);
            SelectedProfile = Profiles.FirstOrDefault(p => p.ProfileName == wanted) ?? Profiles.FirstOrDefault();
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnSelectedProfileChanged(PlayerProfile? value)
    {
        if (value is null || value.ProfileName == Get(K.PlayerProfile) && _loading)
            return;

        _s[K.PlayerProfile] = value.ProfileName;
        _service.ReadPlayerConfig(_install.UserDataDir, value.ProfileName, _s);
        bool wasLoading = _loading;
        RefreshAll();
        if (!wasLoading)
            IsDirty = true;
    }

    private bool CanCreateProfile => NewPlayerName.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanCreateProfile))]
    private async Task CreateProfileAsync()
    {
        try
        {
            var created = _service.CreateProfile(_install.UserDataDir, NewPlayerName.Trim());
            NewPlayerName = "";
            LoadProfiles(created.ProfileName);
            IsDirty = true;
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync(ex.Message, "Error:");
        }
    }

    private bool CanRenameProfile => SelectedProfile is not null && NewPlayerName.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanRenameProfile))]
    private async Task RenameProfileAsync()
    {
        try
        {
            string profile = SelectedProfile!.ProfileName;
            _service.RenameProfile(_install.UserDataDir, profile, NewPlayerName.Trim());
            NewPlayerName = "";
            LoadProfiles(profile);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync(ex.Message, "Error:");
        }
    }

    private bool CanDeleteProfile => SelectedProfile is not null;

    [RelayCommand(CanExecute = nameof(CanDeleteProfile))]
    private async Task DeleteProfileAsync()
    {
        var profile = SelectedProfile!;
        if (!await _dialogs.ConfirmAsync(
                $"Delete the profile of {profile.PlayerName} ({profile.ProfileName}) with all its saves?",
                "Delete profile"))
            return;

        try
        {
            _service.DeleteProfile(_install.UserDataDir, profile.ProfileName);
            LoadProfiles();
            IsDirty = true;
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync(ex.Message, "Error:");
        }
    }

    // ---------------------------------------------------------------- Video tab

    private async Task LoadVideocardsAsync()
    {
        IReadOnlyList<GpuInfo> gpus = [];
        try
        {
            gpus = await _gpus.GetGpusAsync();
        }
        catch
        {
            // Listing GPUs is best effort; fall back to the default adapter.
        }

        Videocards.Clear();
        foreach (var gpu in gpus)
            Videocards.Add(gpu.Description);
        if (Videocards.Count == 0)
            Videocards.Add("Default adapter");

        _loading = true;
        try
        {
            if (GetInt(K.ScreenAdapter) >= Videocards.Count)
                _s[K.ScreenAdapter] = "0";
            OnPropertyChanged(nameof(ActiveVideocardIndex));
        }
        finally
        {
            _loading = false;
        }
    }

    public int ActiveVideocardIndex { get => GetInt(K.ScreenAdapter); set => SetIndex(K.ScreenAdapter, value); }

    public string? ScreenResolution
    {
        get => $"{Get(K.ScreenWidth)}×{Get(K.ScreenHeight)}";
        set
        {
            if (value is null || value.Split('×') is not [var width, var height])
                return;
            Set(K.ScreenWidth, width);
            Set(K.ScreenHeight, height);
        }
    }

    public string? RefreshRate
    {
        get => Get(K.ScreenRefresh) == "0" ? "Auto" : Get(K.ScreenRefresh) + " Hz";
        set
        {
            if (value is not null)
                Set(K.ScreenRefresh, value == "Auto" ? "0" : value.Split(' ')[0]);
        }
    }

    public int ColorDepthIndex
    {
        get => Array.IndexOf(ColorDepthValues, Get(K.ScreenDepth));
        set { if (value >= 0) Set(K.ScreenDepth, ColorDepthValues[value]); }
    }

    public string? Renderer
    {
        get => Get(K.ScreenDevice);
        set { if (value is not null) Set(K.ScreenDevice, value); }
    }

    /// <summary>Slider 5..15 (legacy track bar). Stored as the integer the game expects.</summary>
    public double Gamma
    {
        get => GetInt(K.ScreenGamma);
        set => Set(K.ScreenGamma, ((int)Math.Round(value)).ToString(Invariant));
    }

    /// <summary>Inverted <c>screennovsync</c>.</summary>
    public bool VSync { get => !GetBool(K.ScreenNoVsync); set => SetBool(K.ScreenNoVsync, !value); }

    public bool Windowed { get => GetBool(K.ScreenWindowed); set => SetBool(K.ScreenWindowed, value); }
    public bool Antialiasing { get => GetBool(K.ScreenAntialias); set => SetBool(K.ScreenAntialias, value); }
    public int TextureDetail { get => GetInt(K.TextureDetail); set => SetIndex(K.TextureDetail, value); }
    public int ModelDetail { get => GetInt(K.ModelDetail); set => SetIndex(K.ModelDetail, value); }
    public int TerrainDetail { get => GetInt(K.TerrainEnableFowBlur); set => SetIndex(K.TerrainEnableFowBlur, value); }
    public bool HighResTextures { get => GetBool(K.FullResTeamColour); set => SetBool(K.FullResTeamColour, value); }
    public bool HardwareCursor { get => GetBool(K.AllowHwCursor); set => SetBool(K.AllowHwCursor, value); }
    public int WorldEvents { get => GetInt(K.EventDetailLevel); set => SetIndex(K.EventDetailLevel, value); }
    public int EffectsDetail { get => GetInt(K.FxDetailLevel); set => SetIndex(K.FxDetailLevel, value); }
    public int PersistentBodies { get => GetInt(K.PersistentBodies); set => SetIndex(K.PersistentBodies, value); }
    public int PersistentScarring { get => GetInt(K.PersistentDecals); set => SetIndex(K.PersistentDecals, value); }
    public int DynamicLights { get => GetInt(K.DynamicLights); set => SetIndex(K.DynamicLights, value); }
    public bool Full3DCamera { get => GetBool(K.CameraDetail); set => SetBool(K.CameraDetail, value); }
    public bool UnitsOcclusion { get => GetBool(K.UnitOcclusion); set => SetBool(K.UnitOcclusion, value); }

    /// <summary>Shadows are three cumulative flags: blob, then map, then volume.</summary>
    public int ShadowsDetail
    {
        get => !GetBool(K.ShadowBlob) ? 0 : !GetBool(K.ShadowMap) ? 1 : !GetBool(K.ShadowVolume) ? 2 : 3;
        set
        {
            if (value < 0)
                return;
            SetBool(K.ShadowBlob, value >= 1, nameof(ShadowsDetail));
            SetBool(K.ShadowMap, value >= 2, nameof(ShadowsDetail));
            SetBool(K.ShadowVolume, value >= 3, nameof(ShadowsDetail));
        }
    }

    [RelayCommand]
    private void ApplyGraphicsPreset(GraphicsPreset preset)
    {
        _pendingLaunchFlags = GameSettingsService.ApplyGraphicsPreset(_s, preset);
        RefreshAll();
        IsDirty = true;
        Status = preset switch
        {
            GraphicsPreset.UltraPlus => "ULTRA+ also enables -forcehighpoly when you save.",
            GraphicsPreset.LowMinus => "LOW- also enables -noprecachemodels when you save.",
            _ => $"{preset} preset applied — press SAVE to keep it.",
        };
    }

    // ---------------------------------------------------------------- Audio tab

    public bool SoundEnabled { get => GetBool(K.SoundEnabled); set => SetBool(K.SoundEnabled, value); }

    /// <summary>Inverted <c>sound_limitsamples</c>.</summary>
    public bool RandomizedSounds { get => !GetBool(K.SoundLimitSamples); set => SetBool(K.SoundLimitSamples, !value); }

    public int SoundQuality { get => GetInt(K.SoundQuality); set => SetIndex(K.SoundQuality, value); }

    public int SoundChannelsIndex
    {
        get => Array.IndexOf(SoundChannelValues, Get(K.SoundNrChannels));
        set { if (value >= 0) Set(K.SoundNrChannels, SoundChannelValues[value]); }
    }

    public double MusicVolume { get => GetPercent(K.VolumeMusic); set => SetPercent(K.VolumeMusic, value); }
    public double VoiceVolume { get => GetPercent(K.VolumeVoice); set => SetPercent(K.VolumeVoice, value); }
    public double EffectsVolume { get => GetPercent(K.VolumeSfx); set => SetPercent(K.VolumeSfx, value); }
    public double AmbientVolume { get => GetPercent(K.VolumeAmbient); set => SetPercent(K.VolumeAmbient, value); }

    [RelayCommand]
    private void ApplyAudioPreset(AudioPreset preset)
    {
        GameSettingsService.ApplyAudioPreset(_s, preset);
        RefreshAll();
        IsDirty = true;
    }

    // ---------------------------------------------------------------- Footer

    [RelayCommand(CanExecute = nameof(IsDirty))]
    private async Task SaveAsync()
    {
        try
        {
            _service.Write(_install, _s);
            if (_pendingLaunchFlags is not null)
            {
                _applyLaunchFlags(_pendingLaunchFlags);
                _pendingLaunchFlags = null;
            }
            IsDirty = false;
            Status = "Settings saved.";
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync(ex.Message, "Could not save the settings");
        }
    }

    [RelayCommand]
    private void Defaults()
    {
        string profile = Get(K.PlayerProfile);
        _s = _service.GetDefaults(_install);
        _s[K.PlayerProfile] = profile;
        _service.ReadPlayerConfig(_install.UserDataDir, profile, _s);
        _pendingLaunchFlags = new LaunchFlagChange(ForceHighPoly: false, NoPrecacheModels: false);
        RefreshAll();
        IsDirty = true;
    }

    [RelayCommand]
    private void OpenFontsManager() =>
        _dialogs.ShowWindow(_factory.CreateFontsManager(_install, Get(K.ScreenWidth)));

    [RelayCommand]
    private void OpenSystemPerformance() =>
        _dialogs.ShowWindow(_factory.CreateSystemPerformance(_install));

    [RelayCommand]
    private void Close() => RequestClose();
}
