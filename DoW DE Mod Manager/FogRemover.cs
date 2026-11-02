using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace DoW_DE_Nod_Manager
{
    /// This class was fully rewritten with DeepSeek 4.1 Flash and audited by IgorTheLight
    /// <summary>
    /// Removes the map's distance (ambient) fog from Dawn of War: Definitive Edition.
    ///
    /// The DE engine keeps the environment fog range in the world/render-settings
    /// object as two floats:
    ///     +0xC64 = fog start distance
    ///     +0xC68 = fog end distance
    /// and exposes them through two tiny getters:
    ///     0x116EF50: movss xmm0, dword ptr [rcx+0xC64] ; ret   (fog start)
    ///     0x116EF30: movss xmm0, dword ptr [rcx+0xC68] ; ret   (fog end)
    ///
    /// The engine's own "fog_toggle" developer command switches fog off by saving the
    /// current range and then writing 0 to the fog end field (the fog start is left
    /// untouched). The renderer validates the range with fogEnd >= fogStart; a range
    /// with fogEnd < fogStart is treated as invalid, the fog effect is disabled and
    /// the terrain shader falls back to its no-fog variant.
    ///
    /// This implementation applies the same state permanently by patching the fog end
    /// getter in the running process to always return 0.0f:
    ///     0F 57 C0   xorps xmm0, xmm0
    ///     C3         ret
    ///     90 90 90 90 90   (padding to the original 9-byte function size)
    ///
    /// Every read of the fog end value goes through this getter (the per-frame render
    /// parameter upload, the fog-effect setup and the terrain shader quality flags),
    /// so returning zero disables the distance fog without touching the fog of war.
    /// </summary>
    public static class FogRemover
    {
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_OPERATION = 0x0008;

        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        private const int MaxReadChunk = 1 << 20; // 1 MiB

        // One-time patch guard per running process.
        private static readonly HashSet<int> AlreadyTriedPids = new HashSet<int>();
        private static readonly object SyncRoot = new object();

        // movss xmm0, dword ptr [rcx+0xC68] ; ret
        // (the fog end getter in the 64-bit DE engine)
        private static readonly byte[] FogEndGetterSignature =
        {
            0xF3, 0x0F, 0x10, 0x81, 0x68, 0x0C, 0x00, 0x00, 0xC3
        };

        // xorps xmm0, xmm0 ; ret ; padding
        private static readonly byte[] FogEndGetterPatch =
        {
            0x0F, 0x57, 0xC0, 0xC3, 0x90, 0x90, 0x90, 0x90, 0x90
        };

        // Last error from DisableFog. Useful for the caller / for diagnostics.
        public static string LastError { get; private set; }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr baseAddress,
            byte[] buffer,
            IntPtr size,
            out IntPtr bytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr baseAddress,
            byte[] buffer,
            IntPtr size,
            out IntPtr bytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtectEx(
            IntPtr hProcess,
            IntPtr address,
            IntPtr size,
            uint newProtect,
            out uint oldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(
            IntPtr hProcess,
            IntPtr baseAddress,
            IntPtr size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// Removes the distance fog from a running W40k.exe process.
        /// Safe to call more than once for the same process.
        /// </summary>
        /// <returns>true when the fog getter is patched (or was already patched).</returns>
        public static bool DisableFog(Process process)
        {
            LastError = null;

            try
            {
                if (process == null)
                {
                    LastError = "process is null.";
                    return false;
                }

                // W40k.exe is a 64-bit process and lives above the 4 GB boundary.
                // A 32-bit mod manager cannot read or patch its memory, so make sure
                // the manager itself is running as a 64-bit process.
                if (IntPtr.Size != 8)
                {
                    LastError = "the mod manager is running as a 32-bit process, but W40k.exe is 64-bit. " +
                                "Build/run the 64-bit version of the mod manager.";
                    ThemedMessageBox.Show(
                        "Fog Remover requires the 64-bit build of the mod manager:\n\n" + LastError,
                        "Fog Remover");
                    return false;
                }

                string exeName = process.ProcessName;
                if (!string.Equals(exeName, "W40k", StringComparison.OrdinalIgnoreCase))
                    return false;

                lock (SyncRoot)
                {
                    if (AlreadyTriedPids.Contains(process.Id))
                        return true;
                }

                // The game may still be finishing its startup; a few short retries
                // make the feature reliable on slower machines.
                string error = null;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    if (TryDisableFog(process, out error))
                    {
                        lock (SyncRoot)
                        {
                            AlreadyTriedPids.Add(process.Id);
                        }
                        return true;
                    }

                    if (process.HasExited)
                        break;

                    Thread.Sleep(250);
                }

                LastError = error ?? "unknown error.";
                ThemedMessageBox.Show(
                    "Fog Remover could not patch the game:\n\n" + LastError,
                    "Fog Remover");
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.GetType().FullName + ": " + ex.Message;
            }

            return false;
        }

        private static bool TryDisableFog(Process process, out string error)
        {
            error = null;

            long moduleBase;
            int moduleSize;

            try
            {
                ProcessModule mainModule = process.MainModule;
                moduleBase = mainModule.BaseAddress.ToInt64();
                moduleSize = mainModule.ModuleMemorySize;
            }
            catch (Exception ex)
            {
                error = "cannot read the game's main module: " + ex.Message;
                return false;
            }

            IntPtr hProcess = OpenProcess(
                PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION,
                false,
                process.Id);

            if (hProcess == IntPtr.Zero)
            {
                error = "OpenProcess failed (WinErr=" + Marshal.GetLastWin32Error() +
                        "). Try running the mod manager as administrator.";
                return false;
            }

            try
            {
                long getterAddress;
                if (!FindFogEndGetter(hProcess, moduleBase, moduleSize, out getterAddress, out error))
                    return false;

                return PatchFogEndGetter(hProcess, getterAddress, out error);
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }

        /// <summary>
        /// Locates the fog end getter by scanning the executable sections of the game
        /// module for its signature. This is build independent: it only depends on the
        /// getter's machine code (and therefore on the world object's +0xC68 field).
        /// </summary>
        private static bool FindFogEndGetter(
            IntPtr hProcess,
            long moduleBase,
            int moduleSize,
            out long getterAddress,
            out string error)
        {
            getterAddress = 0;
            error = null;

            // PE headers are small; 0x2000 bytes is enough for the DOS/NT headers and
            // any realistic section table.
            byte[] headers = new byte[0x2000];
            if (!ReadExact(hProcess, moduleBase, headers, 0, headers.Length, out error))
            {
                error = "cannot read the PE headers: " + error;
                return false;
            }

            if (headers[0] != (byte)'M' || headers[1] != (byte)'Z')
            {
                error = "the game module does not start with an MZ header.";
                return false;
            }

            int peOffset = BitConverter.ToInt32(headers, 0x3C);
            if (peOffset < 0 || peOffset + 24 > headers.Length)
            {
                error = "invalid PE header offset.";
                return false;
            }

            if (headers[peOffset] != (byte)'P' ||
                headers[peOffset + 1] != (byte)'E' ||
                headers[peOffset + 2] != 0 ||
                headers[peOffset + 3] != 0)
            {
                error = "invalid PE signature.";
                return false;
            }

            ushort machine = BitConverter.ToUInt16(headers, peOffset + 4);
            if (machine != 0x8664)
            {
                error = "the game is not a 64-bit executable (Machine=0x" + machine.ToString("X4") + ").";
                return false;
            }

            ushort sectionCount = BitConverter.ToUInt16(headers, peOffset + 6);
            ushort optionalHeaderSize = BitConverter.ToUInt16(headers, peOffset + 20);
            int optionalOffset = peOffset + 24;

            if (optionalHeaderSize < 0x70 || optionalOffset + optionalHeaderSize > headers.Length)
            {
                error = "invalid PE optional header.";
                return false;
            }

            ushort magic = BitConverter.ToUInt16(headers, optionalOffset);
            if (magic != 0x20B) // PE32+
            {
                error = "the game is not a PE32+ (64-bit) image.";
                return false;
            }

            int sectionOffset = optionalOffset + optionalHeaderSize;
            if (sectionCount == 0 ||
                sectionOffset + sectionCount * 40 > headers.Length)
            {
                error = "invalid PE section table.";
                return false;
            }

            for (int i = 0; i < sectionCount; i++)
            {
                int section = sectionOffset + i * 40;

                uint virtualSize = BitConverter.ToUInt32(headers, section + 8);
                uint virtualAddress = BitConverter.ToUInt32(headers, section + 12);
                uint characteristics = BitConverter.ToUInt32(headers, section + 36);

                bool executable = (characteristics & 0x20000000) != 0;
                if (!executable || virtualSize == 0)
                    continue;

                // Sanity check: the section must be inside the mapped image.
                if (virtualAddress + (long)virtualSize > moduleSize)
                    continue;

                int sectionSize = (int)Math.Min((long)virtualSize, 64L * 1024 * 1024);
                byte[] sectionBytes = new byte[sectionSize];

                if (!ReadExact(hProcess, moduleBase + virtualAddress, sectionBytes, 0, sectionSize, out error))
                    continue;

                int index = IndexOf(sectionBytes, FogEndGetterSignature);
                if (index >= 0)
                {
                    getterAddress = moduleBase + virtualAddress + index;
                    return true;
                }
            }

            error = "could not find the fog-end getter signature. " +
                    "The installed W40k.exe build is probably not supported yet.";
            return false;
        }

        private static bool PatchFogEndGetter(IntPtr hProcess, long getterAddress, out string error)
        {
            error = null;

            byte[] original = new byte[FogEndGetterSignature.Length];
            if (!ReadExact(hProcess, getterAddress, original, 0, original.Length, out error))
            {
                error = "cannot read the fog-end getter: " + error;
                return false;
            }

            if (!BytesEqual(original, FogEndGetterSignature))
            {
                error = "unexpected bytes at the fog-end getter address 0x" +
                        getterAddress.ToString("X") + ": " + BytesToHex(original);
                return false;
            }

            uint oldProtect;
            if (!VirtualProtectEx(
                    hProcess,
                    new IntPtr(getterAddress),
                    new IntPtr(FogEndGetterPatch.Length),
                    PAGE_EXECUTE_READWRITE,
                    out oldProtect))
            {
                error = "VirtualProtectEx failed (WinErr=" + Marshal.GetLastWin32Error() + ").";
                return false;
            }

            bool written;
            try
            {
                written = WriteExact(hProcess, getterAddress, FogEndGetterPatch, out error);
            }
            finally
            {
                uint ignored;
                VirtualProtectEx(
                    hProcess,
                    new IntPtr(getterAddress),
                    new IntPtr(FogEndGetterPatch.Length),
                    oldProtect,
                    out ignored);
            }

            if (!written)
                return false;

            FlushInstructionCache(hProcess, new IntPtr(getterAddress), new IntPtr(FogEndGetterPatch.Length));

            byte[] verify = new byte[FogEndGetterPatch.Length];
            if (!ReadExact(hProcess, getterAddress, verify, 0, verify.Length, out error))
            {
                error = "cannot verify the fog-end patch: " + error;
                return false;
            }

            if (!BytesEqual(verify, FogEndGetterPatch))
            {
                error = "fog-end patch verification failed. Wanted " + BytesToHex(FogEndGetterPatch) +
                        ", got " + BytesToHex(verify) + ".";
                return false;
            }

            return true;
        }

        private static bool ReadExact(
            IntPtr hProcess,
            long address,
            byte[] buffer,
            int offset,
            int count,
            out string error)
        {
            error = null;

            if (count == 0)
                return true;

            int done = 0;
            while (done < count)
            {
                int chunk = Math.Min(MaxReadChunk, count - done);
                byte[] temp = new byte[chunk];

                if (!ReadProcessMemory(
                        hProcess,
                        new IntPtr(address + done),
                        temp,
                        new IntPtr(chunk),
                        out IntPtr bytesRead))
                {
                    error = "ReadProcessMemory failed at 0x" + (address + done).ToString("X") +
                            " (WinErr=" + Marshal.GetLastWin32Error() + ").";
                    return false;
                }

                if (bytesRead.ToInt64() != chunk)
                {
                    error = "short read at 0x" + (address + done).ToString("X") +
                            " (" + bytesRead.ToInt64() + "/" + chunk + ").";
                    return false;
                }

                Buffer.BlockCopy(temp, 0, buffer, offset + done, chunk);
                done += chunk;
            }

            return true;
        }

        private static bool WriteExact(
            IntPtr hProcess,
            long address,
            byte[] data,
            out string error)
        {
            error = null;

            if (!WriteProcessMemory(
                    hProcess,
                    new IntPtr(address),
                    data,
                    new IntPtr(data.Length),
                    out IntPtr written))
            {
                error = "WriteProcessMemory failed at 0x" + address.ToString("X") +
                        " (WinErr=" + Marshal.GetLastWin32Error() + ").";
                return false;
            }

            if (written.ToInt64() != data.Length)
            {
                error = "short write at 0x" + address.ToString("X") +
                        " (" + written.ToInt64() + "/" + data.Length + ").";
                return false;
            }

            return true;
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            int limit = haystack.Length - needle.Length;
            for (int i = 0; i <= limit; i++)
            {
                if (haystack[i] != needle[0])
                    continue;

                int j = 1;
                while (j < needle.Length && haystack[i + j] == needle[j])
                    j++;

                if (j == needle.Length)
                    return i;
            }

            return -1;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private static string BytesToHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0)
                    builder.Append(' ');
                builder.Append(bytes[i].ToString("X2"));
            }
            return builder.ToString();
        }

        public static void ResetAllAttempts()
        {
            lock (SyncRoot)
            {
                AlreadyTriedPids.Clear();
            }
        }

        public static void ResetAttempt(Process process)
        {
            if (process == null)
                return;

            lock (SyncRoot)
            {
                AlreadyTriedPids.Remove(process.Id);
            }
        }
    }
}
