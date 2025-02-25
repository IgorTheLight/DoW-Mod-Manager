using System;
using System.Diagnostics;
using System.IO;

namespace DoW_Mod_Manager
{
    public static class MemoryPatcher
    {
        // Check if Soulstorm.exe is running
        public static bool IsGameRunning()
        {
            return Process.GetProcessesByName("Soulstorm").Length > 0;
        }

        // Apply LAA patch
        public static bool ApplyLAA(string exePath)
        {
            try
            {
                if (IsFileLocked(exePath))
                {
                    Console.WriteLine("LAA patch skipped: File is locked.");
                    return false;
                }

                using (FileStream fs = new FileStream(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                using (BinaryReader br = new BinaryReader(fs))
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    fs.Seek(0x3C, SeekOrigin.Begin);
                    int peOffset = br.ReadInt32();
                    fs.Seek(peOffset + 0x16, SeekOrigin.Begin);

                    ushort characteristics = br.ReadUInt16();
                    if ((characteristics & 0x20) != 0)
                    {
                        Console.WriteLine("LAA patch skipped: Already applied.");
                        return true;
                    }

                    characteristics |= 0x20;
                    fs.Seek(peOffset + 0x16, SeekOrigin.Begin);
                    bw.Write(characteristics);
                    Console.WriteLine("LAA patch applied successfully.");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying LAA patch: {ex.Message}");
                return false;
            }
        }

        // Check if the file is locked
        private static bool IsFileLocked(string filePath)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return true;
            }
        }

        // Apply Overdrive LAA patch
        public static bool ApplyOverdriveLAA(string exePath)
        {
            if (IsGameRunning())
            {
                Console.WriteLine("Overdrive LAA patch skipped: Game is running.");
                return false;
            }
            return ApplyLAA(exePath);
        }

        // Calculate file offset from virtual address using PE header
        private static long GetFileOffsetFromVA(string exePath, uint virtualAddress)
        {
            try
            {
                using (FileStream fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    // Read DOS header
                    fs.Seek(0x3C, SeekOrigin.Begin);
                    int peOffset = br.ReadInt32();

                    // Read PE signature and file header
                    fs.Seek(peOffset + 4, SeekOrigin.Begin);
                    short numberOfSections = br.ReadInt16();
                    fs.Seek(peOffset + 0x18, SeekOrigin.Begin); // Optional Header offset
                    uint imageBase = br.ReadUInt32(); // Usually 0x00400000

                    // Skip to section table (after Optional Header, size 0xE0 for 32-bit)
                    fs.Seek(peOffset + 0xF8, SeekOrigin.Begin);

                    for (int i = 0; i < numberOfSections; i++)
                    {
                        uint virtualAddr = br.ReadUInt32();
                        uint virtualSize = br.ReadUInt32();
                        uint rawOffset = br.ReadUInt32();
                        fs.Seek(0x10, SeekOrigin.Current); // Skip rest of section header

                        if (virtualAddress >= virtualAddr && virtualAddress < virtualAddr + virtualSize)
                        {
                            long fileOffset = rawOffset + (virtualAddress - virtualAddr);
                            Console.WriteLine($"VA 0x{virtualAddress:X} maps to file offset 0x{fileOffset:X}");
                            return fileOffset;
                        }
                    }
                }
                Console.WriteLine("VA not found in any section.");
                return -1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating file offset: {ex.Message}");
                return -1;
            }
        }

        // Search for a byte pattern near an expected offset
        private static long FindPattern(string exePath, byte[] pattern, long expectedOffset, int searchRange = 0x1000)
        {
            try
            {
                using (FileStream fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    long start = Math.Max(0, expectedOffset - searchRange);
                    fs.Seek(start, SeekOrigin.Begin);
                    byte[] buffer = br.ReadBytes((int)Math.Min(searchRange * 2, fs.Length - start));
                    for (int i = 0; i <= buffer.Length - pattern.Length; i++)
                    {
                        bool match = true;
                        for (int j = 0; j < pattern.Length; j++)
                        {
                            if (buffer[i + j] != pattern[j])
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match)
                        {
                            long foundOffset = start + i;
                            Console.WriteLine($"Pattern found at file offset 0x{foundOffset:X}");
                            return foundOffset;
                        }
                    }
                }
                Console.WriteLine("Pattern not found near expected offset.");
                return -1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching for pattern: {ex.Message}");
                return -1;
            }
        }

        // Apply multiplayer lobby patch
        public static bool PatchMultiplayerLobby(string exePath)
        {
            try
            {
                if (IsFileLocked(exePath))
                {
                    Console.WriteLine("Multiplayer lobby patch skipped: File is locked.");
                    return false;
                }

                byte[] signature = new byte[] { 0x75, 0x40 }; // JNZ instruction
                byte[] patchBytes = new byte[] { 0x90, 0x90 }; // NOPs
                uint targetVA = 0x007D1496; // Known memory address from C++

                // Calculate exact file offset
                long patchOffset = GetFileOffsetFromVA(exePath, targetVA);
                if (patchOffset == -1)
                {
                    Console.WriteLine("Falling back to signature scan...");
                    patchOffset = FindPattern(exePath, signature, 0x3D2496); // Rough estimate as fallback
                    if (patchOffset == -1)
                    {
                        Console.WriteLine("Multiplayer lobby patch failed: Could not locate patch point.");
                        return false;
                    }
                }

                // Verify and patch
                using (FileStream fs = new FileStream(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                using (BinaryReader br = new BinaryReader(fs))
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    fs.Seek(patchOffset, SeekOrigin.Begin);
                    byte[] currentBytes = br.ReadBytes(signature.Length);
                    if (currentBytes[0] == patchBytes[0] && currentBytes[1] == patchBytes[1])
                    {
                        Console.WriteLine("Multiplayer lobby already patched.");
                        return true;
                    }
                    if (currentBytes[0] != signature[0] || currentBytes[1] != signature[1])
                    {
                        Console.WriteLine($"Unexpected bytes at 0x{patchOffset:X}: {BitConverter.ToString(currentBytes)}. Proceeding anyway.");
                    }

                    fs.Seek(patchOffset, SeekOrigin.Begin);
                    bw.Write(patchBytes);
                    Console.WriteLine($"Multiplayer lobby patched at file offset 0x{patchOffset:X}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying multiplayer lobby patch: {ex.Message}");
                return false;
            }
        }

        // Apply all patches
        public static bool ApplyAllPatches(string exePath)
        {
            if (IsGameRunning())
            {
                Console.WriteLine("Cannot apply patches: Game is running.");
                return false;
            }

            bool laaSuccess = ApplyLAA(exePath);
            Console.WriteLine(laaSuccess ? "LAA patch step completed." : "LAA patch step failed.");

            bool lobbySuccess = PatchMultiplayerLobby(exePath);
            Console.WriteLine(lobbySuccess ? "Multiplayer lobby patch step completed." : "Multiplayer lobby patch step failed.");

            return laaSuccess && lobbySuccess;
        }
    }
}