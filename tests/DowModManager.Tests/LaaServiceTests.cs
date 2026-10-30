using DoW_Mod_Manager.Services;
using Xunit;

namespace DowModManager.Tests
{
    public class LaaServiceTests : IDisposable
    {
        private readonly string _dir;
        private readonly ILaaService _service = new LaaService();

        public LaaServiceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dmm_laa_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        /// <summary>
        /// Builds a minimal but structurally valid PE image:
        /// MZ header, e_lfanew at 0x3C pointing to a PE signature, COFF header with a
        /// Characteristics field, and an optional header large enough to contain the
        /// checksum field at optionalHeaderStart + 0x40.
        /// </summary>
        private static byte[] BuildMinimalPe(bool laaSet, ushort machine = 0x014C, ushort magic = 0x10B, int extraBytes = 0)
        {
            // Layout: PE header at 0x80, leaving plenty of room.
            const int peHeaderPos = 0x80;
            // peHeaderPos + 0x18 = optional header start; checksum at +0x40; field is 4 bytes.
            // Allocate generously and pad to a multiple of 4 for the checksum walk.
            int size = peHeaderPos + 0x18 + 0x40 + 0x40;
            if (size % 4 != 0) size += 4 - (size % 4);
            size += extraBytes;

            var buf = new byte[size];

            // MZ header.
            buf[0] = 0x4D; // 'M'
            buf[1] = 0x5A; // 'Z'

            // e_lfanew at 0x3C -> peHeaderPos.
            BitConverter.GetBytes(peHeaderPos).CopyTo(buf, 0x3C);

            // PE signature "PE\0\0" at peHeaderPos.
            buf[peHeaderPos] = 0x50;     // 'P'
            buf[peHeaderPos + 1] = 0x45; // 'E'
            buf[peHeaderPos + 2] = 0x00;
            buf[peHeaderPos + 3] = 0x00;

            // COFF header machine (sections left as zero).
            BitConverter.GetBytes(machine).CopyTo(buf, peHeaderPos + 0x4);

            // Optional header magic field starts at peHeaderPos + 0x18 (0x10B = PE32, 0x20B = PE32+).
            // The legacy code reads "Size of Optional Header" at peHeaderPos + 0x14 but does not
            // use it for the checksum position, so we leave it zero.
            BitConverter.GetBytes(magic).CopyTo(buf, peHeaderPos + 0x18);

            // Characteristics (LAA flag) is a 16-bit field at peHeaderPos + 0x16.
            short characteristics = (short)(laaSet ? 0x20 : 0x00);
            BitConverter.GetBytes(characteristics).CopyTo(buf, peHeaderPos + 0x16);

            return buf;
        }

        [Theory]
        [InlineData(false)] // Steam algorithm
        [InlineData(true)]  // GOG algorithm
        public void IsLargeAddressAware_ReflectsFlag(bool gog)
        {
            string set = Path.Combine(_dir, "set.exe");
            string clear = Path.Combine(_dir, "clear.exe");
            File.WriteAllBytes(set, BuildMinimalPe(laaSet: true));
            File.WriteAllBytes(clear, BuildMinimalPe(laaSet: false));

            Assert.True(_service.IsLargeAddressAware(set, gog));
            Assert.False(_service.IsLargeAddressAware(clear, gog));
        }

        [Theory]
        [InlineData(false)] // Steam algorithm
        [InlineData(true)]  // GOG algorithm
        public void ToggleLAA_FlipsFlag(bool gog)
        {
            string exe = Path.Combine(_dir, "toggle.exe");
            File.WriteAllBytes(exe, BuildMinimalPe(laaSet: false));

            Assert.False(_service.IsLargeAddressAware(exe, gog));

            bool nowSet = _service.ToggleLAA(exe, gog);
            Assert.True(nowSet);
            Assert.True(_service.IsLargeAddressAware(exe, gog));

            bool afterSecond = _service.ToggleLAA(exe, gog);
            Assert.False(afterSecond);
            Assert.False(_service.IsLargeAddressAware(exe, gog));
        }

        [Fact]
        public void IsLargeAddressAware_ReturnsFalse_ForNonPe()
        {
            string notExe = Path.Combine(_dir, "notexe.bin");
            File.WriteAllBytes(notExe, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 });

            Assert.False(_service.IsLargeAddressAware(notExe, gogVersion: false));
        }

        // ------------------------------------------------------------------ applicability / 64-bit

        private const ushort I386 = 0x014C;
        private const ushort Amd64 = 0x8664;
        private const ushort Pe32Magic = 0x10B;
        private const ushort Pe32PlusMagic = 0x20B;

        [Fact]
        public void GetLaaApplicability_Pe32_IsApplicable()
        {
            string exe = Path.Combine(_dir, "pe32.exe");
            File.WriteAllBytes(exe, BuildMinimalPe(laaSet: false, I386, Pe32Magic));

            Assert.Equal(LaaApplicability.Applicable, _service.GetLaaApplicability(exe));
            Assert.True(_service.IsLaaApplicable(exe));
        }

