using System;
using System.IO;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using Iced.Intel;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.PAGE;
using static TerraFX.Interop.Windows.MEM;
using static DetourSharp.Unix;
using static DetourSharp.UnixExtensions;
namespace DetourSharp;

/// <summary>Provides methods for detouring code addresses.</summary>
public sealed unsafe class Detour : IDisposable
{
    static readonly object s_DetourLock = new();

    bool disposed;

    DetourState state;

    Allocation trampoline;

    Allocation attachBytes;

    Allocation detachBytes;

    readonly void* targetAddress;

    readonly void* detourAddress;

    /// <summary>Gets a value that indicates whether <see cref="Detour"/> can be used with the current operating system.</summary>
    [SupportedOSPlatformGuard("windows")]
    [SupportedOSPlatformGuard("linux")]
    [SupportedOSPlatformGuard("macos")]
    public static bool IsSupported
    {
        get
        {
            return OperatingSystem.IsWindows()
                || OperatingSystem.IsLinux()
                || OperatingSystem.IsMacOS();
        }
    }

    /// <summary>Gets a value that indicates the state of the detour.</summary>
    public DetourState State => state;

    /// <summary>Gets the address of the code that will be detoured.</summary>
    public void* TargetAddress
    {
        get
        {
            ThrowIfDisposed();
            return targetAddress;
        }
    }

    /// <summary>Gets the address that the <see cref="TargetAddress"/> will be detoured to.</summary>
    public void* DetourAddress
    {
        get
        {
            ThrowIfDisposed();
            return detourAddress;
        }
    }

    /// <summary>Gets the address of code that will behave equivalently to <see cref="TargetAddress"/> when it is not detoured.</summary>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("windows")]
    public void* TrampolineAddress
    {
        get
        {
            ThrowIfDisposed();
            return trampoline.Address;
        }
    }

    /// <summary>Initializes a new <see cref="Detour"/> instance with the given target and detour code addresses.</summary>
    /// <param name="target">The address of the code that will be detoured.</param>
    /// <param name="detour">The address that <paramref name="target"/> will be detoured to.</param>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("windows")]
    public Detour(void* target, void* detour)
    {
        targetAddress = target;
        detourAddress = detour;

        // Generate some assembly to jump from our target to our detour
        var asm = new Assembler(Environment.Is64BitProcess ? 64 : 32);
        asm.jmp((nuint)detour);

        // The detour stores a copy of the code needed for attached and detached states.
        attachBytes.Length  = GetCodeSize(asm, (nuint)target);
        attachBytes.Address = NativeMemory.Alloc(attachBytes.Length);
        asm.Assemble(new PointerCodeWriter(attachBytes.Address), (nuint)target);

        // Allocate the trampoline, which will also allocate a copy of the original code bytes.
        trampoline = AllocTrampoline(target, (uint)attachBytes.Length, out detachBytes);
        state      = DetourState.Detached;
    }

    /// <summary>Modifies the code located at <see cref="TargetAddress"/> to redirect callers to <see cref="DetourAddress"/>.</summary>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("windows")]
    public void Attach()
    {
        lock (s_DetourLock)
        {
            ThrowIfDisposed();

            if (state == DetourState.Attached)
                return;

            Patch(targetAddress, attachBytes);
            state = DetourState.Attached;
        }
    }

    /// <summary>Restores the original code located at <see cref="TargetAddress"/> when the detour was created.</summary>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("windows")]
    public void Detach()
    {
        lock (s_DetourLock)
        {
            ThrowIfDisposed();

            if (state == DetourState.Detached)
                return;

            Patch(targetAddress, detachBytes);
            state = DetourState.Detached;
        }
    }

    /// <summary>Restores the original code located at <see cref="TargetAddress"/> if it has been detoured, and releases any unmanaged memory owned by the detour.</summary>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("windows")]
    public void Dispose()
    {
        if (disposed)
            return;

        lock (s_DetourLock)
        {
            if (disposed)
                return;

            if (state == DetourState.Attached)
            {
                Patch(targetAddress, detachBytes);
                state = DetourState.Detached;
            }

            NativeMemory.Free(attachBytes.Address);
            NativeMemory.Free(detachBytes.Address);
            attachBytes = default;
            detachBytes = default;

            if (OperatingSystem.IsWindows())
                _ = VirtualFree(trampoline.Address, 0, MEM_RELEASE);
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                _ = munmap(trampoline.Address, trampoline.Length);

            trampoline = default;
            disposed   = true;
        }
    }

