using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Platform.Windows;

/// <summary>
/// Windows system-performance service. Ports the legacy <c>WinApiCalls</c> NT timer-resolution
/// P/Invoke and the <c>SystemPerformanceManagerForm</c> logic: AppCompatFlags registry profile,
/// power-plan enumeration/selection via the registry + powrprof, Ultimate Performance unlock via
/// <c>powercfg.exe</c>, and the <c>control.exe</c> Power Options shortcut.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsPerformanceService : ISystemPerformanceService
{
    // --- AppCompatFlags (Compatibility tab) ---
    const string REG_COMPATIBILITY_PATH = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
    const string REG_VALUE_FULLSCREEN_OPTIMIZATIONS = "DISABLEDXMAXIMIZEDWINDOWEDMODE";
    const string REG_VALUE_RUN_AS_ADMIN = "RUNASADMIN";
    const string REG_VALUE_HDPI_AWARE = "HIGHDPIAWARE";
    const string REG_VALUE_COMPATIBILITY_WITH = "WINXPSP2";

    // --- Power schemes ---
    const string REG_POWER_SCHEMES_PATH = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";

    const string NAME_ULTIMATE_PERFORMANCE = "Ultimate Performance";
    const string GUID_ULTIMATE_PERFORMANCE = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    const string GUID_ULTIMATE_PERFORMANCE_2 = "46742d2f-42d6-41df-a6ed-b49d182ee15d";

    const string NAME_MAX_PERFORMANCE = "High Performance";
    const string GUID_MAX_PERFORMANCE = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    const string NAME_BALANCED = "Balanced";
    const string GUID_BALANCED = "381b4222-f694-41f0-9685-ff5bb260df2e";

    const string NAME_POWER_SAVER = "Power Saver";
    const string GUID_POWER_SAVER = "a1841308-3541-4fab-bc81-f71556f20b4a";

    // ===================================================================================
    // Capabilities
    // ===================================================================================

    public PerfCapabilities GetCapabilities()
    {
        var plans = new List<string>();
        bool ultimateUnlocked = false;

        // Probe the registry for each known power scheme GUID, exactly like the legacy form did.
        if (PowerSchemeExists(GUID_ULTIMATE_PERFORMANCE_2) || PowerSchemeExists(GUID_ULTIMATE_PERFORMANCE))
        {
            plans.Add(NAME_ULTIMATE_PERFORMANCE);
            ultimateUnlocked = true;
        }
        if (PowerSchemeExists(GUID_MAX_PERFORMANCE))
            plans.Add(NAME_MAX_PERFORMANCE);
        if (PowerSchemeExists(GUID_BALANCED))
            plans.Add(NAME_BALANCED);
        if (PowerSchemeExists(GUID_POWER_SAVER))
            plans.Add(NAME_POWER_SAVER);

        return new PerfCapabilities
        {
            SupportsTimerResolution = true,
            SupportsPowerPlans = true,
            SupportsCompatibilityFlags = true,
            AvailablePowerPlans = plans,
            // The unlock button is meaningful only when Ultimate Performance is NOT already present.
            CanUnlockUltimatePerformance = !ultimateUnlocked
        };
    }

    static bool PowerSchemeExists(string guid)
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(REG_POWER_SCHEMES_PATH + "\\" + guid, false);
        return key != null;
    }

    // ===================================================================================
    // Timer resolution
    // ===================================================================================

    public TimerResolutionInfo? GetTimerResolution()
    {
        TimerCaps caps = QueryTimerResolution();
        // Legacy display: min = PeriodMax/10000, max = PeriodMin/10000, current = PeriodCurrent/10000.
        // NtQueryTimerResolution returns values in 100ns units; the *longest* period (PeriodMin in
        // the NT API) is the coarsest resolution, the *shortest* (PeriodMax) is the finest. The
        // legacy UI labelled "minimum" with PeriodMax (finest, smallest ms) and "maximum" with
        // PeriodMin (coarsest, largest ms); we preserve that exactly.
        return new TimerResolutionInfo
        {
            MinimumMs = caps.PeriodMax / 10000.0,
            MaximumMs = caps.PeriodMin / 10000.0,
            CurrentMs = caps.PeriodCurrent / 10000.0
        };
    }

    public void SetTimerResolution(double milliseconds)
    {
        // Legacy "Set" button used a fixed 0.5 ms => (uint)(0.5 * 10000) = 5000 (100ns units).
        uint hundredNs = (uint)(milliseconds * 10000);
        SetTimerResolutionNative(hundredNs, doSet: true);
    }

    public void ResetTimerResolution()
    {
        // Legacy "Default" button: SetTimerResolution(0, false).
        SetTimerResolutionNative(0, doSet: false);
    }

    // ===================================================================================
    // Compatibility profile (AppCompatFlags)
    // ===================================================================================

    public PerfProfile GetCompatibilityProfile(GameInstall game)
    {
        string regValueName = RegistryGameValueName(game);

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(REG_COMPATIBILITY_PATH, false);
        if (key == null)
            return new PerfProfile();

        object? valueObject = key.GetValue(regValueName);
        if (valueObject == null)
            return new PerfProfile();

        string value = valueObject.ToString() ?? string.Empty;

        return new PerfProfile
        {
            FullscreenOptimizationsDisabled = value.Contains(REG_VALUE_FULLSCREEN_OPTIMIZATIONS),
            RunAsAdmin = value.Contains(REG_VALUE_RUN_AS_ADMIN),
            HighDpiAware = value.Contains(REG_VALUE_HDPI_AWARE),
            Win7CompatibilityMode = value.Contains(REG_VALUE_COMPATIBILITY_WITH)
        };
    }

    public void ApplyCompatibilityProfile(GameInstall game, PerfProfile profile)
    {
        string regValueName = RegistryGameValueName(game);

        // The Layers key normally exists; create it if missing so a fresh profile can be written.
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(REG_COMPATIBILITY_PATH, writable: true)
            ?? throw new InvalidOperationException("Could not open the AppCompatFlags\\Layers registry key.");

        // Legacy format: "~ " then a space-separated list of the enabled flags.
        string newValue = "~ ";

        if (profile.FullscreenOptimizationsDisabled)
            newValue += " " + REG_VALUE_FULLSCREEN_OPTIMIZATIONS;
        if (profile.RunAsAdmin)
            newValue += " " + REG_VALUE_RUN_AS_ADMIN;
        if (profile.HighDpiAware)
            newValue += " " + REG_VALUE_HDPI_AWARE;
        if (profile.Win7CompatibilityMode)
            newValue += " " + REG_VALUE_COMPATIBILITY_WITH;

        key.SetValue(regValueName, newValue, RegistryValueKind.String);
    }

    // The legacy key used CurrentDir + "\\" + CurrentGameEXE — i.e. the full executable path.
    static string RegistryGameValueName(GameInstall game) => game.ExecutablePath;

    // ===================================================================================
    // Power plans
    // ===================================================================================

    public string? GetActivePowerPlan()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(REG_POWER_SCHEMES_PATH, false);
        object? valueObject = key?.GetValue("ActivePowerScheme");
        if (valueObject == null)
            return null;

        string value = valueObject.ToString() ?? string.Empty;

        return value switch
        {
            GUID_ULTIMATE_PERFORMANCE or GUID_ULTIMATE_PERFORMANCE_2 => NAME_ULTIMATE_PERFORMANCE,
            GUID_MAX_PERFORMANCE => NAME_MAX_PERFORMANCE,
            GUID_BALANCED => NAME_BALANCED,
            GUID_POWER_SAVER => NAME_POWER_SAVER,
            _ => null
        };
    }

    public void SetPowerPlan(string planName)
    {
        Guid powerPlanGUID;

        switch (planName)
        {
            case NAME_ULTIMATE_PERFORMANCE:
                // Prefer the "_2" GUID when that scheme is the one actually present (mirrors the
                // legacy ultimatePerformanceGUIDIndex == 2 branch).
                powerPlanGUID = PowerSchemeExists(GUID_ULTIMATE_PERFORMANCE_2)
                    ? new Guid(GUID_ULTIMATE_PERFORMANCE_2)
                    : new Guid(GUID_ULTIMATE_PERFORMANCE);
                break;
            case NAME_MAX_PERFORMANCE:
                powerPlanGUID = new Guid(GUID_MAX_PERFORMANCE);
                break;
            case NAME_BALANCED:
                powerPlanGUID = new Guid(GUID_BALANCED);
                break;
            case NAME_POWER_SAVER:
                powerPlanGUID = new Guid(GUID_POWER_SAVER);
                break;
            default:
                // Unknown plan name: do nothing rather than silently switching to Balanced.
                return;
        }

        PowerSetActiveScheme(IntPtr.Zero, ref powerPlanGUID);
    }

    public void UnlockUltimatePerformancePlan()
    {
        // Legacy: powercfg.exe -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61
        Process.Start("powercfg.exe", "-duplicatescheme " + GUID_ULTIMATE_PERFORMANCE);
    }

    public void OpenPowerOptions()
    {
        // Legacy: %SystemRoot%\system32\control.exe /name Microsoft.PowerOptions
        string root = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
        Process.Start(root + "\\system32\\control.exe", "/name Microsoft.PowerOptions");
    }

    // ===================================================================================
    // Native timer-resolution P/Invoke (ported from WinApiCalls)
    // ===================================================================================

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct TimerCaps
    {
        public uint PeriodMin;
        public uint PeriodMax;
        public uint PeriodCurrent;
    }

    [DllImport("ntdll.dll", SetLastError = true)]
    static extern NtStatus NtQueryTimerResolution(out uint MinimumResolution, out uint MaximumResolution, out uint ActualResolution);

    [DllImport("ntdll.dll", SetLastError = true)]
    static extern NtStatus NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);

    [DllImport("powrprof.dll", EntryPoint = "PowerSetActiveScheme")]
    static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid ActivePolicyGuid);

    static TimerCaps QueryTimerResolution()
    {
        TimerCaps caps = new TimerCaps();
        _ = NtQueryTimerResolution(out caps.PeriodMin, out caps.PeriodMax, out caps.PeriodCurrent);
        return caps;
    }

    static ulong SetTimerResolutionNative(uint timerResolutionIn100nsUnits, bool doSet = true)
    {
        uint currentRes = 0;
        _ = NtSetTimerResolution(timerResolutionIn100nsUnits, doSet, ref currentRes);
        return currentRes;
    }
}
