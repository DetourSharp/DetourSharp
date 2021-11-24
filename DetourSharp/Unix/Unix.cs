using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
namespace DetourSharp;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
static unsafe class Unix
{
    public const int EACCES = 13;

    public const int EINVAL = 22;

    public const int PROT_READ = 1 << 0;

    public const int PROT_WRITE = 1 << 1;

    public const int PROT_EXEC = 1 << 2;

    public const int MAP_SHARED = 1 << 0;

    public const int MAP_PRIVATE = 1 << 1;

    public const int MAP_ANONYMOUS = 1 << 5;

    [DllImport("c", ExactSpelling = true)]
    public static extern void* mmap(void* addr, nuint length, int prot, int flags, int fd, uint offset);

    [DllImport("c", ExactSpelling = true)]
    public static extern int munmap(void* addr, nuint length);

    [DllImport("c", ExactSpelling = true)]
    public static extern int mprotect(void* addr, nuint len, int prot);

    public static void ThrowExceptionForErrno(int errno)
    {
        if (errno != 0)
        {
            throw new Win32Exception(errno);
        }
    }

    public static void __builtin___clear_cache(void* begin, void* end)
    {
        // TODO: Use a native library to expose this
        _ = begin;
        _ = end;
    }
}
