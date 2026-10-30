using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Platform.Windows;

/// <summary>
/// Enumerates installed GPUs via user32 <c>EnumDisplayDevices</c>. Ported from the legacy
/// <c>SettingsManagerForm.GetAllVideocards()</c>, which queried WMI <c>Win32_VideoController</c>
/// and kept only adapters with a non-null <c>CurrentBitsPerPixel</c> (i.e. currently driving a
/// display). Here that is adapters attached to the desktop, mirror drivers excluded. WMI is not
/// used because System.Management needs built-in COM interop, which trimming removes.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsGpuInfoService : IGpuInfoService
{
    public Task<IReadOnlyList<GpuInfo>> GetGpusAsync(CancellationToken ct = default)
    {
        return Task.Run<IReadOnlyList<GpuInfo>>(() =>
        {
            var gpus = new List<GpuInfo>();
            var seenAdapters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var device = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            for (uint i = 0; EnumDisplayDevices(null, i, ref device, 0); i++)
            {
                ct.ThrowIfCancellationRequested();

                bool attached = (device.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0;
                bool mirror = (device.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER) != 0;
                // One entry per display output; outputs of the same adapter share the DeviceKey
                // up to its last segment (...\Video\{adapter GUID}\0000, \0001, ...).
                if (attached && !mirror && !string.IsNullOrEmpty(device.DeviceString)
                    && seenAdapters.Add(AdapterKey(device)))
                {
                    gpus.Add(new GpuInfo { Description = device.DeviceString });
                }

                device = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            }

            return gpus;
        }, ct);
    }

    static string AdapterKey(DISPLAY_DEVICE device)
    {
        string key = device.DeviceKey ?? "";
        int slash = key.LastIndexOf('\\');
        return slash > 0 ? key[..slash] : device.DeviceString;
    }

    const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1;
    const int DISPLAY_DEVICE_MIRRORING_DRIVER = 0x8;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplayDevicesW")]
    static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);
}
