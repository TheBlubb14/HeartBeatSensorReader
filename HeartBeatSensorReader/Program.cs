using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Collections.Generic;

namespace HeartBeatSensorReader
{
    public record struct BpmData(DateTime Occured, int Heartrate, long NanoSecondsSinceEpoch);

    public class Program
    {
        private readonly HttpClient client = new HttpClient();
        private static BPMMeasurement lastMeasurement;
        private static HttpListener server;

        private static List<BpmData> datas = new();
        public static bool UseGoogleFit = true;
        private static sbyte batteryPercent = -1;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Use google fit support? (Y/n)");

            ConsoleKeyInfo a;
            while ((a = Console.ReadKey()).Key is not ConsoleKey.Y and not ConsoleKey.N)
            {
                Console.WriteLine("Use google fit support? (Y/n)");
            }
            UseGoogleFit = a.Key == ConsoleKey.Y;

            if (UseGoogleFit)
            {

                using var service = await GoogleFit.GetFitnessService();
                Console.WriteLine("Connected to google fit services");
                await GoogleFit.UploadFromFiles(@"F:\Temp", service);

                Console.WriteLine("Uploaded old files");
            }


            var p = new Program();

            bool isBeatsaber = true;

            if (UseGoogleFit)
            {
                Console.WriteLine("For aeorobic press a, for beatsaber any other key");
                var key = Console.ReadKey();
                isBeatsaber = key.Key != ConsoleKey.A;
            }

            try
            {

                p.SubscribeBLE();

                server = new HttpListener();
                server.Prefixes.Add("http://localhost:32667/");

                server.Start();

                _ = Task.Run(
                async () =>
                {
                    while (true)
                    {
                        var ctx = await server.GetContextAsync();
                        var response = ctx.Response;
                        using (var sw = new StreamWriter(response.OutputStream, leaveOpen: true))
                        {
                            sw.Write(System.Text.Json.JsonSerializer.Serialize(lastMeasurement));

                        }
                        response.OutputStream.Close();

                    }

                });

                Console.ReadLine();

            }
            finally
            {
                if (datas.Any(x => x.Heartrate > 90))
                {
                    if (UseGoogleFit)
                        await GoogleFit.UploadData(datas, activityType: isBeatsaber ? 108 : 9);
                    File.Move($@"F:\Temp\BPMoverTimeNew_{DateTime.UtcNow.ToShortDateString()}.txt", $@"F:\Temp\BPMDone\BPMoverTimeNew_{DateTime.UtcNow.ToShortDateString()}.txt");

                }
            }


        }


        private async void SubscribeBLE()
        {
            watcher = new BluetoothLEAdvertisementWatcher();
            string todayBPMLog = DateTime.Now.ToShortDateString();
            semaphore = new SemaphoreSlim(1, 1);
            watcher.ScanningMode = BluetoothLEScanningMode.Active;
            watcher.Received += Watcher_Received;
            watcher.Start();
        }

        BluetoothLEDevice BluetoothLEDevice;
        SemaphoreSlim semaphore;
        BluetoothLEAdvertisementWatcher watcher;

        private async void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            string localName = args.Advertisement.LocalName.ToLower();

            if (localName != "heart rate sensor")
                return;
            await semaphore.WaitAsync();
            try
            {
                if ((BluetoothLEDevice == null || BluetoothLEDevice.ConnectionStatus == BluetoothConnectionStatus.Disconnected))
                {
                    BluetoothLEDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
                    var services = await BluetoothLEDevice.GetGattServicesAsync();
                    foreach (GattDeviceService s in BluetoothLEDevice.GattServices)
                    {

                        var car = await s.GetCharacteristicsAsync();

                        foreach (var c in car.Characteristics)
                        {
                            bool cont = false;

                            if (s.Uuid.ToString().StartsWith("0000180d") && c.Uuid.ToString().StartsWith("00002a37"))
                            {
                                c.ValueChanged += HeartBeatValueChanged;
                                cont = true;

                            }
                            if (s.Uuid.ToString().StartsWith("0000") && c.Uuid.ToString().StartsWith("00002a19"))
                            {
                                c.ValueChanged += BatteryValueChanged;
                                cont = true;
                            }

                            if (!cont)
                                continue;

                            var value_result = await c.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

                            if (value_result != GattCommunicationStatus.Success)
                            {
                                c.ValueChanged -= HeartBeatValueChanged;
                                Console.WriteLine($"ERROR: Gatt Subscription hat failed: {value_result}");
                                Console.WriteLine($"{c.Uuid}");
                            }
                            else
                            {
                                Console.WriteLine("Subscription was successfull");
                                Console.WriteLine($"{c.Uuid}");
                            }

                        }
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }

            return;
        }

        bool b = false;


        private void BatteryValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var data = new byte[args.CharacteristicValue.Length];
            var now = DateTime.UtcNow;
            using (var reader = DataReader.FromBuffer(args.CharacteristicValue))
            {
                reader.ReadBytes(data);
            }
            batteryPercent = (sbyte)data[0];
        }

        private void HeartBeatValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var data = new byte[args.CharacteristicValue.Length];
            var now = DateTime.UtcNow;
            using (var reader = DataReader.FromBuffer(args.CharacteristicValue))
            {
                reader.ReadBytes(data);
            }

            if (data.Length > 1)
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


                lastMeasurement = new(data[1], now.ToString("s") + "Z");
                datas.Add(new BpmData(now, data[1], GoogleFit.GetNanoSecondsSinceEpochFrom(now)));
                File.WriteAllText(@"F:\Temp\BPM.txt", "HB: " + data[1].ToString() + "bpm");
                File.WriteAllText(@"F:\Temp\BPM.json", System.Text.Json.JsonSerializer.Serialize(new { bpm = data[1], measured_at = now.ToString("s") + "Z" }));
                var stringToAppend = $"{now:dd.MM.yyyy HH:mm:ss.ffff}{{0}}: {data[1]}";
                foreach (var item in rrValues)
                {
                    if (item > 0)
                    {
                        stringToAppend += " | " + item;
                    }
                    else
                        break;
                }

                File.AppendAllText($@"F:\Temp\BPMoverTimeNew_{now.ToShortDateString()}.txt", $"{stringToAppend}\r\n");

                Console.WriteLine(string.Format(stringToAppend, batteryPercent == -1 ? "" : $" ({batteryPercent})"));


            }
        }


        private readonly record struct BPMMeasurement
        {

            [System.Text.Json.Serialization.JsonPropertyName("bpm")]
            public int Bpm { get; init; }
            [System.Text.Json.Serialization.JsonPropertyName("measured_at")]
            public string MeasuredAt { get; init; }
            public BPMMeasurement(int bpm, string measuredAt)
            {
                Bpm = bpm;
                MeasuredAt = measuredAt;
            }
        }
    }
}
