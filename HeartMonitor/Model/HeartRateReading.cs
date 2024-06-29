using System.IO;

namespace HeartMonitor.Model;

// https://github.com/jlennox/HeartRate/blob/master/src/HeartRate/HeartRateService.cs
public struct HeartRateReading
{
    public HeartRateFlags Flags { get; set; }
    public ContactSensorStatus Status { get; set; }
    public int BeatsPerMinute { get; set; }
    public int? EnergyExpended { get; set; }
    public int[] RRIntervals { get; set; }
    public bool IsError { get; set; }
    public string Error { get; set; }

    public override string ToString()
    {
        return
            "BPM: " + BeatsPerMinute + Environment.NewLine +
            "Energy: " + EnergyExpended + Environment.NewLine +
            "Status: " + Status + Environment.NewLine +
            "Flags: " + Flags + Environment.NewLine
            ;
    }

    public static HeartRateReading? ReadBuffer(byte[]? buffer)
    {
        if (buffer is not { Length: > 0 }) return null;

        using var ms = new MemoryStream(buffer);
        var flags = (HeartRateFlags)ms.ReadByte();
        var isshort = flags.HasFlag(HeartRateFlags.IsShort);
        var contactSensor = (ContactSensorStatus)(((int)flags >> 1) & 3);
        var hasEnergyExpended = flags.HasFlag(HeartRateFlags.HasEnergyExpended);
        var hasRRInterval = flags.HasFlag(HeartRateFlags.HasRRInterval);
        var minLength = isshort ? 3 : 2;

        if (buffer.Length < minLength) return null;

        var reading = new HeartRateReading
        {
            Flags = flags,
            Status = contactSensor,
            BeatsPerMinute = isshort ? ms.ReadUInt16() : ms.ReadByte()
        };

        if (hasEnergyExpended)
        {
            reading.EnergyExpended = ms.ReadUInt16();
        }

        if (hasRRInterval)
        {
            var rrvalueCount = (buffer.Length - ms.Position) / sizeof(ushort);
            var rrvalues = new int[rrvalueCount];
            for (var i = 0; i < rrvalueCount; ++i)
            {
                rrvalues[i] = ms.ReadUInt16();
            }

            reading.RRIntervals = rrvalues;
        }

        return reading;
    }
}

public enum ContactSensorStatus
{
    NotSupported,
    NotSupported2,
    NoContact,
    Contact
}

[Flags]
public enum HeartRateFlags
{
    None = 0,
    IsShort = 1,
    HasEnergyExpended = 1 << 3,
    HasRRInterval = 1 << 4,
}
