using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Platform.Linux;

/// <summary>
/// Enumerates installed GPUs on Linux. Prefers <c>lspci</c> for human-readable descriptions and
/// falls back to PCI vendor/device IDs read from <c>/sys/class/drm</c>. Always defensive: any
/// failure yields an empty list rather than throwing.
/// </summary>
public sealed class LinuxGpuInfoService : IGpuInfoService
{
    public async Task<IReadOnlyList<GpuInfo>> GetGpusAsync(CancellationToken ct = default)
    {
        var fromLspci = await TryLspciAsync(ct).ConfigureAwait(false);
        if (fromLspci.Count > 0)
            return fromLspci;

        return ReadFromSysfs();
    }

    /// <summary>Runs <c>lspci</c> and keeps lines describing VGA / 3D / Display controllers.</summary>
    private static async Task<IReadOnlyList<GpuInfo>> TryLspciAsync(CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "lspci",
                Arguments = "-mm",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(psi);
            if (process is null)
                return [];

            var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            var gpus = new List<GpuInfo>();
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                // -mm output: <slot> "Class" "Vendor" "Device" ...
                if (!ContainsGpuClass(line))
                    continue;

                var description = DescribeLspciLine(line);
                if (!string.IsNullOrWhiteSpace(description))
                    gpus.Add(new GpuInfo { Description = description, Driver = "lspci" });
            }

            return gpus;
        }
        catch
        {
            return [];
        }
    }

    private static bool ContainsGpuClass(string line)
        => line.Contains("VGA compatible controller", StringComparison.OrdinalIgnoreCase)
        || line.Contains("3D controller", StringComparison.OrdinalIgnoreCase)
        || line.Contains("Display controller", StringComparison.OrdinalIgnoreCase);

    /// <summary>Extracts vendor + device from an <c>lspci -mm</c> line into a readable string.</summary>
    private static string DescribeLspciLine(string line)
    {
        // Quoted fields: slot "class" "vendor" "device" ["svendor" "sdevice"] -rev ...
        var fields = new List<string>();
        var inQuotes = false;
        var current = new System.Text.StringBuilder();
        foreach (var c in line)
        {
            if (c == '"')
            {
                if (inQuotes) { fields.Add(current.ToString()); current.Clear(); }
                inQuotes = !inQuotes;
            }
            else if (inQuotes)
            {
                current.Append(c);
            }
        }

        // fields[0]=class, [1]=vendor, [2]=device (subsystem fields may follow).
        var vendor = fields.Count > 1 ? fields[1] : "";
        var device = fields.Count > 2 ? fields[2] : "";
        var combined = string.Join(" ", new[] { vendor, device }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.IsNullOrWhiteSpace(combined) ? line.Trim() : combined;
    }

    /// <summary>Fallback: read PCI vendor/device IDs from each DRM card under /sys/class/drm.</summary>
    private static IReadOnlyList<GpuInfo> ReadFromSysfs()
    {
        var gpus = new List<GpuInfo>();
        const string drmPath = "/sys/class/drm";

        try
        {
            if (!Directory.Exists(drmPath))
                return gpus;

            foreach (var card in Directory.EnumerateDirectories(drmPath, "card*"))
            {
                // Skip connector entries like card0-DP-1; only the bare cardN have a device dir.
                var name = Path.GetFileName(card);
                if (name.Contains('-'))
                    continue;

                var deviceDir = Path.Combine(card, "device");
                var vendor = ReadIdFile(Path.Combine(deviceDir, "vendor"));
                var device = ReadIdFile(Path.Combine(deviceDir, "device"));

                if (vendor is null && device is null)
                    continue;

                var vendorName = DescribeVendor(vendor);
                var description = $"{vendorName} GPU ({name}, vendor={vendor ?? "?"}, device={device ?? "?"})";
                gpus.Add(new GpuInfo { Description = description, Driver = ReadDriver(deviceDir) });
            }
        }
        catch
        {
            // Defensive: return whatever we managed to collect.
        }

        return gpus;
    }

    private static string? ReadIdFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            return File.ReadAllText(path).Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadDriver(string deviceDir)
    {
        try
        {
            var driverLink = Path.Combine(deviceDir, "driver");
            if (!Directory.Exists(driverLink))
                return null;
            // The driver symlink target's name is the module (e.g. amdgpu, i915, nvidia).
            var target = new DirectoryInfo(driverLink).ResolveLinkTarget(true)?.Name
                         ?? new DirectoryInfo(driverLink).Name;
            return target;
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeVendor(string? vendorId) => vendorId?.ToLowerInvariant() switch
    {
        "0x1002" => "AMD/ATI",
        "0x10de" => "NVIDIA",
        "0x8086" => "Intel",
        _ => "Unknown vendor"
    };
}
