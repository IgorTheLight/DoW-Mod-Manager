using System.Text;

namespace DowModManager.Tests
{
    /// <summary>
    /// Frozen copy of the pre-hardening LaaService, used as a byte-level oracle by
    /// LaaServiceTests. Do not modify. Verbatim port of the legacy <c>LAATweaks</c> (Steam) and <c>LAATweaksOld</c> (GOG)
    /// Large-Address-Aware patching. WinForms message boxes from the originals are dropped;
    /// the byte offsets, checksum logic and toggle semantics are preserved exactly.
    /// </summary>
    internal sealed class LegacyLaaReference
    {
        // Steam variant (LAATweaks) uses a 16-bit flag mask.
        private const short LAA_FLAG = 0x20;

        // GOG variant (LAATweaksOld) constant.
        private const int IMAGE_FILE_LARGE_ADDRESS_AWARE = 0x20;

        private const short MZ_HEADER = 0x5A4D;
        private const int PE_HEADER_SIGNATURE = 0x4550;

        /// <summary>Legacy behaviour.</summary>
        public bool IsLargeAddressAware(string exePath, bool gogVersion)
        {
            return gogVersion ? IsLargeAwareGog(exePath) : IsLargeAwareSteam(exePath);
        }

        /// <summary>Legacy behaviour.</summary>
        public bool ToggleLAA(string exePath, bool gogVersion)
        {
            return gogVersion ? ToggleLaaGog(exePath) : ToggleLaaSteam(exePath);
        }

        // ----------------------------------------------------------------------------------
        // Steam variant — port of LAATweaks.cs
        // ----------------------------------------------------------------------------------

        private static bool IsLargeAwareSteam(string file)
        {
            using FileStream fs = File.OpenRead(file);
            using BinaryReader br = new(fs);

            if (!IsValidExecutable(br)) return false;

            long peHeaderPos = GetPEHeaderPosition(br);
            if (peHeaderPos == -1) return false;

            long laaFlagPos = GetLAAFlagPosition(br, peHeaderPos);
            if (laaFlagPos == -1) return false;

            short laaFlag = br.ReadInt16();

            // Check if the LAA flag is set.
            return (laaFlag & LAA_FLAG) == LAA_FLAG;
        }

        private static bool ToggleLaaSteam(string file)
        {
            using FileStream fs = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using BinaryReader br = new(fs);
            using BinaryWriter bw = new(fs);

            if (!IsValidExecutable(br)) return false;

            long peHeaderPos = GetPEHeaderPosition(br);
            if (peHeaderPos == -1) return false;

            long laaFlagPos = GetLAAFlagPosition(br, peHeaderPos);
            if (laaFlagPos == -1) return false;

            short laaFlag = br.ReadInt16();

            long checksumPos = GetChecksumPosition(br, peHeaderPos);
            if (checksumPos == -1) return false;

            // Read original checksum (preserved to mirror legacy behaviour).
            _ = br.ReadInt16();

            bool isLAASet = (laaFlag & LAA_FLAG) == LAA_FLAG;
            laaFlag = ToggleFlag(laaFlag, LAA_FLAG);

            // Write updated LAA flag.
            bw.Seek((int)laaFlagPos, SeekOrigin.Begin);
            bw.Write(laaFlag);

            // Calculate and write new checksum.
            uint newChecksum = CalculatePEChecksum(fs, checksumPos);
            bw.Seek((int)checksumPos, SeekOrigin.Begin);
            bw.Write((short)newChecksum); // Writing lower 16 bits (checksum field is 16 bits).

            return !isLAASet;
        }

        private static long GetPEHeaderPosition(BinaryReader br)
        {
            br.BaseStream.Seek(0x3C, SeekOrigin.Begin);
            int peHeaderOffset = br.ReadInt32(); // Location of PE header (at 0x3C in MZ header).

            br.BaseStream.Seek(peHeaderOffset, SeekOrigin.Begin);
            if (br.ReadInt32() == 0x4550)  // PE signature (0x4550 = "PE\0\0").
                return peHeaderOffset;

            return -1;  // PE header not found.
        }

        private static long GetLAAFlagPosition(BinaryReader br, long peHeaderPos)
        {
            br.BaseStream.Seek(peHeaderPos + 0x4, SeekOrigin.Begin);  // Skip to COFF header.
            _ = br.ReadInt16(); // Machine type (not used here).
            _ = br.ReadInt16(); // Number of sections (not used here).

            // The Characteristics field (where the LAA flag is located) is at offset 0x16
            // from the PE header.
            br.BaseStream.Seek(peHeaderPos + 0x16, SeekOrigin.Begin);
            return br.BaseStream.Position;
        }

        private static long GetChecksumPosition(BinaryReader br, long peHeaderPos)
        {
            br.BaseStream.Seek(peHeaderPos + 0x14, SeekOrigin.Begin);  // Skip to Optional Header start.
            _ = br.ReadInt16();  // Size of Optional Header.

            // The checksum field is at offset 0x40 from the beginning of the Optional Header.
            long checksumPos = peHeaderPos + 0x18 + 0x40;
            if (checksumPos < br.BaseStream.Length)
            {
                br.BaseStream.Seek(checksumPos, SeekOrigin.Begin);
                return checksumPos;
            }

            return -1;  // Checksum position not found.
        }

