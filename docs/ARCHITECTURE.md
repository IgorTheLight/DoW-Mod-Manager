# DoW Mod Manager — cross-platform edition architecture

This document describes the .NET 10 + Avalonia edition in `DoW Mod Manager.CrossPlatform/`.
The interfaces in `Abstractions/` and the models in `Models/` are the contracts every other
layer codes against; change them deliberately. (The WS0–WS7 labels in code comments name the
work packages the port was built in.)

## Repository layout

The cross-platform edition sits **next to** the WinForms manager and shares nothing with its
build:

```
DoW Mod Manager.sln              WinForms solution (.NET Framework 4.8)
DoW Mod Manager/                 WinForms app: its sources, DXVK/, dgVoodoo2/, FONT/, CAMERA/,
                                 ModList/, LatestStable/ (the cross-platform edition downloads
                                 these folders from GitHub at run time, like the WinForms app)
DowModManager.sln                cross-platform edition + its tests
DoW Mod Manager.CrossPlatform/   cross-platform edition (.NET 10 + Avalonia)
tests/  packaging/  docs/  .github/
```

The WinForms code is the behavioural reference: a feature added there is ported by reading it,
never by compiling it into the cross-platform edition. `git diff <last-ported> HEAD -- "DoW Mod Manager"`
lists what still has to be followed.

Distribution-specific values (version suffix, project URL) live in
`DoW Mod Manager.CrossPlatform/Branding.props`, so a fork shipping its own releases only changes that file.

## Project layout

Single multi-targeted project `DoW Mod Manager.CrossPlatform/DowModManager.csproj`:

- `net10.0` — built everywhere (Linux dev + CI).
- `net10.0-windows` — added automatically on Windows / CI; the **only** TFM that compiles
  `Platform/Windows/**` (P/Invoke, COM, Registry). Guard Windows code with the `WINDOWS`
  compile constant or `[SupportedOSPlatform("windows")]`.

```
DoW Mod Manager.CrossPlatform/
  Abstractions/      IPlatformService + sub-interfaces        (WS0, frozen)
  Models/            GameInstall, LaunchOptions, ModInfo, ...  (WS0, frozen)
  Platform/
    Stubs/           safe no-op impl (default until WS7)
    Windows/         WS1  — compiled only on net10.0-windows
    Linux/           WS2  — Proton/Wine launcher, Steam discovery
  Services/          WS3  — cross-platform domain logic
  ViewModels/        WS4 (base + shell) + WS5 (feature VMs)
  Views/             WS4 (shell/shared) + WS5 (feature windows)
  Styles/ Assets/    WS4 — theme, icons
  Composition/       ServiceConfiguration — the single DI root
  Branding.props     version suffix / project URL (changed only by forks)
```

## Composition root

`Composition/ServiceConfiguration.AddDowModManager()` is the one place everything is wired:

- `AddPlatformServices()` — selects the OS `IPlatformService`. **WS7 seam**: replace the stub
  registration with `AddWindowsPlatform()` / `AddLinuxPlatform()` extension methods that WS1/WS2
  provide. Sub-services (`IGameLauncher`, `IFileSystemService`, …) are re-exported from the
  aggregate with `TryAddSingleton`, so ViewModels can depend on them directly. It must stay
  `TryAdd`: the Linux module registers the sub-services itself and builds `IPlatformService` from
  them, so an unconditional re-export is a resolution cycle (the app froze at start-up;
  `CompositionTests` guards against it).
- `AddCoreServices()` — WS3 registers its parsers/services here.
- `AddViewModels()` — WS4/WS5 register feature ViewModels here.

## Frozen interfaces (`Abstractions/IPlatformService.cs`)

```csharp
IPlatformService {
    IGameLauncher Launcher; IFileSystemService FileSystem;
    ISystemPerformanceService Performance; IGpuInfoService Gpu;
    IGameInstallLocator InstallLocator;
    bool IsWindows;
}

IGameLauncher.LaunchAsync(GameInstall, LaunchOptions, CancellationToken) -> GameLaunchResult
    // implementation owns post-launch tweaks it supports (affinity/fog on Windows)

IFileSystemService { OpenFolder(path); ShowFileProperties(path); CreateShortcut(ShortcutSpec); }

ISystemPerformanceService {
    PerfCapabilities GetCapabilities();
    TimerResolutionInfo? GetTimerResolution(); SetTimerResolution(ms); ResetTimerResolution();
    ApplyCompatibilityProfile(GameInstall, PerfProfile); PerfProfile GetCompatibilityProfile(GameInstall);
    string? GetActivePowerPlan(); SetPowerPlan(name); UnlockUltimatePerformancePlan(); OpenPowerOptions();
}

IGpuInfoService.GetGpusAsync(ct) -> IReadOnlyList<GpuInfo>

IGameInstallLocator {
    DiscoverAsync(ct) -> IReadOnlyList<GameInstall>;   // Steam/Proton scan on Linux
    GameInstall? IdentifyInstall(string directory);    // "manager in game folder" mode
}

```

`IGraphicsModService` (DXVK / dgVoodoo2) used to be a platform service. It is now a
cross-platform domain service in `Services/` (see below), so it left `IPlatformService`.

## Models (`Models/`)

- `GameInstall` — `InstallDir`, `ExecutableName`, `Variant` (Original/WinterAssault/DarkCrusade/
  Soulstorm), `Distribution` (Steam/GoG), `SteamAppId?`, `ProtonPrefix?`, `SteamRoot?`. Helpers:
  `ExecutablePath`, `IsOldGame`, `SupportsNoFog`.
