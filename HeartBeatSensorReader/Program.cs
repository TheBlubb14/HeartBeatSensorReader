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

namespace HeartBeatSensorReader
{
    public class Program
    {
        private readonly HttpClient client = new HttpClient();
        static void Main(string[] args)
        {
            var p = new Program();
            p.SubscribeBLE();
            Console.ReadLine();
        }

        private async void SubscribeBLE()
        {
            watcher = new BluetoothLEAdvertisementWatcher();
            string todayBPMLog = DateTime.Now.ToShortDateString();

            watcher.ScanningMode = BluetoothLEScanningMode.Active;
            watcher.Received += Watcher_Received;
            watcher.Start();
        }

        BluetoothLEDevice BluetoothLEDevice;
        GattCharacteristic Characteristic;
        bool lockO = false;
        BluetoothLEAdvertisementWatcher watcher;

        private async void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            string localName = args.Advertisement.LocalName;

            if (localName != "heart rate sensor")
                return;

            if (!lockO && (BluetoothLEDevice == null || BluetoothLEDevice.ConnectionStatus == BluetoothConnectionStatus.Disconnected))
            {
                lockO = true;
                BluetoothLEDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
                GattDeviceServicesResult service = await BluetoothLEDevice.GetGattServicesAsync();

                foreach (GattDeviceService s in service.Services.Where(x => x.Uuid.ToString().StartsWith("0000180d")))
                {
                    var car = await s.GetCharacteristicsAsync();

                    foreach (var c in car.Characteristics.Where(x => x.Uuid.ToString().StartsWith("00002a37")))
                    {
                        Characteristic = c;
                        Characteristic.ValueChanged += C_ValueChanged;

                        var value_result = await c.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

                        if (value_result != GattCommunicationStatus.Success)
                        {
                            Characteristic.ValueChanged -= C_ValueChanged;
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
                lockO = false;
            }

            return;
        }

        private void C_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var data = new byte[args.CharacteristicValue.Length];
            using (var reader = DataReader.FromBuffer(args.CharacteristicValue))
            {
                reader.ReadBytes(data);
            }

            if (data.Length > 1)
            {
                Task.Run(() =>
                {
                    try
                    {
                        WebRequest request = WebRequest.Create("http://localhost:8880");
                        request.Method = "POST";
                        byte[] byteArray = Encoding.UTF8.GetBytes("HB: " + data[1].ToString() + "bpm");
                        request.ContentType = "application/x-www-form-urlencoded";
                        request.ContentLength = byteArray.Length;
                        using (Stream dataStream = request.GetRequestStream())
                            dataStream.Write(byteArray, 0, byteArray.Length);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                });

                File.WriteAllText(@"F:\Temp\BPM.txt", "HB: " + data[1].ToString() + "bpm");
                File.AppendAllText($@"F:\Temp\BPMoverTime_{DateTime.Now.ToShortDateString()}.txt", $"{DateTime.Now}: {data[1]}\r\n");
                Console.WriteLine($"HeartBeat {DateTime.Now.ToLongTimeString()}: {data[1]}");
            }
        }
    }
}
