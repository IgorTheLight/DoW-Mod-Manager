using System;
using System.IO;

namespace DoW_Mod_Manager
{
    static class LAATweaks
    {
        const short LAA_FLAG = 0x20;

        public static bool IsLargeAddressAware(string file)
        {
            try
            {
                using (FileStream fs = File.OpenRead(file))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    if (!IsValidExecutable(br)) return false;

                    long peHeaderPos = GetPEHeaderPosition(br);
                    if (peHeaderPos == -1) return false;

                    long laaFlagPos = GetLAAFlagPosition(br, peHeaderPos);
                    if (laaFlagPos == -1) return false;

                    short laaFlag = br.ReadInt16();

                    // Check if the LAA flag is set
                    return (laaFlag & LAA_FLAG) == LAA_FLAG;
                }
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(ex.Message, "ERROR:");
                return false;
            }
        }


        public static bool ToggleLAA(string file)
        {
            try
            {
                using (FileStream fs = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                using (BinaryReader br = new BinaryReader(fs))
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    if (!IsValidExecutable(br)) return false;

                    long peHeaderPos = GetPEHeaderPosition(br);
                    if (peHeaderPos == -1) return false;

                    long laaFlagPos = GetLAAFlagPosition(br, peHeaderPos);
                    if (laaFlagPos == -1) return false;

                    short laaFlag = br.ReadInt16();

                    long checksumPos = GetChecksumPosition(br, peHeaderPos);
                    if (checksumPos == -1) return false;

                    short originalChecksum = br.ReadInt16();

                    bool isLAASet = (laaFlag & LAA_FLAG) == LAA_FLAG;
                    laaFlag = ToggleFlag(laaFlag, LAA_FLAG);

                    // Write updated LAA flag
                    bw.Seek((int)laaFlagPos, SeekOrigin.Begin);
                    bw.Write(laaFlag);

                    // Calculate and write new checksum
                    uint newChecksum = CalculatePEChecksum(fs, checksumPos);
                    bw.Seek((int)checksumPos, SeekOrigin.Begin);
                    bw.Write((short)newChecksum); // Writing lower 16 bits (checksum field is 16 bits)

                    return !isLAASet;
                }
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(ex.Message, "ERROR:");
                return false;
            }
        }

        static long GetPEHeaderPosition(BinaryReader br)
        {
            br.BaseStream.Seek(0x3C, SeekOrigin.Begin);
            int peHeaderOffset = br.ReadInt32(); // Location of PE header (at 0x3C in MZ header)

            br.BaseStream.Seek(peHeaderOffset, SeekOrigin.Begin);
            if (br.ReadInt32() == 0x4550)  // PE signature (0x4550 = "PE\0\0")
                return peHeaderOffset;

            return -1;  // PE header not found
        }

        static long GetLAAFlagPosition(BinaryReader br, long peHeaderPos)
        {
            br.BaseStream.Seek(peHeaderPos + 0x4, SeekOrigin.Begin);  // Skip to COFF header
            short machineType = br.ReadInt16(); // Machine type (not used here)
            short numberOfSections = br.ReadInt16(); // Number of sections (not used here)

            // The Characteristics field (where the LAA flag is located) is at offset 0x16 from the PE header.
            br.BaseStream.Seek(peHeaderPos + 0x16, SeekOrigin.Begin);
            return br.BaseStream.Position;
        }

        static long GetChecksumPosition(BinaryReader br, long peHeaderPos)
        {
            br.BaseStream.Seek(peHeaderPos + 0x14, SeekOrigin.Begin);  // Skip to Optional Header start
            short optionalHeaderSize = br.ReadInt16();  // Size of Optional Header

            // The checksum field is at offset 0x40 from the beginning of the Optional Header.
            long checksumPos = peHeaderPos + 0x18 + 0x40;
            if (checksumPos < br.BaseStream.Length)
            {
                br.BaseStream.Seek(checksumPos, SeekOrigin.Begin);
                return checksumPos;
            }

            return -1;  // Checksum position not found
        }

        static uint CalculatePEChecksum(FileStream fs, long checksumPos)
        {
            uint checksum = 0;
            long fileSize = fs.Length;

            // Save the current position to restore later
            long originalPos = fs.Position;

            fs.Seek(0, SeekOrigin.Begin);  // Start from the beginning of the file

            using (BinaryReader br = new BinaryReader(fs, System.Text.Encoding.Default, leaveOpen: true))
            {
                for (long i = 0; i < fileSize; i += 4)
                {
                    if (i == checksumPos)
                    {
                        br.BaseStream.Seek(4, SeekOrigin.Current); // Skip the checksum field itself (4 bytes)
                        continue;
                    }

                    // Add 32-bit words to checksum
                    if (i + 4 <= fileSize)
                        checksum += br.ReadUInt32();
                    else
                        checksum += br.ReadUInt16(); // Handle remaining bytes
                }
            }

            // Add file length to the checksum
            checksum += (uint)fileSize;

            // Fold the checksum into 16 bits
            checksum = (checksum & 0xFFFF) + (checksum >> 16);
            checksum += (checksum >> 16);

            // Restore the original file position
            fs.Seek(originalPos, SeekOrigin.Begin);

            return checksum & 0xFFFF; // Return a 16-bit checksum
        }

        static bool IsValidExecutable(BinaryReader br)
        {
            const short MZ_HEADER = 0x5A4D;
            const int PE_HEADER_SIGNATURE = 0x4550;

            if (br.ReadInt16() != MZ_HEADER) return false;

            br.BaseStream.Position = 0x3C;
            int peHeaderLoc = br.ReadInt32();

            br.BaseStream.Position = peHeaderLoc;
            return br.ReadInt32() == PE_HEADER_SIGNATURE;
        }

        static short ToggleFlag(short value, short flag)
        {
            return (short)(value ^ flag);
        }
    }
}