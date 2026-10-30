# Linux port — Windows-bound code audit

Baseline: upstream **IgorTheLight/DoW-Mod-Manager `master` @ `a5a9b2f` (v2.7.0.1)**, whose
WinForms sources live unchanged in `DoW Mod Manager/` (reference only, never compiled into the
port). Line numbers refer to those files. The "Port" column says where the .NET 10 + Avalonia port
(`DoW Mod Manager.CrossPlatform/DowModManager.csproj`) stands today.

## 1. Project, framework, UI

| | Upstream 2.7.0.1 | Port |
|---|---|---|
| Project | `DoW Mod Manager/DoW Mod Manager.csproj` (old-style) | `DoW Mod Manager.CrossPlatform/DowModManager.csproj` (SDK-style) |
| Target framework | **.NET Framework 4.8** (`TargetFrameworkVersion v4.8`), Windows-only | `net10.0` everywhere, plus `net10.0-windows` when built on Windows |
| UI | **WinForms** (`System.Windows.Forms`, `*.Designer.cs` + `*.resx`) | **Avalonia 11.2** + CommunityToolkit.Mvvm |
| Assembly name | `DoW Mod Manager v2.7.0.1` (version is in the .exe name) | `DowModManager` |
| COM | `IWshRuntimeLibrary` (Windows Script Host, `.lnk` shortcuts) | none; Windows shortcut in `Platform/Windows/WindowsFileSystemService.cs`, `.desktop` file on Linux |
| Other Windows refs | `System.Management` (WMI), `System.Deployment` | none: the Registry API is built into `net10.0-windows`; WMI is replaced (see below) |

## 2. P/Invoke (`DllImport`)

| Upstream location | DLL / functions | Purpose | Port |
|---|---|---|---|
| `FogRemover.cs:43-56` | kernel32 `OpenProcess`, `ReadProcessMemory`, `WriteProcessMemory`, `VirtualProtectEx`, `CloseHandle` | Soulstorm "no fog": patches the running game's memory | `Platform/Windows/WindowsFogRemover.cs` only. **Linux: not available**. The game runs inside Wine, so this would need a helper `.exe` run in the same prefix (see the plan). |
| `WinApiCalls.cs:41-45` | ntdll `NtQueryTimerResolution`, `NtSetTimerResolution` | 0.5 ms system timer resolution | `Platform/Windows/WindowsPerformanceService.cs:247-250`; Linux no-op |
| `WinApiCalls.cs:61-65` | winmm `timeBeginPeriod`/`timeEndPeriod` | timer resolution | dropped (ntdll path is enough) |
| `WinApiCalls.cs:67-77` | powrprof `PowerGetActiveScheme`, `PowerSetActiveScheme`, `PowerReadACValue`, `PowerWriteACValueIndex` | power plan switching | `WindowsPerformanceService.cs:253`; Linux no-op (`LinuxPerformanceService`) |
| `WinApiCalls.cs:79-92` | shell32 `ShellExecuteEx` | "Properties" dialog for the exe | `WindowsFileSystemService.cs:138`; Linux opens the folder |

## 3. Registry

All in `SystemPerformanceManagerForm.cs` (lines 11-19 constants, 66-183 use):

- `HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers`, value name
  `<install dir>\<game exe>`: compatibility flags `DISABLEDXMAXIMIZEDWINDOWEDMODE`, `RUNASADMIN`,
  `HIGHDPIAWARE`, `WINXPSP2`. Read and written.
- `HKLM\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\<GUID>`: detects the Ultimate,
  Max, Balanced and Power-saver plans. Read only.

Upstream does **not** use the registry to find the game. It expects to sit in the game folder
(`Directory.GetCurrentDirectory()`), so nothing equivalent to a Steam/Lutris lookup has to be
ported from it.

