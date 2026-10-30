using System.Buffers.Binary;

namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Port of the legacy <c>LAATweaks</c> (Steam) and <c>LAATweaksOld</c> (GOG)
    /// Large-Address-Aware patching. WinForms message boxes from the originals are dropped;
    /// the byte offsets, checksum logic and toggle semantics on a valid 32-bit executable are
    /// preserved exactly.
    /// <para>
    /// Hardening over the legacy code: the PE header is fully validated (bounds-checked, PE32 /
    /// x86 only) before anything is written, so 64-bit images (Dawn of War: Definitive Edition),
    /// truncated or foreign files are never modified; the new flag and checksum are computed in
    /// memory and only then written, so a failure can no longer leave the flag patched without its
    /// checksum; a locked / read-only file is reported as an <see cref="IOException"/>.
    /// </para>
    /// </summary>
    public sealed class LaaService : ILaaService
    {
        // Steam variant (LAATweaks) uses a 16-bit flag mask.
        private const short LAA_FLAG = 0x20;

        // GOG variant (LAATweaksOld) constant.
        private const int IMAGE_FILE_LARGE_ADDRESS_AWARE = 0x20;

        private const short MZ_HEADER = 0x5A4D;
        private const int PE_HEADER_SIGNATURE = 0x4550;

        private const ushort IMAGE_FILE_MACHINE_I386 = 0x014C;
        private const ushort IMAGE_FILE_MACHINE_IA64 = 0x0200;
        private const ushort IMAGE_FILE_MACHINE_AMD64 = 0x8664;
        private const ushort IMAGE_FILE_MACHINE_ARM64 = 0xAA64;
        private const ushort IMAGE_NT_OPTIONAL_HDR32_MAGIC = 0x10B;
        private const ushort IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20B;

        // Offsets relative to the PE signature (e_lfanew), shared by both legacy algorithms.
        private const int E_LFANEW_OFFSET = 0x3C;
        private const int MACHINE_OFFSET = 0x04;
        private const int CHARACTERISTICS_OFFSET = 0x16;   // LAA flag lives here.
        private const int OPTIONAL_MAGIC_OFFSET = 0x18;
        private const int CHECKSUM_OFFSET = 0x18 + 0x40;   // Optional header start + 0x40.
        private const int CHECKSUM_FIELD_SIZE = 4;

        // A DoW executable is ~10 MB; refuse anything absurd rather than buffering it.
        private const long MAX_EXE_SIZE = 512L * 1024 * 1024;

        /// <inheritdoc />
        public LaaApplicability GetLaaApplicability(string exePath)
        {
            try
            {
                using FileStream fs = OpenForReading(exePath);
                return ReadHeader(fs).Applicability;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                           or ArgumentException or NotSupportedException)
            {
                return LaaApplicability.FileUnavailable;
            }
        }

        /// <inheritdoc />
        public bool IsLaaApplicable(string exePath) =>
            GetLaaApplicability(exePath) == LaaApplicability.Applicable;

        /// <inheritdoc />
        public bool IsLargeAddressAware(string exePath, bool gogVersion)
        {
            // Both legacy algorithms (IsLargeAware in LAATweaks and LAATweaksOld) read the same
            // 16-bit COFF Characteristics field at e_lfanew + 0x16 and test bit 0x20. That field has
            // the same position in PE32+, so the answer is also correct for 64-bit images.
            using FileStream fs = OpenForReading(exePath);
            PeHeader header = ReadHeader(fs);
            if (header.Applicability == LaaApplicability.UnsupportedImage)
                return false;

            return (header.Characteristics & LAA_FLAG) == LAA_FLAG;
        }

        /// <inheritdoc />
        public bool ToggleLAA(string exePath, bool gogVersion)
        {
            LaaApplicability applicability = GetLaaApplicability(exePath);
            if (applicability != LaaApplicability.Applicable)
                throw new LaaNotApplicableException(exePath, applicability);

            using FileStream fs = OpenForPatching(exePath);

            // Re-validate on the handle we will write through (the file may have changed).
            PeHeader header = ReadHeader(fs);
            if (header.Applicability != LaaApplicability.Applicable)
                throw new LaaNotApplicableException(exePath, header.Applicability);

            return gogVersion ? ToggleLaaGog(fs, header.PeOffset) : ToggleLaaSteam(fs, header.PeOffset);
        }

        // ----------------------------------------------------------------------------------
        // Header validation
        // ----------------------------------------------------------------------------------

        private readonly record struct PeHeader(LaaApplicability Applicability, long PeOffset, short Characteristics);

        private static PeHeader ReadHeader(Stream s)
        {
            static PeHeader Unsupported() => new(LaaApplicability.UnsupportedImage, -1, 0);

            long length = s.Length;
            if (length < E_LFANEW_OFFSET + 4)
                return Unsupported();

            Span<byte> buf = stackalloc byte[4];

            if (!ReadAt(s, 0, buf[..2]) || BinaryPrimitives.ReadInt16LittleEndian(buf) != MZ_HEADER)
                return Unsupported();

            if (!ReadAt(s, E_LFANEW_OFFSET, buf))
                return Unsupported();
            long pe = BinaryPrimitives.ReadInt32LittleEndian(buf);

            // Must at least reach the optional header magic.
            if (pe <= 0 || pe + OPTIONAL_MAGIC_OFFSET + 2 > length)
                return Unsupported();

            if (!ReadAt(s, pe, buf) || BinaryPrimitives.ReadInt32LittleEndian(buf) != PE_HEADER_SIGNATURE)
                return Unsupported();

            if (!ReadAt(s, pe + MACHINE_OFFSET, buf[..2]))
                return Unsupported();
            ushort machine = BinaryPrimitives.ReadUInt16LittleEndian(buf);

            if (!ReadAt(s, pe + CHARACTERISTICS_OFFSET, buf[..2]))
                return Unsupported();
            short characteristics = BinaryPrimitives.ReadInt16LittleEndian(buf);

            if (!ReadAt(s, pe + OPTIONAL_MAGIC_OFFSET, buf[..2]))
                return Unsupported();
            ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(buf);

            if (magic == IMAGE_NT_OPTIONAL_HDR64_MAGIC
                || machine is IMAGE_FILE_MACHINE_AMD64 or IMAGE_FILE_MACHINE_ARM64 or IMAGE_FILE_MACHINE_IA64)
                return new PeHeader(LaaApplicability.NotNeeded64Bit, pe, characteristics);

            if (machine != IMAGE_FILE_MACHINE_I386 || magic != IMAGE_NT_OPTIONAL_HDR32_MAGIC)
                return Unsupported();

            // The (legacy 16-bit) checksum write must land inside the file.
            if (pe + CHECKSUM_OFFSET + CHECKSUM_FIELD_SIZE > length || length > MAX_EXE_SIZE)
                return Unsupported();

            return new PeHeader(LaaApplicability.Applicable, pe, characteristics);
        }

        private static bool ReadAt(Stream s, long pos, Span<byte> dest)
        {
            if (pos < 0 || pos + dest.Length > s.Length)
                return false;
            s.Seek(pos, SeekOrigin.Begin);
            s.ReadExactly(dest);
            return true;
        }

        // Header probes must not fail because another process has the file open (on Windows a plain
        // OpenRead conflicts with any handle that has write access): the "in use" case is reported
        // by OpenForPatching, where it matters.
        private static FileStream OpenForReading(string exePath) =>
            new(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        private static FileStream OpenForPatching(string exePath)
        {
            try
            {
                return File.Open(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException(
                    $"{Path.GetFileName(exePath)} is read-only or you lack permission to modify it.", ex);
            }
            catch (IOException ex) when (ex is not FileNotFoundException and not DirectoryNotFoundException)
            {
                throw new IOException(
                    $"{Path.GetFileName(exePath)} is in use (is the game running?) and cannot be modified.", ex);
            }
        }

        private static byte[] ReadAll(FileStream fs)
        {
            var data = new byte[fs.Length];
            fs.Seek(0, SeekOrigin.Begin);
            fs.ReadExactly(data);
            return data;
        }

        private static void WriteAt(FileStream fs, long pos, ReadOnlySpan<byte> bytes)
        {
            fs.Seek(pos, SeekOrigin.Begin);
            fs.Write(bytes);
        }

        // ----------------------------------------------------------------------------------
        // Steam variant — port of LAATweaks.cs
        // ----------------------------------------------------------------------------------

        private static bool ToggleLaaSteam(FileStream fs, long peHeaderPos)
        {
            byte[] data = ReadAll(fs);

            long laaFlagPos = peHeaderPos + CHARACTERISTICS_OFFSET;
            long checksumPos = peHeaderPos + CHECKSUM_OFFSET;

            short laaFlag = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan((int)laaFlagPos));

            bool isLAASet = (laaFlag & LAA_FLAG) == LAA_FLAG;
            laaFlag = ToggleFlag(laaFlag, LAA_FLAG);

            // Legacy wrote the flag first and then checksummed the file including it; do the same
            // in memory so nothing hits the disk until both values are known.
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan((int)laaFlagPos), laaFlag);

            uint newChecksum = CalculatePEChecksum(data, checksumPos);

            Span<byte> flagBytes = stackalloc byte[2];
            Span<byte> sumBytes = stackalloc byte[2];
            BinaryPrimitives.WriteInt16LittleEndian(flagBytes, laaFlag);
            BinaryPrimitives.WriteInt16LittleEndian(sumBytes, (short)newChecksum); // Lower 16 bits only (legacy).

            WriteAt(fs, laaFlagPos, flagBytes);
            WriteAt(fs, checksumPos, sumBytes);
            fs.Flush(flushToDisk: true);

            return !isLAASet;
        }

        /// <summary>
        /// Exact replica of the legacy <c>LAATweaks.CalculatePEChecksum</c> stream walk, run over an
        /// in-memory copy. The only deviation is a lone trailing byte (odd file length), where the
        /// legacy <c>ReadUInt16</c> threw after the flag had already been written; it is now added
        /// as a zero-extended word.
        /// </summary>
        private static uint CalculatePEChecksum(byte[] data, long checksumPos)
        {
            uint checksum = 0;
            long fileSize = data.LongLength;

            for (long i = 0; i < fileSize; i += 4)
            {
                if (i == checksumPos)
                    continue; // Skip the checksum field itself (4 bytes).

                // Add 32-bit words to checksum.
                if (i + 4 <= fileSize)
                    checksum += BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)i, 4));
                else if (i + 2 <= fileSize)
                    checksum += BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan((int)i, 2)); // Remaining bytes.
                else
                    checksum += data[i];
            }

            // Add file length to the checksum.
            checksum += (uint)fileSize;

            // Fold the checksum into 16 bits.
            checksum = (checksum & 0xFFFF) + (checksum >> 16);
            checksum += (checksum >> 16);

            return checksum & 0xFFFF; // Return a 16-bit checksum.
        }

        private static short ToggleFlag(short value, short flag)
        {
            return (short)(value ^ flag);
        }

        // ----------------------------------------------------------------------------------
        // GOG variant — port of LAATweaksOld.cs
        // ----------------------------------------------------------------------------------

        private static bool ToggleLaaGog(FileStream fs, long peloc)
        {
            Span<byte> buf = stackalloc byte[2];

            long nFilePos = peloc + CHARACTERISTICS_OFFSET;     // LAA flag position.
            ReadAt(fs, nFilePos, buf);
            short laaFlag = BinaryPrimitives.ReadInt16LittleEndian(buf);

            long nSumPos = nFilePos + 2 + 0x40;                 // Checksum position.
            ReadAt(fs, nSumPos, buf);
            short chckSum = BinaryPrimitives.ReadInt16LittleEndian(buf);

            bool result;
            if ((laaFlag & IMAGE_FILE_LARGE_ADDRESS_AWARE) != IMAGE_FILE_LARGE_ADDRESS_AWARE)
            {
                laaFlag += IMAGE_FILE_LARGE_ADDRESS_AWARE;
                chckSum += IMAGE_FILE_LARGE_ADDRESS_AWARE;
                result = true;
            }
            else
            {
                laaFlag -= IMAGE_FILE_LARGE_ADDRESS_AWARE;
                chckSum -= IMAGE_FILE_LARGE_ADDRESS_AWARE;
                result = false;
            }

            BinaryPrimitives.WriteInt16LittleEndian(buf, laaFlag);
            WriteAt(fs, nFilePos, buf);
            BinaryPrimitives.WriteInt16LittleEndian(buf, chckSum);
            WriteAt(fs, nSumPos, buf);
            fs.Flush(flushToDisk: true);

            return result;
        }
    }
}
