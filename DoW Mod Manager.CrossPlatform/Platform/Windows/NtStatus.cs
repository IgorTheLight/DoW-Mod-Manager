using System.Runtime.Versioning;

namespace DoW_Mod_Manager.Platform.Windows;

/// <summary>
/// A NT status value. Ported verbatim from the legacy <c>NTStatus.cs</c> for use by the
/// timer-resolution P/Invoke in <see cref="WindowsPerformanceService"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal enum NtStatus : uint
{
    // Success
    SuccessOrWait0 = 0x00000000,
    Wait1 = 0x00000001,
    Wait2 = 0x00000002,
    Wait3 = 0x00000003,
    Wait63 = 0x0000003f,
    Abandoned = 0x00000080,
    AbandonedWait1 = 0x00000081,
    AbandonedWait2 = 0x00000082,
    AbandonedWait3 = 0x00000083,
    AbandonedWait63 = 0x000000bf,
    UserApc = 0x000000c0,
    KernelApc = 0x00000100,
    Alerted = 0x00000101,
    Timeout = 0x00000102,
    Pending = 0x00000103,
    Reparse = 0x00000104,
    MoreEntries = 0x00000105,
    NotAllAssigned = 0x00000106,
    SomeNotMapped = 0x00000107,

    // Warning
    Warning = 0x80000000,
    BufferOverflow = 0x80000005,

    // Error
    Error = 0xc0000000,
    Unsuccessful = 0xc0000001,
    NotImplemented = 0xc0000002,
    InvalidInfoClass = 0xc0000003,
    InfoLengthMismatch = 0xc0000004,
    AccessViolation = 0xc0000005,
    InvalidParameter = 0xc000000d,
    AccessDenied = 0xc0000022,

    MaximumNtStatus = 0xffffffff
}