        private static uint CalculatePEChecksum(FileStream fs, long checksumPos)
        {
            uint checksum = 0;
            long fileSize = fs.Length;

            // Save the current position to restore later.
            long originalPos = fs.Position;

            fs.Seek(0, SeekOrigin.Begin);  // Start from the beginning of the file.

            using (BinaryReader br = new(fs, Encoding.Default, leaveOpen: true))
            {
                for (long i = 0; i < fileSize; i += 4)
                {
                    if (i == checksumPos)
                    {
                        br.BaseStream.Seek(4, SeekOrigin.Current); // Skip the checksum field itself (4 bytes).
                        continue;
                    }

                    // Add 32-bit words to checksum.
                    if (i + 4 <= fileSize)
                        checksum += br.ReadUInt32();
                    else
                        checksum += br.ReadUInt16(); // Handle remaining bytes.
                }
            }

            // Add file length to the checksum.
            checksum += (uint)fileSize;

            // Fold the checksum into 16 bits.
            checksum = (checksum & 0xFFFF) + (checksum >> 16);
            checksum += (checksum >> 16);

            // Restore the original file position.
            fs.Seek(originalPos, SeekOrigin.Begin);

            return checksum & 0xFFFF; // Return a 16-bit checksum.
        }

        private static bool IsValidExecutable(BinaryReader br)
        {
            if (br.ReadInt16() != MZ_HEADER) return false;

            br.BaseStream.Position = 0x3C;
            int peHeaderLoc = br.ReadInt32();

            br.BaseStream.Position = peHeaderLoc;
            return br.ReadInt32() == PE_HEADER_SIGNATURE;
        }

        private static short ToggleFlag(short value, short flag)
        {
            return (short)(value ^ flag);
        }

        // ----------------------------------------------------------------------------------
        // GOG variant — port of LAATweaksOld.cs
        // ----------------------------------------------------------------------------------

        private static bool IsLargeAwareGog(string file)
        {
            using FileStream fs = File.OpenRead(file);
            using BinaryReader br = new(fs);

            if (br.ReadInt16() != 0x5A4D)       // No MZ Header.
                return false;

            br.BaseStream.Position = 0x3C;
            int peloc = br.ReadInt32();         // Get the PE header location.

            br.BaseStream.Position = peloc;
            if (br.ReadInt32() != 0x4550)       // No PE header.
                return false;

            br.BaseStream.Position += 0x12;     // LAA flag position.
            short laaFlag = br.ReadInt16();

            return (laaFlag & IMAGE_FILE_LARGE_ADDRESS_AWARE) == IMAGE_FILE_LARGE_ADDRESS_AWARE;
        }

        private static bool ToggleLaaGog(string file)
        {
            bool result = false;

            using FileStream fs = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            BinaryReader br = new(fs);

            if (br.ReadInt16() != 0x5A4D)       // No MZ Header.
                return false;

            br.BaseStream.Position = 0x3C;
            int peloc = br.ReadInt32();         // Get the PE header location.

            br.BaseStream.Position = peloc;
            if (br.ReadInt32() != 0x4550)       // No PE header.
                return false;

            br.BaseStream.Position += 0x12;     // LAA flag position.
            long nFilePos = (int)br.BaseStream.Position;
            short laaFlag = br.ReadInt16();

            br.BaseStream.Position += 0x40;     // Checksum position.
            long nSumPos = (int)br.BaseStream.Position;
            short chckSum = br.ReadInt16();

            BinaryWriter bw = new(fs);
            if ((laaFlag & IMAGE_FILE_LARGE_ADDRESS_AWARE) != IMAGE_FILE_LARGE_ADDRESS_AWARE)
            {
                laaFlag += IMAGE_FILE_LARGE_ADDRESS_AWARE;
                chckSum += IMAGE_FILE_LARGE_ADDRESS_AWARE;
                bw.Seek((int)nFilePos, SeekOrigin.Begin);
                bw.Write(laaFlag);
                bw.Seek((int)nSumPos, SeekOrigin.Begin);
                bw.Write(chckSum);
                bw.Flush();
                result = true;
            }
            else if ((laaFlag & IMAGE_FILE_LARGE_ADDRESS_AWARE) == IMAGE_FILE_LARGE_ADDRESS_AWARE)
            {
                laaFlag -= IMAGE_FILE_LARGE_ADDRESS_AWARE;
                chckSum -= IMAGE_FILE_LARGE_ADDRESS_AWARE;
                bw.Seek((int)nFilePos, SeekOrigin.Begin);
                bw.Write(laaFlag);
                bw.Seek((int)nSumPos, SeekOrigin.Begin);
                bw.Write(chckSum);
                bw.Flush();
                result = false;
            }

            return result;
        }
    }
}
