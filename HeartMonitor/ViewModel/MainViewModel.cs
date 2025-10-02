using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeartMonitor.Model;
using InTheHand.Bluetooth;
using System.Windows;
using System.Windows.Input;

namespace HeartMonitor.ViewModel
{
    public sealed partial class MainViewModel : ObservableRecipient
    {
        [ObservableProperty, NotifyPropertyChangedFor(nameof(AverageBeatsPerMinute), nameof(AverageSkippedHeartBeats), nameof(AverageHFR))]
        private HeartRateReading reading;

        public int AverageBeatsPerMinute => (int)heartBeatsAverage.GetAverage();

        public int AverageSkippedHeartBeats => (int)skippedHeartBeatsAverage.GetAverage();

        // Gets set manually, to calculate HFR
        [ObservableProperty]
        private int averageRR;

        public int AverageHFR => (int)hfrAverage.GetAverage();

        [ObservableProperty]
        private double fontSize = 15;

        [ObservableProperty]
        private bool showAverageBeatsPerMinute;

        [ObservableProperty]
        private bool showAverageSkippedHeartBeats;

        [ObservableProperty]
        private bool showAverageRR;

        [ObservableProperty]
        private bool showAverageHFR;

        [ObservableProperty]
        private GridLength showLabels;

        private BluetoothLEScan? scan = null;
        private readonly List<GattCharacteristic> subscribedCharacteristics = [];
        private readonly Average skippedHeartBeatsAverage = new(TimeSpan.FromHours(1));
        private readonly Average heartBeatsAverage = new(TimeSpan.FromMinutes(1));
        private readonly Average rrAverage = new(TimeSpan.FromMinutes(1));
        private readonly Average hfrAverage = new(TimeSpan.FromMinutes(5));

        public MainViewModel()
        {
            Bluetooth.AdvertisementReceived += Bluetooth_AdvertisementReceived;
        }
        private async void Bluetooth_AdvertisementReceived(object? sender, BluetoothAdvertisingEvent e)
        {
            if (e.Name == "XOSS_X2_626583")
            {
                // device id: C00D77E0C255
                scan?.Stop();
                scan = null;

                await ReadBle(e.Device.Id);
            }
        }

        private async Task ReadBle(string deviceId)
        {
            var device = await BluetoothDevice.FromIdAsync(deviceId);
            if (device is null)
                return;

            // Heart Rate
            var service = await device.Gatt.GetPrimaryServiceAsync(0x180D);

            if (service is null)
                return;

            // Heart Rate Measurement
            var characteristic = await service.GetCharacteristicAsync(0x2A37);

            if (characteristic is null)
                return;
            service.Device.GattServerDisconnected += async (s, e) =>
            {
                Reading = new HeartRateReading() { BeatsPerMinute = 999999 };
                await Task.Delay(TimeSpan.FromSeconds(5));
                await ReadBle(deviceId).ConfigureAwait(false);
            };
            await characteristic.StartNotificationsAsync();
            subscribedCharacteristics.Add(characteristic);

            characteristic.CharacteristicValueChanged += Characteristic_CharacteristicValueChanged;
        }

        private void Characteristic_CharacteristicValueChanged(object? sender, GattCharacteristicValueChangedEventArgs e)
        {
            var newReading = HeartRateReading.ReadBuffer(e.Value);

            if (newReading is null)
                return;

            var value = newReading.Value;

            skippedHeartBeatsAverage.Add(value.SkippedHeartBeats);
            heartBeatsAverage.Add(value.BeatsPerMinute);

            if (value.RRIntervals is not null)
            {
                var oldRRAvg = AverageRR;
                rrAverage.Add((int)value.RRIntervals.Average());
                AverageRR = (int)rrAverage.GetAverage();
                hfrAverage.Add(Math.Abs(oldRRAvg - AverageRR));

                foreach (var rrInterval in value.RRIntervals)
                {
                    if (rrInterval > 0)
                    {
                        if (rrInterval * value.BeatsPerMinute > 90_000)
                        {
                            Console.WriteLine($"Skipped Heartbeat BPM: {value.BeatsPerMinute} RR: {rrInterval}");
                            Console.WriteLine($"AVG Values");
                            Console.WriteLine("BPM: " + AverageBeatsPerMinute);
                            Console.WriteLine("RR: " + AverageRR);
                            Console.WriteLine("HFR: " + AverageHFR);
                            Console.WriteLine("Skipped: " + AverageSkippedHeartBeats);
                            skippedHeartBeatsAverage.Add(1);
                        }
                    }
                }
            }

            Reading = value;
        }

        [RelayCommand]
        private async Task Loaded()
        {
            scan = await Bluetooth.RequestLEScanAsync();
        }

        [RelayCommand]
        private void Unloaded()
        {
            if (scan is not null)
            {
                scan.Stop();
                scan = null;
            }

            subscribedCharacteristics.ForEach(x =>
            {
                x.CharacteristicValueChanged -= Characteristic_CharacteristicValueChanged;
                x.StopNotificationsAsync();
            });

            subscribedCharacteristics.Clear();
        }

        [RelayCommand]
        private void DoubleClick(MouseButtonEventArgs e)
        {
            switch (e.ChangedButton)
            {
                case MouseButton.Left:
                    ShowLabels = ShowLabels == GridLength.Auto ? new GridLength(0) : GridLength.Auto;
                    break;

                case MouseButton.Middle:
                    break;

                case MouseButton.Right:
                    break;
            }
        }

        [RelayCommand]
        private void MouseWheel(MouseWheelEventArgs e)
        {
            var isUp = e.Delta > 0;

            if (e.MouseDevice.MiddleButton == MouseButtonState.Pressed)
            {
                if (isUp)
                {
                    // ShowAverageBeatsPerMinute
                    // ShowAverageSkippedHeartBeats
                    // ShowAverageRR
                    // ShowAverageHFR
                    if (ShowAverageRR)
                    {
                        ShowAverageHFR = true;
                    }
                    else if (ShowAverageSkippedHeartBeats)
                    {
                        ShowAverageRR = true;
                    }
                    else if (ShowAverageBeatsPerMinute)
                    {
                        ShowAverageSkippedHeartBeats = true;
                    }
                    else
                    {
                        ShowAverageBeatsPerMinute = true;
                    }
                }
                else
                {
                    if (ShowAverageHFR)
                    {
                        ShowAverageHFR = false;
                    }
                    else if (ShowAverageRR)
                    {
                        ShowAverageRR = false;
                    }
                    else if (ShowAverageSkippedHeartBeats)
                    {
                        ShowAverageSkippedHeartBeats = false;
                    }
                    else
                    {
                        ShowAverageBeatsPerMinute = false;
                    }
                }
            }
            else
            {
                if (isUp)
                    FontSize += 1;
                else
                    FontSize -= 1;
            }
        }
    }
}
