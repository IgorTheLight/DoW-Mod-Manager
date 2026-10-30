using System;
using System.Buffers.Binary;
using System.IO;

namespace DoW_Mod_Manager.Platform.Linux;

/// <summary>
/// Some 32-bit installers (NSIS stubs, e.g. Unification's) have no relocation table but still set
/// the ASLR flag (<c>IMAGE_DLLCHARACTERISTICS_DYNAMIC_BASE</c>). Windows ignores the flag for such
/// images and loads them at their base address; Wine's WoW64 mode (GE-Proton 10, Proton 10)
/// honours it, moves the image and the program crashes on its first absolute address. They run
/// from a copy without the flag, next to the original so they still find the files beside them.
/// </summary>
public static class FixedBaseWorkaround
{
    private const ushort RelocsStripped = 0x0001;
    private const ushort DynamicBase = 0x0040;
    private const ushort Pe32Magic = 0x10b;

    /// <summary>True for a 32-bit PE header with stripped relocations and the ASLR flag set.</summary>
    public static bool NeedsCopy(ReadOnlySpan<byte> header) =>
        DllCharacteristicsOffset(header) is { } offset
        && (BinaryPrimitives.ReadUInt16LittleEndian(header[offset..]) & DynamicBase) != 0;

    /// <summary>
    /// The copy to run instead of <paramref name="exePath"/>: <c>.&lt;name&gt;.dmm.exe</c> beside it,
    /// without the ASLR flag. Null when the program does not need one or the copy cannot be made.
    /// </summary>
    public static string? CreateCopy(string exePath)
    {
        try
        {
            var header = new byte[4096];
            int read;
            using (var file = File.OpenRead(exePath))
                read = file.Read(header, 0, header.Length);
            if (!NeedsCopy(header.AsSpan(0, read)))
                return null;

            string copy = Path.Combine(Path.GetDirectoryName(exePath)!,
                                       "." + Path.GetFileNameWithoutExtension(exePath) + ".dmm.exe");
            File.Copy(exePath, copy, overwrite: true);
            int offset = DllCharacteristicsOffset(header)!.Value;
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(offset));
            using (var file = File.OpenWrite(copy))
            {
                file.Position = offset;
                Span<byte> patched = stackalloc byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(patched, (ushort)(flags & ~DynamicBase));
                file.Write(patched);
            }
            return copy;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Offset of DllCharacteristics when the header is PE32 with stripped relocations.</summary>
    private static int? DllCharacteristicsOffset(ReadOnlySpan<byte> header)
    {
        if (header.Length < 0x40 || header[0] != 'M' || header[1] != 'Z')
            return null;
        int pe = BinaryPrimitives.ReadInt32LittleEndian(header[0x3C..]);
        int optional = pe + 24;
        if (pe < 0 || optional + 72 > header.Length
            || BinaryPrimitives.ReadUInt32LittleEndian(header[pe..]) != 0x00004550) // "PE\0\0"
            return null;

        ushort characteristics = BinaryPrimitives.ReadUInt16LittleEndian(header[(pe + 22)..]);
        ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(header[optional..]);
        return magic == Pe32Magic && (characteristics & RelocsStripped) != 0 ? optional + 70 : null;
    }
}
