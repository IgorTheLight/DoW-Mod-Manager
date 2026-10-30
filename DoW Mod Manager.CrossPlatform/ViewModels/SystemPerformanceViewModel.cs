using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.ViewModels;

/// <summary>
/// MVVM port of the legacy <c>SystemPerformanceManagerForm</c>: timer resolution, Windows
/// compatibility flags and power plans. All of these are Windows-only; on Linux the capabilities are
/// empty and the window only explains why (the game runs under Proton, which manages this itself).
/// </summary>
public partial class SystemPerformanceViewModel : ViewModelBase
{
    private readonly GameInstall _install;
    private readonly ISystemPerformanceService _perf;
    private readonly IFileSystemService _files;
    private readonly DispatcherTimer? _timer;

    public PerfCapabilities Capabilities { get; }
    public IReadOnlyList<string> PowerPlans => Capabilities.AvailablePowerPlans;

    public bool IsSupported =>
        Capabilities.SupportsTimerResolution || Capabilities.SupportsCompatibilityFlags || Capabilities.SupportsPowerPlans;

    public string UnsupportedMessage =>
        "These tweaks (timer resolution, compatibility flags, power plans) only exist on Windows. " +
        "On Linux the game runs through Proton, which handles them itself — nothing to configure here.";

    [ObservableProperty] private string _minimumResolution = "—";
    [ObservableProperty] private string _maximumResolution = "—";
    [ObservableProperty] private string _currentResolution = "—";

    [NotifyCanExecuteChangedFor(nameof(ApplyCompatibilityCommand))]
    [ObservableProperty] private bool _compatibilityDirty;

    [ObservableProperty] private bool _fullscreenOptimizationsDisabled;
    [ObservableProperty] private bool _runAsAdmin;
    [ObservableProperty] private bool _highDpiAware;
    [ObservableProperty] private bool _compatibilityMode;

    [NotifyCanExecuteChangedFor(nameof(SetPowerPlanCommand))]
    [ObservableProperty] private string? _selectedPowerPlan;

    [NotifyCanExecuteChangedFor(nameof(UnlockUltimatePerformanceCommand))]
    [ObservableProperty] private bool _canUnlockUltimatePerformance;

    [ObservableProperty] private string _status = "";

    public SystemPerformanceViewModel(GameInstall install, ISystemPerformanceService perf, IFileSystemService files)
    {
        _install = install;
        _perf = perf;
        _files = files;
        Capabilities = perf.GetCapabilities();
        CanUnlockUltimatePerformance = Capabilities.CanUnlockUltimatePerformance;

        if (Capabilities.SupportsTimerResolution)
        {
            RefreshTimerResolution(includeLimits: true);
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => RefreshTimerResolution(includeLimits: false);
            _timer.Start();
        }

        if (Capabilities.SupportsCompatibilityFlags)
        {
            var profile = perf.GetCompatibilityProfile(install);
            FullscreenOptimizationsDisabled = profile.FullscreenOptimizationsDisabled;
            RunAsAdmin = profile.RunAsAdmin;
            HighDpiAware = profile.HighDpiAware;
            CompatibilityMode = profile.Win7CompatibilityMode;
            CompatibilityDirty = false;
        }

        if (Capabilities.SupportsPowerPlans)
            SelectedPowerPlan = perf.GetActivePowerPlan();
    }

    private void RefreshTimerResolution(bool includeLimits)
    {
        var info = _perf.GetTimerResolution();
        if (info is null)
            return;

        if (includeLimits)
        {
            MinimumResolution = Format(info.MinimumMs);
            MaximumResolution = Format(info.MaximumMs);
        }
        CurrentResolution = Format(info.CurrentMs);

        static string Format(double ms) => ms.ToString("0.####", CultureInfo.InvariantCulture) + " ms";
    }

    [RelayCommand]
    private void SetTimerResolution()
    {
        _perf.SetTimerResolution(0.5);
        RefreshTimerResolution(includeLimits: false);
    }

    [RelayCommand]
    private void ResetTimerResolution()
    {
        _perf.ResetTimerResolution();
        RefreshTimerResolution(includeLimits: false);
    }

    partial void OnFullscreenOptimizationsDisabledChanged(bool value) => CompatibilityDirty = true;
    partial void OnRunAsAdminChanged(bool value) => CompatibilityDirty = true;
    partial void OnHighDpiAwareChanged(bool value) => CompatibilityDirty = true;
    partial void OnCompatibilityModeChanged(bool value) => CompatibilityDirty = true;

    [RelayCommand(CanExecute = nameof(CompatibilityDirty))]
    private void ApplyCompatibility()
    {
        try
        {
            _perf.ApplyCompatibilityProfile(_install, new PerfProfile
            {
                FullscreenOptimizationsDisabled = FullscreenOptimizationsDisabled,
                RunAsAdmin = RunAsAdmin,
                HighDpiAware = HighDpiAware,
                Win7CompatibilityMode = CompatibilityMode,
            });
            CompatibilityDirty = false;
            Status = $"Compatibility flags saved for {_install.ExecutableName}.";
        }
        catch (Exception ex)
        {
            Status = $"Could not save the compatibility flags: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenGameProperties() => _files.ShowFileProperties(_install.ExecutablePath);

    private bool CanSetPowerPlan => SelectedPowerPlan is not null;

    [RelayCommand(CanExecute = nameof(CanSetPowerPlan))]
    private void SetPowerPlan()
    {
        try
        {
            _perf.SetPowerPlan(SelectedPowerPlan!);
            Status = $"Power plan set to {SelectedPowerPlan}.";
        }
        catch (Exception ex)
        {
            Status = $"Could not change the power plan: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanUnlockUltimatePerformance))]
    private void UnlockUltimatePerformance()
    {
        _perf.UnlockUltimatePerformancePlan();
        CanUnlockUltimatePerformance = false;
        Status = "Ultimate Performance plan unlocked. Reopen this window to select it.";
    }

    [RelayCommand]
    private void OpenPowerOptions() => _perf.OpenPowerOptions();

    [RelayCommand]
    private void Close() => RequestClose();

    public override void OnClosed() => _timer?.Stop();
}
