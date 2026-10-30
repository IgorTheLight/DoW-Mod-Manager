using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;

namespace DoW_Mod_Manager.Platform.Windows;

/// <summary>
/// Windows file-system helpers. Ports:
/// <list type="bullet">
///   <item><c>OpenFolder</c> — <c>explorer.exe</c> (legacy <c>OpenFolderButton_Click</c>).</item>
///   <item><c>ShowFileProperties</c> — shell32 <c>ShellExecuteEx</c> "properties" verb
///         (legacy <c>WinApiCalls.ShowFileProperties</c>).</item>
///   <item><c>CreateShortcut</c> — a Desktop <c>.lnk</c> via the ShellLink COM object
///         (legacy <c>DownloadHelper.CreateShortcut</c> which used IWshRuntimeLibrary).</item>
/// </list>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsFileSystemService : IFileSystemService
{
    public void OpenFolder(string path)
    {
        // Mirrors the legacy Process.Start("explorer.exe", path) behaviour.
        Process.Start("explorer.exe", path);
    }

    public void ShowFileProperties(string path)
    {
        ShowFilePropertiesNative(path);
    }

    public void CreateShortcut(ShortcutSpec spec)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string shortcutLocation = Path.Combine(desktop, spec.Name + ".lnk");

        // The ShellLink COM object behind WScript.Shell's CreateShortcut, called through
        // source-generated COM interfaces: late-bound COM needs built-in COM interop, which the
        // project disables and trimming removes.
        Guid clsid = ClsidShellLink;
        Guid iid = IidShellLinkW;
        Marshal.ThrowExceptionForHR(CoCreateInstance(clsid, IntPtr.Zero, ClsctxInprocServer, iid, out IntPtr ptr));
        IShellLinkW link;
        try
        {
            link = (IShellLinkW)ShellComWrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.None);
        }
        finally
        {
            Marshal.Release(ptr);
        }

        link.SetPath(spec.TargetPath);

        if (!string.IsNullOrEmpty(spec.Arguments))
            link.SetArguments(spec.Arguments!);

        string workingDir = spec.WorkingDirectory
            ?? Path.GetDirectoryName(spec.TargetPath)
            ?? string.Empty;
        if (!string.IsNullOrEmpty(workingDir))
            link.SetWorkingDirectory(workingDir);

        if (!string.IsNullOrEmpty(spec.Description))
            link.SetDescription(spec.Description!);

        if (!string.IsNullOrEmpty(spec.IconPath))
        {
            // WScript.Shell's IconLocation format: "path" or "path,index".
            string iconPath = spec.IconPath!;
            int iconIndex = 0;
            int comma = iconPath.LastIndexOf(',');
            if (comma > 0 && int.TryParse(iconPath.AsSpan(comma + 1).Trim(), out int index))
            {
                iconIndex = index;
                iconPath = iconPath[..comma].TrimEnd();
            }
            link.SetIconLocation(iconPath, iconIndex);
        }

        ((IPersistFile)link).Save(shortcutLocation, true);
    }

    // --- ShellLink COM object (shobjidl_core.h / objidl.h) ---

    static readonly Guid ClsidShellLink = new("00021401-0000-0000-C000-000000000046");
    static readonly Guid IidShellLinkW = new("000214F9-0000-0000-C000-000000000046");
    const uint ClsctxInprocServer = 1;

    static readonly StrategyBasedComWrappers ShellComWrappers = new();

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, in Guid riid, out IntPtr ppv);

    /// <summary>
    /// IShellLinkW in vtable order. Only the setters are called; the other methods hold their
    /// slots and use raw pointers.
    /// </summary>
    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal partial interface IShellLinkW
    {
        void GetPath(IntPtr pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription(IntPtr pszName, int cch);
        void SetDescription(string pszName);
        void GetWorkingDirectory(IntPtr pszDir, int cch);
        void SetWorkingDirectory(string pszDir);
        void GetArguments(IntPtr pszArgs, int cch);
        void SetArguments(string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation(IntPtr pszIconPath, int cch, out int piIcon);
        void SetIconLocation(string pszIconPath, int iIcon);
        void SetRelativePath(string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath(string pszFile);
    }

    /// <summary>IPersistFile (with IPersist's GetClassID first), up to Save.</summary>
    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    internal partial interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load(string pszFileName, uint dwMode);
        void Save(string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
    }

    // --- shell32 ShellExecuteEx "properties" verb (ported from WinApiCalls) ---

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpVerb;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpFile;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpParameters;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    const int SW_SHOW = 5;
    const uint SEE_MASK_INVOKEIDLIST = 12;

    static bool ShowFilePropertiesNative(string filename)
    {
        SHELLEXECUTEINFO info = new SHELLEXECUTEINFO();
        info.cbSize = Marshal.SizeOf(info);
        info.lpVerb = "properties";
        info.lpFile = filename;
        info.nShow = SW_SHOW;
        info.fMask = SEE_MASK_INVOKEIDLIST;
        return ShellExecuteEx(ref info);
    }
}
