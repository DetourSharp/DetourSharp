using Iced.Intel;
namespace DetourSharp;

sealed class NullCodeWriter : CodeWriter
{
    public nuint BytesWritten { get; private set; }

    public override void WriteByte(byte value)
    {
        BytesWritten++;
    }
}
