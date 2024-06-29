using InTheHand.Bluetooth;
using YamlDotNet.Serialization;
using System.Text;

namespace Blubb;

class Program
{
    static bool show_unknown = false;
    static RingBuffer.RingBuffer<int> avg = new(200);
    static int max = 0;
    static int min = 0;

    static BluetoothLEScan? scan = null;
    static CancellationTokenSource cts = new();
    static ServiceUUID[] service_uuids;
    static ServiceUUID[] characteristic_uuids;
    static List<GattCharacteristic> subscribedCharacteristics = new();

    static async Task Main(string[] args)
    {
        var deserializer = new DeserializerBuilder()
        .Build();

        service_uuids = deserializer.Deserialize<Services>(Data.service_uuids).uuids;
        characteristic_uuids = deserializer.Deserialize<Services>(Data.characteristic_uuids).uuids;

        Console.CancelKeyPress += Console_CancelKeyPress;
        Bluetooth.AdvertisementReceived += Bluetooth_AdvertisementReceived;

        scan = await Bluetooth.RequestLEScanAsync();

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (TaskCanceledException) { }
    }

    private static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;

        scan?.Stop();
        scan = null;
        cts.Cancel();

        subscribedCharacteristics.ForEach(x => x.StopNotificationsAsync());
        subscribedCharacteristics.Clear();
    }

    private static async void Bluetooth_AdvertisementReceived(object? sender, BluetoothAdvertisingEvent e)
    {
        // Dont print after we stopped
        if (scan is not null)
            Console.WriteLine("Advertisement: " + e.Name);

        if (e.Name == "XOSS_X2_626583")
        {
            // device id: C00D77E0C255
            scan?.Stop();
            scan = null;

            await ReadBle(e.Device.Id);
        }
    }

    private static async Task ReadBle(string deviceId)
    {
        Console.Clear();

        var device = await BluetoothDevice.FromIdAsync(deviceId);
        if (device is null)
            return;

        var services = await device.Gatt.GetPrimaryServicesAsync();

        if (services is null)
            return;

        foreach (var service in services)
        {
            var uuid = service.Uuid.ToString();

            var serviceName = service_uuids.FirstOrDefault(x => x.uuid.EndsWith(uuid))?.name;

            if (serviceName is null)
            {
                if (!show_unknown)
                    continue;

                serviceName = uuid;
            }

            Console.WriteLine(serviceName);

            var characteristics = await service.GetCharacteristicsAsync();

            if (characteristics is null)
                continue;

            foreach (var characteristic in characteristics)
            {
                uuid = characteristic.Uuid.ToString();
                var characteristicName = characteristic_uuids.FirstOrDefault(x => x.uuid.EndsWith(uuid))?.name;

                if (characteristicName is null)
                {
                    if (!show_unknown)
                        continue;

                    characteristicName = uuid;
                }

                Console.WriteLine("  - " + characteristicName);

                var val = await characteristic.ReadValueAsync();
                if (val is not null)
                {
                    Console.WriteLine($"    [{string.Join(" ", val)}]");
                    Console.WriteLine($"    {Encoding.ASCII.GetString(val)}");

                    Console.WriteLine();
                }

                if (characteristicName == "Heart Rate Measurement")
                {
                    await characteristic.StartNotificationsAsync();
                    subscribedCharacteristics.Add(characteristic);

                    characteristic.CharacteristicValueChanged += (s, e) =>
                    {
                        Console.WriteLine(characteristicName + ": ");
                        if (e.Value is null)
                        {
                            Console.WriteLine(" Error:");
                            Console.WriteLine(e.Error?.Message);

                            Console.WriteLine();
                        }
                        else if (characteristicName == "Heart Rate Measurement")
                        {
                            //var heart = HeartRateConvert(e.Value);
                            var h = ReadBuffer(e.Value, e.Value.Length).Value;

                            //Console.WriteLine(heart.Rate + " - " + string.Join(" ", heart.MsBetweenTwo.TakeWhile(x => x != 0)));
                            var b = h.BeatsPerMinute;

                            Console.WriteLine(b);
                            avg.Add(b);

                            if (b > max)
                                max = b;

                            if (b < min || min == 0)
                                min = b;

                            Console.Title = $"AVG: {avg.Average():0} MAX: {max} MIN: {min}";
                        }
                        else
                        {
                            Console.WriteLine($"  [{string.Join(" ", e.Value)}]");
                            Console.WriteLine($"  {Encoding.ASCII.GetString(e.Value)}");

                            //if (characteristicName == "Heart Rate Measurement")
                            //{
                            //    var heart = HeartRateConvert(e.Value);

                            //    Console.WriteLine(heart.Rate + " - " + string.Join(" ", heart.MsBetweenTwo.TakeWhile(x => x != 0)));


                            //    //var bits = new BitArray(e.Value);
                            //    //Console.WriteLine($"  Bits: [{string.Join(" ", bits.Cast<bool>().Select(x => x ? 1 : 0))}]");
                            //}

                            Console.WriteLine();
                        }
                    };
                }
            }

            Console.WriteLine();
        }
    }
    record HeartData(byte Rate, ushort[] MsBetweenTwo, ushort EnergyExpended);

    static HeartData HeartRateConvert(byte[] data)
    {
        var flags = data[0];
        Span<ushort> rrValues = stackalloc ushort[10];
        ushort heartRate = 0;
        ushort energyExpended = 0;

        if ((flags & 1) > 0)
            heartRate = (ushort)(data[2] << 8 | data[1]);
        else
            heartRate = data[1];

        int offsetRRValue = 0;
        if ((flags & 0x001) > 0)
        {
            energyExpended = (ushort)(data[4] << 8 | data[3]);
            offsetRRValue = 2;
        }


        if ((flags & 0b10000) > 0)
        {
            var spanIndex = 0;
            for (int i = 2 + offsetRRValue; i < data.Length; i += 2)
            {
                var data1 = (ushort)(data[i + 1] << 8 | data[i]);
                rrValues[spanIndex++] = data1;
            }
        }

        return new HeartData(data[1], rrValues.ToArray(), energyExpended);
    }

    internal enum ContactSensorStatus
    {
        NotSupported,
        NotSupported2,
        NoContact,
        Contact
    }

    [Flags]
    internal enum HeartRateFlags
    {
        None = 0,
        IsShort = 1,
        HasEnergyExpended = 1 << 3,
        HasRRInterval = 1 << 4,
    }

    internal struct HeartRateReading
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
    }

    // https://github.com/jlennox/HeartRate/blob/master/src/HeartRate/HeartRateService.cs
    internal static HeartRateReading? ReadBuffer(byte[] buffer, int length)
    {
        if (length == 0) return null;

        var ms = new MemoryStream(buffer, 0, length);
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

    record Services(ServiceUUID[] uuids)
    {
        public Services() : this([]) { }
    };
    record ServiceUUID(string uuid, string name, string id)
    {
        public ServiceUUID() : this("", "", "") { }
    };
}

public static class Extension
{
    public static ushort ReadUInt16(this Stream stream)
    {
        return (ushort)(stream.ReadByte() | (stream.ReadByte() << 8));
    }
}
