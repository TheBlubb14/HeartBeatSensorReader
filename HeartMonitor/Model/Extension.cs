using System.IO;

namespace HeartMonitor.Model;

public static class Extension
{
    public static ushort ReadUInt16(this Stream stream)
    {
        return (ushort)(stream.ReadByte() | (stream.ReadByte() << 8));
    }
}