    void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(Detour));
        }
    }

    static nuint GetCodeSize(Assembler asm, ulong rip)
    {
        var writer = new NullCodeWriter();
        asm.Assemble(writer, rip);
        return writer.BytesWritten;
    }

    static Allocation AllocTrampoline(void* target, uint bytesNeeded, out Allocation oldBytes)
    {
        var asm     = new Assembler(Environment.Is64BitProcess ? 64 : 32);
        var decoder = Decoder.Create(asm.Bitness, new PointerCodeReader(target), (nuint)target);

        while (decoder.IP < (nuint)target + bytesNeeded)
            asm.AddInstruction(decoder.Decode());

        oldBytes.Length  = (nuint)decoder.IP - (nuint)target;
        oldBytes.Address = NativeMemory.Alloc(oldBytes.Length);

        var source = new Span<byte>(target, (int)oldBytes.Length);
        var dest   = new Span<byte>(oldBytes.Address, source.Length);
        source.CopyTo(dest);

        asm.jmp(decoder.IP);
        return AllocMethod(asm, 0);
    }

    static Allocation AllocMethod(Assembler asm, ulong rip)
    {
        void* addr;
        uint protect;
        using var ms = new MemoryStream();
        asm.Assemble(new StreamCodeWriter(ms), rip);

        var length = (uint)ms.Length;
        var buffer = new ReadOnlySpan<byte>(ms.GetBuffer(), 0, (int)length);

        if (OperatingSystem.IsWindows())
        {
            addr = VirtualAlloc(null, length, MEM_COMMIT, PAGE_READWRITE);

            if (addr is null)
            {
                Marshal.ThrowExceptionForHR(HRESULT_FROM_WIN32(Marshal.GetLastSystemError()));
            }
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            addr = mmap(null, length, PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);

            if (addr is null)
            {
                ThrowExceptionForErrno(Marshal.GetLastSystemError());
            }
        }
        else
        {
            throw new PlatformNotSupportedException();
        }

        buffer.CopyTo(new Span<byte>(addr, buffer.Length));

        if (OperatingSystem.IsWindows())
        {
            _ = VirtualProtect(addr, length, PAGE_EXECUTE_READ, &protect);
            _ = FlushInstructionCache(GetCurrentProcess(), addr, length);
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            _ = mprotect(addr, length, PROT_READ | PROT_EXEC);
            __builtin___clear_cache(addr, (byte*)addr + length);
        }

        return new Allocation { Address = addr, Length = length };
    }

    static void Patch(void* target, Allocation patch)
    {
        uint protect;

        if (OperatingSystem.IsWindows())
        {
            if (!VirtualProtect(target, patch.Length, PAGE_EXECUTE_READWRITE, &protect))
            {
                Marshal.ThrowExceptionForHR(HRESULT_FROM_WIN32(Marshal.GetLastSystemError()));
            }
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            if (mprotect2(target, patch.Length, PROT_READ | PROT_WRITE | PROT_EXEC, (int*)&protect) != 0)
            {
                ThrowExceptionForErrno(Marshal.GetLastSystemError());
            }
        }
        else
        {
            throw new PlatformNotSupportedException();
        }

        Buffer.MemoryCopy(
            patch.Address,
            target,
            (nint)patch.Length,
            (nint)patch.Length
        );

        if (OperatingSystem.IsWindows())
        {
            _ = VirtualProtect(target, patch.Length, protect, &protect);
            _ = FlushInstructionCache(GetCurrentProcess(), target, patch.Length);
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            _ = mprotect(target, patch.Length, (int)protect);
            __builtin___clear_cache(target, (byte*)target + patch.Length);
        }
    }

    struct Allocation
    {
        public void* Address;
        public nuint Length;
    }
}