Port: `Platform/Windows/WindowsPerformanceService.cs` only. On Linux, AppCompat flags have no
meaning (Proton/Wine ignores the Windows host's HKCU). They could be written into the *prefix*
registry (`user.reg`), but this is not planned.

## 4. Hard-coded Windows paths and assumptions

| Upstream location | What | Port |
|---|---|---|
| everywhere (`File.Exists("dxvk.conf")`, `"d3d9.dll"`, …) | Relative paths: the cwd **must** be the game folder | absolute paths from `GameInstall.InstallDir` |
| `ModManagerForm.cs:413-427` | `%WINDIR%\Microsoft.NET\Framework\v4.0.30319\ngen.exe` (AOT/NGEN toggle) | dropped (no NGEN on .NET 10) |
| `ModManagerForm.cs:1459,1467` | `explorer.exe <dir>` | `IFileSystemService.OpenFolder` → `xdg-open` on Linux |
| `DownloadHelper.cs:226` | `cmd.exe /C choice … & Del "<old exe>"` (self-delete after update) | dropped with the updater |
| `DownloadHelper.cs:236-237` | `.lnk` shortcut via WScript | ShellLink COM object (`IShellLinkW`) on Windows, `.desktop` entry on Linux |
| `SettingsManagerForm.cs:700` | WMI `SELECT * FROM Win32_VideoController` (VRAM/GPU) | `WindowsGpuInfoService` (user32 `EnumDisplayDevices`) / `LinuxGpuInfoService` (`/sys/class/drm`, `lspci`) |
| `ModManagerForm.cs:1036-1037` | `PriorityClass = High`, `ProcessorAffinity = 0x6` on the game process | Windows only. On Linux the game is a Wine process, found by `GameProcessFinder`, and affinity is not applied. |
| `*.module`, `Local.ini`, `warnings.log` | `\` separators, case-insensitive FS, CRLF | `GamePaths` resolves case-insensitively; INI written with CRLF |
| DE user data | `%APPDATA%\Relic Entertainment\Dawn of War` | `GameVariantDetector.ProtonAppDataDir` (`compatdata/<appid>/pfx/drive_c/users/steamuser/AppData/Roaming`) |

## 5. LAA ("4 GB patch")

- `LAATweaks.cs` sets/clears `IMAGE_FILE_LARGE_ADDRESS_AWARE` (`0x20`) in the PE `Characteristics`
  field. `LAATweaksOld.cs` is the older variant that `ModManagerForm.cs:1329` uses for **Steam**,
  while `LAATweaks` is used for **GOG** (setting `IsGOGVersion`).
- It is pure byte I/O and works on any OS. The port has it in `Services/LaaService` (`ILaaService`)
  and refuses 64-bit images (`LaaApplicability.NotNeeded64Bit`). The Definitive Edition's
  `W40k.exe` is 64-bit, so LAA never applies to DE.
- Caveat under Proton: Steam's file verification can restore the original exe. This is the same as on Windows.

## 6. DXVK and dgVoodoo2 installers

- **DXVK** (`ModManagerForm.cs:286-301`, `1495-1513`): downloads `d3d9.dll`, `d3d8.dll` (was
  `dxgi.dll` before 2.7.0.0), `dxvk.conf` and `dxvk.version` from `raw.githubusercontent.com/IgorTheLight/…/DXVK/`
  into the game folder.
- **dgVoodoo2** (new in upstream 2.7.0.0, `ModManagerForm.cs:50,328-343,1539-1556`): downloads
  `D3D9.dll`, `dgVoodoo.conf`, `dgVoodooCpl.exe` and `dgVoodoo.version`. Upstream does not stop
  both being installed, although both provide `d3d9.dll`.
- **Port (both OSes)**: `Services/GraphicsModService.cs` downloads the same files from the same
  upstream folders, with the `DXVKUpdateCheck` / `dgVoodoo2UpdateCheck` version checks. The
  wrappers are mutually exclusive, downloads are staged so a failure keeps the current install,
  and other casings of a file are removed (`D3D9.dll` vs `d3d9.dll` are two files on Linux).
  On Linux, `LinuxGameLauncher` passes `GraphicsModService.WineDllOverrides` (e.g. `d3d9,d3d8=n,b`)
  as `WINEDLLOVERRIDES` on the Proton and Wine routes. Steam `-applaunch` cannot receive
  environment variables; Proton's DXVK setup loads `d3d9`/`d3d8` as native, which should pick the
  game-folder DLLs, and the launch detail tells the user which Steam launch option to add if not.
  **Not verified on a real Proton install yet.** The dgVoodoo2 binaries are freeware, not MIT.

## 7. Updater

- Upstream `DownloadHelper.CheckForExeUpdate` (`DownloadHelper.cs:52,92`) compares against
  `…/IgorTheLight/DoW-Mod-Manager/master/DoW%20Mod%20Manager/LatestStable/version`, downloads
  `LatestStable/DoW Mod Manager.exe`, starts it and self-deletes through `cmd.exe`. It runs at
  start-up when `Autoupdate=1` (`ModManagerForm.cs:271-278`).
- **Port: no self-updater.** No code reads `LatestStable/version` or downloads the `.exe`. The
  `Autoupdate` INI key is only kept so existing INI files round-trip (comment added in
  `Services/SettingsKeys.cs`). About → "Check for updates" opens
  the project's Releases page (`DmmHomePageUrl` in `Branding.props`). The cross-platform edition
  therefore never replaces itself with the WinForms `.exe`.
- Still fetched from upstream (data, not executables): mod list (`Services/ModlistService.cs:20`,
  `ModList/version`) and camera/fonts/DXVK payloads (`Services/GameExtrasService.cs:7`). These
  are game data and are safe to share with upstream.

## 8. Launching the game `.exe`

- Upstream (`ModManagerForm.cs:39-42, 1013-1016`): `Process.Start(CurrentGameEXE, "-modname X [-dev] [-nomovies] [-forcehighpoly] [-noprecachemodels]")`
  with the cwd being the game folder. Exe is `Soulstorm.exe` > `DarkCrusade.exe` > `W40kWA.exe`
  > `W40k.exe`. Afterwards it polls `Process.GetProcessesByName` for affinity/priority (lines 770, 1035, 1063)
  and the fog patch.
- Port, Windows: `WindowsGameLauncher` does the same.
- Port, Linux: `LinuxLaunchPlanner` / `LinuxGameLauncher` try routes in this order: `steam -applaunch <appid> <args>`
  (native or snap), then `flatpak run com.valvesoftware.Steam -applaunch`, then the `steam://` URL. If no
  Steam is available, Proton is run directly with `STEAM_COMPAT_DATA_PATH` / `STEAM_COMPAT_CLIENT_INSTALL_PATH`
  (`ProtonToolResolver` picks the Proton build Steam assigned to the app; `DOWMM_PROTON` forces this
  route first). The last resort is plain
  `wine` with an inherited `WINEPREFIX` (Lutris/Heroic/Bottles). The game pid is found afterwards by
  `GameProcessFinder` (`/proc` scan for the Wine process).

## 9. Remaining Windows-only gaps on Linux

1. No-fog memory patch (Soulstorm): needs an in-prefix helper.
2. AppCompat registry flags: no Linux equivalent, hidden.
3. CPU affinity/priority of the game: could use `taskset`/`renice` on the Wine pid. Not done.
4. DXVK/dgVoodoo2 under Steam-launched Proton: relies on Proton's native override (to verify).
5. Lutris/Heroic **discovery**: only *launching* honours `WINEPREFIX`. Discovering games from
   those launchers' configs is not implemented (see the plan).