        [Theory]
        [InlineData(Amd64, Pe32PlusMagic)] // DoW: Definitive Edition W40k.exe
        [InlineData(I386, Pe32PlusMagic)]  // inconsistent, but PE32+ layout
        [InlineData(Amd64, Pe32Magic)]     // inconsistent, but 64-bit machine
        public void GetLaaApplicability_64Bit_IsNotNeeded(ushort machine, ushort magic)
        {
            string exe = Path.Combine(_dir, "pe64.exe");
            File.WriteAllBytes(exe, BuildMinimalPe(laaSet: true, machine, magic));

            Assert.Equal(LaaApplicability.NotNeeded64Bit, _service.GetLaaApplicability(exe));
            Assert.False(_service.IsLaaApplicable(exe));
        }

        [Fact]
        public void GetLaaApplicability_UnknownMachineOrMagic_IsUnsupported()
        {
            string zeroed = Path.Combine(_dir, "zero.exe");
            File.WriteAllBytes(zeroed, BuildMinimalPe(laaSet: false, machine: 0, magic: 0));
            Assert.Equal(LaaApplicability.UnsupportedImage, _service.GetLaaApplicability(zeroed));

            string rom = Path.Combine(_dir, "rom.exe");
            File.WriteAllBytes(rom, BuildMinimalPe(laaSet: false, I386, magic: 0x107));
            Assert.Equal(LaaApplicability.UnsupportedImage, _service.GetLaaApplicability(rom));
        }

