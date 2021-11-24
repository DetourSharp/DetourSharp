using System;
using System.IO;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using static DetourSharp.Unix;
namespace DetourSharp;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
static class UnixExtensions
{
    // Alternative version of mprotect that provides the previous protection value.
    // This is intended to mimic the functionality of the Windows VirtualProtect API.
    public static unsafe int mprotect2(void* addr, nuint len, int prot, int* prev)
    {
        if (GetMemoryProtection(addr, prev) != 0)
            return -1;

        return mprotect(addr, len, prot);
    }

    // Parses /proc/self/maps for the memory protection flags of the given address.
    static unsafe int GetMemoryProtection(void* addr, int* prot)
    {
        ReadOnlySpan<byte> maps;

        if (prot is null)
            goto InvalidValue;

        try
        {
            *prot = 0;
            maps  = File.ReadAllBytes("/proc/self/maps");
        }
        catch (Exception)
        {
            Marshal.SetLastSystemError(EACCES);
            return -1;
        }

        foreach (ReadOnlySpan<byte> line in maps.Tokenize((byte)'\n'))
        {
            if (!MapsFileEntry.TryParse(line, out MapsFileEntry entry))
                continue;

            if ((nuint)addr < entry.Start || (nuint)addr > entry.End)
                continue;

            *prot = entry.Protect;
            return 0;
        }

    InvalidValue:
        Marshal.SetLastSystemError(EINVAL);
        return -1;
    }
}
