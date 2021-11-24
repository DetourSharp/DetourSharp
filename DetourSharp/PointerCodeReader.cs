using Iced.Intel;
namespace DetourSharp;

sealed unsafe class PointerCodeReader : CodeReader
{
    public void* Address { get; }

    public nuint Offset { get; set; }

    public PointerCodeReader(void* address)
    {
        Address = address;
    }

    public override int ReadByte()
    {
        return ((byte*)Address)[Offset++];
    }
}