- `LaunchOptions` — mod name + flags (`Dev`, `NoMovies`, `ForceHighPoly`, `NoPrecacheModels`,
  `NoFog`, `CpuAffinityOptimization`, `ExtraArguments`) + `ToArgumentString()` that reproduces the
  legacy `-modname X -dev -nomovies …` command line.
- `GameLaunchResult` — `ProcessId`, `ProcessName`, `Started`, `Detail`.
- `ModInfo` — parsed `.module` (`Name`, `ModuleFilePath`, `ModFolder?`, `Version?`, `Playable`,
  `RequiredMods`). `ModDependencyStatus` for resolution results.
- `PerfCapabilities`, `TimerResolutionInfo`, `PerfProfile`, `GpuInfo`, `ShortcutSpec`.

## MVVM

`ViewModelBase : CommunityToolkit.Mvvm.ComponentModel.ObservableObject`. Use
`[ObservableProperty]` / `[RelayCommand]`. ViewModels take fine-grained interface dependencies
via constructor injection and stay UI-framework-agnostic (no Avalonia types in VMs).

## Build / run

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build  "DoW Mod Manager.CrossPlatform/DowModManager.csproj"
dotnet run    --project "DoW Mod Manager.CrossPlatform/DowModManager.csproj" --framework net10.0
# Windows target, compiled (not run) from Linux:
dotnet build  "DoW Mod Manager.CrossPlatform/DowModManager.csproj" -p:TargetFrameworks=net10.0-windows -p:EnableWindowsTargeting=true
```

## Feature windows (WS5)

Secondary windows are opened through two seams so ViewModels never reference Avalonia windows:

- `ViewModels/IDialogService` — message/confirm boxes, file picker, open URL, and
  `ShowWindow(viewModel)`. Implemented by `Views/DialogService` (maps each ViewModel type to its
  window, keeps one window per type, closes it on `ViewModelBase.CloseRequested` and calls
  `OnClosed()`).
- `ViewModels/IFeatureViewModelFactory` — builds the feature ViewModels with their runtime
  context (selected `GameInstall`, callbacks into the main screen) via `ActivatorUtilities`.

| Window | ViewModel | Domain service |
|---|---|---|
| Mod Merger | `ModMergerViewModel` | `IModMergerService` — edits the `RequiredMod` block, always into a `<Mod>_MERGED.module` copy |
| Mod Downloader | `ModDownloaderViewModel` | `IModlistService` — curated list cached in the user data dir, refreshed from GitHub |
| Settings Manager | `SettingsViewModel` | `IGameSettingsService` — `Local.ini`, `Drivers/spdx9_config.txt`, profiles, `playercfg.lua` |
| System Performance | `SystemPerformanceViewModel` | `ISystemPerformanceService` (Windows only; explanatory text on Linux) |
| Fonts Manager | `FontsManagerViewModel` | `IFontsService` — `*.fnt` of the larger-fonts mod, `defaults.zip` backup |
| About | `AboutViewModel` | — |

The main screen also toggles the "better camera" / "larger fonts" downloads (`IGameExtrasService`)
and the DXVK / dgVoodoo2 Direct3D wrappers (`IGraphicsModService`, same files on every OS; on
Linux the launcher adds `WINEDLLOVERRIDES` so Wine loads them).

## One manager, platform-specific features

The goal is the **same application on Windows and Linux**: one code base, one Avalonia UI, the
same windows, settings file (`DoW Mod Manager.ini`, CRLF) and downloads. Only features that
cannot work on a host differ, and they are greyed out or explained, never hidden in a way that
makes the two builds look like different programs.

Rules:

1. Feature logic lives in `Services/` and is cross-platform by default (file edits, downloads,
   PE patching, `.module` parsing). Prefer this whenever the feature is "put files in the game folder".
2. Only what needs OS APIs goes behind `IPlatformService` (launching, process memory, registry,
   power plans, shortcuts, GPU enumeration). Each OS gets an implementation; unsupported
   operations are explicit no-ops that say why.
3. ViewModels decide what is enabled from `IPlatformService.IsWindows` (or a capability
   property), never with `OperatingSystem.IsXxx()`.

| Feature | Windows | Linux |
|---|---|---|
| Mod list, launch flags, Mod Merger, Mod Downloader, Settings, Fonts, camera | yes | yes |
| Launch | `Process.Start` | Steam `-applaunch` / Proton / Wine (`LinuxLaunchPlanner`) |
| Game discovery | game folder / manual path | Steam libraries + Proton prefixes, manual path |
| LAA (4 GB) patch | yes (32-bit exes) | yes (32-bit exes) |
| DXVK, dgVoodoo2 | yes | yes, plus `WINEDLLOVERRIDES` (Steam route: launch options hint) |
| Run a mod's `Setup.exe` | directly | inside the game's Proton/Wine prefix |
| No-fog memory patch (Soulstorm) | yes | no (process memory inside Wine) |
| CPU affinity/priority, timer resolution, power plans, AppCompat flags | yes | no |
| `.lnk` shortcut / `.desktop` entry | `.lnk` | `.desktop` |

Game paths are resolved through `Services/GamePaths`, which matches each segment
case-insensitively: the legacy code mixed `Data`/`data`, and files referenced by the game
(e.g. `ANSNB___.TTF` in `.fnt` files) only resolve on Linux that way.

`tests/DowModManager.Tests/Ui/WindowSmokeTests` opens every window headlessly (Avalonia.Headless)
on a fake Soulstorm install and fails on binding errors; set `DMM_SCREENSHOTS=<dir>` to save PNGs.