        [Fact]
        public void GetLaaApplicability_NonPeTruncatedOrMissing_IsNotApplicable()
        {
            string notExe = Path.Combine(_dir, "notexe.bin");
            File.WriteAllBytes(notExe, new byte[] { 0x00, 0x01, 0x02, 0x03 });
            Assert.Equal(LaaApplicability.UnsupportedImage, _service.GetLaaApplicability(notExe));

            // Valid MZ + PE signature + magic, but cut off before the checksum field.
            string truncated = Path.Combine(_dir, "truncated.exe");
            File.WriteAllBytes(truncated, BuildMinimalPe(laaSet: false).AsSpan(0, 0x80 + 0x40).ToArray());
            Assert.Equal(LaaApplicability.UnsupportedImage, _service.GetLaaApplicability(truncated));

            // e_lfanew pointing past the end of the file.
            byte[] bad = BuildMinimalPe(laaSet: false);
            BitConverter.GetBytes(0x7FFF_0000).CopyTo(bad, 0x3C);
            string badOffset = Path.Combine(_dir, "badoffset.exe");
            File.WriteAllBytes(badOffset, bad);
            Assert.Equal(LaaApplicability.UnsupportedImage, _service.GetLaaApplicability(badOffset));

            Assert.Equal(LaaApplicability.FileUnavailable,
                _service.GetLaaApplicability(Path.Combine(_dir, "missing.exe")));
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        public void ToggleLAA_Pe32Plus_RefusesAndLeavesFileByteIdentical(bool gog, bool laaSet)
        {
            string exe = Path.Combine(_dir, "w40k_de.exe");
            byte[] original = BuildMinimalPe(laaSet, Amd64, Pe32PlusMagic);
            File.WriteAllBytes(exe, original);

            var ex = Assert.Throws<LaaNotApplicableException>(() => _service.ToggleLAA(exe, gog));
            Assert.Equal(LaaApplicability.NotNeeded64Bit, ex.Applicability);
            Assert.Contains("64-bit", ex.Message);

            Assert.Equal(original, File.ReadAllBytes(exe));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ToggleLAA_Truncated_RefusesAndLeavesFileByteIdentical(bool gog)
        {
            // Legacy would have accepted this (checksumPos < Length) and extended the file.
            byte[] truncated = BuildMinimalPe(laaSet: false).AsSpan(0, 0x80 + 0x5A).ToArray();
            string exe = Path.Combine(_dir, "trunc.exe");
            File.WriteAllBytes(exe, truncated);

            Assert.Throws<LaaNotApplicableException>(() => _service.ToggleLAA(exe, gog));
            Assert.Equal(truncated, File.ReadAllBytes(exe));
        }

        [Fact]
        public void IsLargeAddressAware_Pe32Plus_ReportsActualFlag()
        {
            string set = Path.Combine(_dir, "pe64set.exe");
            string clear = Path.Combine(_dir, "pe64clear.exe");
            File.WriteAllBytes(set, BuildMinimalPe(laaSet: true, Amd64, Pe32PlusMagic));
            File.WriteAllBytes(clear, BuildMinimalPe(laaSet: false, Amd64, Pe32PlusMagic));

            Assert.True(_service.IsLargeAddressAware(set, gogVersion: false));
            Assert.False(_service.IsLargeAddressAware(clear, gogVersion: true));
        }

        [Fact]
        public void IsLargeAddressAware_Truncated_ReturnsFalse()
        {
            string exe = Path.Combine(_dir, "short.exe");
            File.WriteAllBytes(exe, BuildMinimalPe(laaSet: true).AsSpan(0, 0x80 + 0x10).ToArray());

            Assert.False(_service.IsLargeAddressAware(exe, gogVersion: false));
        }

        // ------------------------------------------------------------------ legacy byte equivalence

        /// <summary>A PE32 image with pseudo-random payload so the checksum walk covers real data.</summary>
        private static byte[] BuildNoisyPe32(bool laaSet, int extraBytes, int seed)
        {
            byte[] buf = BuildMinimalPe(laaSet, I386, Pe32Magic, extraBytes);
            var rng = new Random(seed);

            // Randomise everything except MZ, e_lfanew and the PE signature / COFF / magic fields.
            for (int i = 0; i < buf.Length; i++)
            {
                bool reserved = i < 2 || (i >= 0x3C && i < 0x40) || (i >= 0x80 && i < 0x80 + 0x1A);
                if (!reserved)
                    buf[i] = (byte)rng.Next(256);
            }

            // Other Characteristics bits random-but-fixed; LAA bit as requested.
            short characteristics = (short)((rng.Next(0x10000) & ~0x20) | (laaSet ? 0x20 : 0));
            BitConverter.GetBytes(characteristics).CopyTo(buf, 0x80 + 0x16);
            return buf;
        }

        [Theory]
        [InlineData(false, false, 0)]
        [InlineData(false, true, 0)]
        [InlineData(false, false, 4096)]
        [InlineData(false, true, 2)]   // length % 4 == 2 exercises the 16-bit tail word
        [InlineData(false, false, 3)]  // length % 4 == 3
        [InlineData(true, false, 0)]
        [InlineData(true, true, 4096)]
        [InlineData(true, false, 3)]
        public void ToggleLAA_Pe32_MatchesLegacyBytesExactly(bool gog, bool laaSet, int extraBytes)
        {
            var legacy = new LegacyLaaReference();
            for (int seed = 1; seed <= 3; seed++)
            {
                byte[] image = BuildNoisyPe32(laaSet, extraBytes, seed);
                string mine = Path.Combine(_dir, $"mine{seed}.exe");
                string theirs = Path.Combine(_dir, $"legacy{seed}.exe");
                File.WriteAllBytes(mine, image);
                File.WriteAllBytes(theirs, image);

                // Two round trips: enable then disable (or vice versa).
                for (int round = 0; round < 2; round++)
                {
                    bool expected = legacy.ToggleLAA(theirs, gog);
                    bool actual = _service.ToggleLAA(mine, gog);

                    Assert.Equal(expected, actual);
                    Assert.Equal(File.ReadAllBytes(theirs), File.ReadAllBytes(mine));
                    Assert.Equal(legacy.IsLargeAddressAware(theirs, gog), _service.IsLargeAddressAware(mine, gog));
                }
            }
        }

        [Fact]
        public void ToggleLAA_Steam_OddLengthPe32_DoesNotThrowAndFlipsFlag()
        {
            // Legacy threw EndOfStreamException here *after* writing the flag (stale checksum).
            string exe = Path.Combine(_dir, "odd.exe");
            File.WriteAllBytes(exe, BuildMinimalPe(laaSet: false, extraBytes: 1));

            Assert.True(_service.ToggleLAA(exe, gogVersion: false));
            Assert.True(_service.IsLargeAddressAware(exe, gogVersion: false));
        }

        [Fact]
        public void ToggleLAA_ReadOnlyFile_ThrowsIOExceptionAndDoesNotWrite()
        {
            if (!OperatingSystem.IsWindows() && Environment.UserName == "root")
                return; // root ignores permission bits.

            string exe = Path.Combine(_dir, "ro.exe");
            byte[] original = BuildMinimalPe(laaSet: false);
            File.WriteAllBytes(exe, original);
            File.SetAttributes(exe, FileAttributes.ReadOnly);

            try
            {
                var ex = Assert.Throws<IOException>(() => _service.ToggleLAA(exe, gogVersion: false));
                Assert.Contains("read-only", ex.Message);
                Assert.Equal(original, File.ReadAllBytes(exe));
            }
            finally
            {
                File.SetAttributes(exe, FileAttributes.Normal);
            }
        }

        [Fact]
        public void ToggleLAA_FileInUse_ThrowsIOExceptionAndDoesNotWrite()
        {
            string exe = Path.Combine(_dir, "busy.exe");
            byte[] original = BuildMinimalPe(laaSet: false);
            File.WriteAllBytes(exe, original);

            // The toggle opens with FileShare.None, which conflicts with this open handle (natively
            // on Windows, via .NET's advisory flock on Unix).
            using (var holder = new FileStream(exe, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                var ex = Assert.Throws<IOException>(() => _service.ToggleLAA(exe, gogVersion: true));
                Assert.Contains("in use", ex.Message);
            }

            Assert.Equal(original, File.ReadAllBytes(exe));
        }
    }
}
