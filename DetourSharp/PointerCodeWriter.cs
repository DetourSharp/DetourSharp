using Iced.Intel;
namespace DetourSharp;

sealed unsafe class PointerCodeWriter : CodeWriter
{
    public void* Address { get; }

    public nuint Offset { get; set; }

    public PointerCodeWriter(void* address)
    {
        Address = address;
    }

    public override void WriteByte(byte value)
    {
        ((byte*)Address)[Offset++] = value;
    }
}
