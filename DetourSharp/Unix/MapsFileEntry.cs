using System;
using System.Buffers.Text;
using System.Globalization;
using System.Runtime.Versioning;
using static DetourSharp.Unix;
namespace DetourSharp;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
readonly struct MapsFileEntry
{
    public nuint Start { get; }

    public nuint End { get; }

    public int Protect { get; }

    public int Flags { get; }

    public MapsFileEntry(nuint start, nuint end, int protect, int flags)
    {
        Start   = start;
        End     = end;
        Protect = protect;
        Flags   = flags;
    }

    public static bool TryParse(ReadOnlySpan<byte> source, out MapsFileEntry entry)
    {
        var flags   = 0;
        var protect = 0;

        if (!Utf8Parser.TryParse(source, out ulong start, out int consumed, 'X'))
            goto Failure;

        consumed++;
        source = source[consumed..];

        if (!Utf8Parser.TryParse(source, out ulong end, out consumed, 'X'))
            goto Failure;

        consumed++;
        source = source[consumed..];

        if (source.Length < 4)
            goto Failure;

        for (int i = 0; i < 4; i++)
        {
            switch (source[i])
            {
            case (byte)'r':
                protect |= PROT_READ;
                break;
            case (byte)'w':
                protect |= PROT_WRITE;
                break;
            case (byte)'x':
                protect |= PROT_EXEC;
                break;
            case (byte)'s':
                flags |= MAP_SHARED;
                break;
            case (byte)'p':
                flags |= MAP_PRIVATE;
                break;
            }
        }

        entry = new MapsFileEntry((nuint)start, (nuint)end, protect, flags);
        return true;

    Failure:
        entry = default;
        return false;
    }

    public static bool TryParse(ReadOnlySpan<char> source, out MapsFileEntry entry)
    {
        var flags   = 0;
        var protect = 0;
        var index   = source.IndexOf('-');

        if (index == -1)
            goto Failure;

        if (!ulong.TryParse(source[..index], NumberStyles.HexNumber, null, out ulong start))
            goto Failure;

        index++;
        source = source[index..];
        index  = source.IndexOf(' ');

        if (index == -1)
            goto Failure;

        if (!ulong.TryParse(source[..index], NumberStyles.HexNumber, null, out ulong end))
            goto Failure;

        index++;
        source = source[index..];

        if (source.Length < 4)
            goto Failure;

        for (int i = 0; i < 4; i++)
        {
            switch (source[i])
            {
            case 'r':
                protect |= PROT_READ;
                break;
            case 'w':
                protect |= PROT_WRITE;
                break;
            case 'x':
                protect |= PROT_EXEC;
                break;
            case 's':
                flags |= MAP_SHARED;
                break;
            case 'p':
                flags |= MAP_PRIVATE;
                break;
            }
        }

        entry = new MapsFileEntry((nuint)start, (nuint)end, protect, flags);
        return true;

    Failure:
        entry = default;
        return false;
    }
}
