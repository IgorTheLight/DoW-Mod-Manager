// Decompiled with JetBrains decompiler
// Type: SSNoFog.FogRemover
// Assembly: SSNoFog, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ABA6371C-50C2-412D-8152-53D605E42626
// Assembly location: D:\THQ\Dawn of War - Soulstorm\SSNoFog.exe
// This was decompiled from the "SSNoFog.exe" made by the russian DoW modding community.
// And later this file was changed by IgorTheLight to be more human-friendly
// And then it was further improved by BlueAmulet

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace DoW_DE_Nod_Manager
{
    public static class FogRemover
    {
        // GOG - 0x1059580
        static readonly byte[] getFog            = new byte[6] { 0xD9, 0x81, 0x60, 0x0C, 0x00, 0x00 };
        static readonly byte[] getZero           = new byte[6] { 0xD9, 0xEE, 0x0F, 0x1F, 0x40, 0x00 };

        static readonly byte[] float96           = new byte[4] { 0x00, 0x00, 0xC0, 0x42 };
        static readonly byte[] float512          = new byte[4] { 0x00, 0x00, 0x00, 0x44 };

        static readonly byte[] setMapSkyDistance = new byte[6] { 0xD9, 0x9B, 0x70, 0x0C, 0x00, 0x00 };
        static readonly byte[] setNothing        = new byte[6] { 0xDD, 0xD8, 0x0F, 0x1F, 0x40, 0x00 };

        const int PAGE_EXECUTE_READWRITE = 0x40;

        const int fogAddress12               = 0x745570;
        const int float512Address12          = 0x863B18;
        const int mapSkyDistanceAddress12    = 0x7470CA;

        const int fogAddressSteam            = 0x8282F0;
        const int float512AddressSteam       = 0xAF54C8;
        const int mapSkyDistanceAddressSteam = 0x82A33A;

        const int fogAddressGOG              = 0x0;         //TODO: Coudn't find this address!
        const int float512AddressGOG         = 0xDA76ED;
        const int mapSkyDistanceAddressGOG   = 0x0;         //TODO: Coudn't find this address!

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        static extern bool ReadProcessMemory(IntPtr hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        static extern bool VirtualProtectEx(IntPtr hProcess, int lpAddress, int dwSize, int flNewProtect, out int lpflOldProtect);

        public static void DisableFog(Process process)
        {
            ActuallyDisableFog(process, fogAddress12, float512Address12, mapSkyDistanceAddress12);
            ActuallyDisableFog(process, fogAddressSteam, float512AddressSteam, mapSkyDistanceAddressSteam);
            //ActuallyDisableFog(process, fogAddressGOG, float512AddressGOG, mapSkyDistanceAddressGOG);     // Uncomment this when all addresses would be found!
        }

        static void ActuallyDisableFog(Process process, int fogAddress, int float512Address, int mapSkyDistanceAddress)
        {
            IntPtr pHandle = OpenProcess(56, false, process.Id);
            try
            {
                CheckToggleMemory(fogAddress, getFog, getZero, pHandle);
                CheckToggleMemory(float512Address, float96, float512, pHandle);
                CheckToggleMemory(mapSkyDistanceAddress, setMapSkyDistance, setNothing, pHandle);
            }
            finally
            {
                CloseHandle(pHandle);
            }
        }

        static bool CheckToggleMemory(int addr, byte[] checkVal, byte[] setVal, IntPtr pHandle)
        {
            byte[] lpBuffer = new byte[checkVal.Length];

            if (!ReadProcessMemory(pHandle, addr, lpBuffer, lpBuffer.Length, out int lpNumberOfBytesRead)
                || lpNumberOfBytesRead != lpBuffer.Length
                || !lpBuffer.SequenceEqual(checkVal))
                return false;

            VirtualProtectEx(pHandle, addr, setVal.Length, PAGE_EXECUTE_READWRITE, out int lpflOldProtect);
            int returnCode = WriteProcessMemory(pHandle, addr, setVal, setVal.Length, out _) ? 1 : 0;
            
            VirtualProtectEx(pHandle, addr, setVal.Length, lpflOldProtect, out _);
            return returnCode != 0;
        }
    }
}