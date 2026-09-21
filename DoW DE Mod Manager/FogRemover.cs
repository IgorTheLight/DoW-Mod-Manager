using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace DoW_DE_Nod_Manager
{
    public static class FogRemover
    {
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_OPERATION = 0x0008;

        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;

        // One-time patch guard per running process.
        private static readonly HashSet<int> AlreadyTriedPids = new HashSet<int>();

        // Exact RVAs in the user's W40k.exe build.
        // These are CALL rel32 instructions inside the fog-sync routine.
        private const long RVA_CALL_SETC44 = 0x118DC8A;
        private const long RVA_CALL_SETC48 = 0x118DC9A;

        // Original bytes at those callsites in the uploaded EXE.
        // call 0x14116B440
        private static readonly byte[] OrigCallSetC44 = { 0xE8, 0xB1, 0xD7, 0xFD, 0xFF };

        // call 0x14116B410
        private static readonly byte[] OrigCallSetC48 = { 0xE8, 0x71, 0xD7, 0xFD, 0xFF };

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr address,
            byte[] buffer,
            IntPtr size,
            out IntPtr read);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr address,
            byte[] buffer,
            IntPtr size,
            out IntPtr written);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtectEx(
            IntPtr hProcess,
            IntPtr address,
            IntPtr size,
            uint newProtect,
            out uint oldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            IntPtr dwSize,
            uint flAllocationType,
            uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        public static void DisableFog(Process process)
        {
            try
            {
                if (process == null)
                {
                    ThemedMessageBox.Show("Fog Remover: process is null.", "Fog Remover");
                    return;
                }

                string exeName = process.ProcessName?.ToLowerInvariant() ?? "";
                if (exeName != "w40k")
                    return;

                lock (AlreadyTriedPids)
                {
                    if (AlreadyTriedPids.Contains(process.Id))
                        return;

                    AlreadyTriedPids.Add(process.Id);
                }

                IntPtr hProc = OpenProcess(
                    PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION,
                    false,
                    process.Id);

                if (hProc == IntPtr.Zero)
                {
                    ThemedMessageBox.Show(
                        $"Fog Remover: OpenProcess failed. WinErr={Marshal.GetLastWin32Error()}",
                        "Fog Remover");
                    return;
                }

                try
                {
                    long baseAddr = process.MainModule.BaseAddress.ToInt64();

                    StringBuilder report = new StringBuilder();
                    report.AppendLine("Fog Remover (callsite patch)");
                    report.AppendLine($"PID: {process.Id}");
                    report.AppendLine($"Base: 0x{baseAddr:X}");
                    report.AppendLine();

                    int ok = 0;
                    int fail = 0;

                    // Hard fog: 512.0f
                    PatchFogCallsite(
                        hProc,
                        baseAddr,
                        RVA_CALL_SETC44,
                        OrigCallSetC44,
                        fieldOffset: 0x0C44,
                        forcedFloat: 512.0f,
                        name: "C44 hard fog",
                        report: report,
                        ok: ref ok,
                        fail: ref fail);

                    // Weak fog: 1024.0f
                    PatchFogCallsite(
                        hProc,
                        baseAddr,
                        RVA_CALL_SETC48,
                        OrigCallSetC48,
                        fieldOffset: 0x0C48,
                        forcedFloat: 1024.0f,
                        name: "C48 weak fog",
                        report: report,
                        ok: ref ok,
                        fail: ref fail);

                    report.AppendLine();
                    report.AppendLine($"Patched: {ok}");
                    report.AppendLine($"Failed: {fail}");

                    if (fail > 0)
                    {
                        ThemedMessageBox.Show(report.ToString(), "Fog Remover");
                    }
                }
                finally
                {
                    CloseHandle(hProc);
                }
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(
                    "Fog Remover exception:\n\n" +
                    ex.GetType().FullName + "\n" +
                    ex.Message + "\n\n" +
                    ex.StackTrace,
                    "Fog Remover");
            }
        }

        private static void PatchFogCallsite(
            IntPtr hProc,
            long baseAddr,
            long callsiteRva,
            byte[] expectedCallBytes,
            int fieldOffset,
            float forcedFloat,
            string name,
            StringBuilder report,
            ref int ok,
            ref int fail)
        {
            long callsiteAddr = checked(baseAddr + callsiteRva);

            byte[] current = new byte[5];
            if (!ReadExact(hProc, callsiteAddr, current, out string readErr))
            {
                report.AppendLine($"{name}: READ FAILED ({readErr})");
                fail++;
                return;
            }

            // Already patched with call rel32 to our stub? We cannot perfectly verify target here,
            // but a changed call is enough to avoid repatching repeatedly.
            if (!current.SequenceEqual(expectedCallBytes))
            {
                report.AppendLine($"{name}: already changed or unexpected bytes");
                report.AppendLine($"  Expected: {Bytes(expectedCallBytes)}");
                report.AppendLine($"  Actual:   {Bytes(current)}");
                fail++;
                return;
            }

            byte[] stub = BuildForceFloatStub(fieldOffset, forcedFloat);

            IntPtr cave = AllocateNear(hProc, callsiteAddr, stub.Length);
            if (cave == IntPtr.Zero)
            {
                report.AppendLine($"{name}: failed to allocate near code cave. WinErr={Marshal.GetLastWin32Error()}");
                fail++;
                return;
            }

            long caveAddr = cave.ToInt64();

            if (!WriteExact(hProc, caveAddr, stub, out string caveErr))
            {
                report.AppendLine($"{name}: failed to write stub ({caveErr})");
                fail++;
                return;
            }

            long rel = caveAddr - (callsiteAddr + 5);
            if (rel < int.MinValue || rel > int.MaxValue)
            {
                report.AppendLine($"{name}: stub too far for rel32 call");
                fail++;
                return;
            }

            byte[] patchedCall = new byte[5];
            patchedCall[0] = 0xE8; // CALL rel32
            Array.Copy(BitConverter.GetBytes((int)rel), 0, patchedCall, 1, 4);

            if (!WriteWithProtect(hProc, callsiteAddr, patchedCall, out string patchErr))
            {
                report.AppendLine($"{name}: call patch failed ({patchErr})");
                fail++;
                return;
            }

            byte[] verify = new byte[5];
            if (!ReadExact(hProc, callsiteAddr, verify, out string verifyErr))
            {
                report.AppendLine($"{name}: VERIFY FAILED ({verifyErr})");
                fail++;
                return;
            }

            if (!verify.SequenceEqual(patchedCall))
            {
                report.AppendLine($"{name}: VERIFY MISMATCH");
                report.AppendLine($"  Wanted: {Bytes(patchedCall)}");
                report.AppendLine($"  Actual: {Bytes(verify)}");
                fail++;
                return;
            }

            report.AppendLine($"{name}: OK");
            report.AppendLine($"  Callsite @ 0x{callsiteAddr:X}");
            report.AppendLine($"  Stub     @ 0x{caveAddr:X}");
            report.AppendLine($"  Forced   = {forcedFloat:0.###}");
            ok++;
        }

        private static byte[] BuildForceFloatStub(int fieldOffset, float value)
        {
            // mov dword ptr [rcx+disp32], imm32
            // ret
            byte[] stub = new byte[11];
            stub[0] = 0xC7;
            stub[1] = 0x81;
            Array.Copy(BitConverter.GetBytes(fieldOffset), 0, stub, 2, 4);
            Array.Copy(BitConverter.GetBytes(value), 0, stub, 6, 4);
            stub[10] = 0xC3;
            return stub;
        }

        private static IntPtr AllocateNear(IntPtr hProc, long targetAddr, int size)
        {
            const long MaxRel32Distance = 0x7FFF0000;
            const long Step = 0x10000;

            for (long delta = 0; delta <= MaxRel32Distance; delta += Step)
            {
                long up = Align64K(targetAddr + delta);
                IntPtr pUp = VirtualAllocEx(
                    hProc,
                    new IntPtr(up),
                    new IntPtr(size),
                    MEM_COMMIT | MEM_RESERVE,
                    PAGE_EXECUTE_READWRITE);

                if (pUp != IntPtr.Zero)
                {
                    long rel = pUp.ToInt64() - (targetAddr + 5);
                    if (rel >= int.MinValue && rel <= int.MaxValue)
                        return pUp;
                }

                if (delta == 0)
                    continue;

                long down = Align64K(targetAddr - delta);
                if (down > 0)
                {
                    IntPtr pDown = VirtualAllocEx(
                        hProc,
                        new IntPtr(down),
                        new IntPtr(size),
                        MEM_COMMIT | MEM_RESERVE,
                        PAGE_EXECUTE_READWRITE);

                    if (pDown != IntPtr.Zero)
                    {
                        long rel = pDown.ToInt64() - (targetAddr + 5);
                        if (rel >= int.MinValue && rel <= int.MaxValue)
                            return pDown;
                    }
                }
            }

            return IntPtr.Zero;
        }

        private static long Align64K(long value)
        {
            return value & ~0xFFFFL;
        }

        private static bool ReadExact(IntPtr hProc, long address, byte[] buffer, out string error)
        {
            error = "";

            if (!ReadProcessMemory(hProc, new IntPtr(address), buffer, new IntPtr(buffer.Length), out IntPtr read))
            {
                error = $"WinErr={Marshal.GetLastWin32Error()}";
                return false;
            }

            if (read.ToInt64() != buffer.Length)
            {
                error = $"short read {read.ToInt64()} of {buffer.Length}";
                return false;
            }

            return true;
        }

        private static bool WriteExact(IntPtr hProc, long address, byte[] data, out string error)
        {
            error = "";

            if (!WriteProcessMemory(hProc, new IntPtr(address), data, new IntPtr(data.Length), out IntPtr written))
            {
                error = $"WriteProcessMemory failed. WinErr={Marshal.GetLastWin32Error()}";
                return false;
            }

            if (written.ToInt64() != data.Length)
            {
                error = $"short write {written.ToInt64()} of {data.Length}";
                return false;
            }

            return true;
        }

        private static bool WriteWithProtect(IntPtr hProc, long address, byte[] data, out string error)
        {
            error = "";

            if (!VirtualProtectEx(hProc, new IntPtr(address), new IntPtr(data.Length), PAGE_EXECUTE_READWRITE, out uint oldProtect))
            {
                error = $"VirtualProtectEx failed. WinErr={Marshal.GetLastWin32Error()}";
                return false;
            }

            try
            {
                return WriteExact(hProc, address, data, out error);
            }
            finally
            {
                VirtualProtectEx(hProc, new IntPtr(address), new IntPtr(data.Length), oldProtect, out _);
            }
        }

        private static string Bytes(byte[] arr)
        {
            return string.Join(" ", arr.Select(b => b.ToString("X2")));
        }

        public static void ResetAllAttempts()
        {
            lock (AlreadyTriedPids)
            {
                AlreadyTriedPids.Clear();
            }
        }

        public static void ResetAttempt(Process process)
        {
            if (process == null)
                return;

            lock (AlreadyTriedPids)
            {
                AlreadyTriedPids.Remove(process.Id);
            }
        }
    }
}