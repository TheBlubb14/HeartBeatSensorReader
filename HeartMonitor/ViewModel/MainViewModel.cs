using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeartMonitor.Model;
using InTheHand.Bluetooth;
using System.Windows.Input;

namespace HeartMonitor.ViewModel
{
    public sealed partial class MainViewModel : ObservableRecipient
    {
        [ObservableProperty]
        private HeartRateReading reading;

        [ObservableProperty]
        private double fontSize = 15;

        private BluetoothLEScan? scan = null;
        private readonly List<GattCharacteristic> subscribedCharacteristics = [];

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

            if (newReading is not null)
                Reading = newReading.Value;
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
                    FontSize += 5;
                    break;

                case MouseButton.Middle:
                    break;

                case MouseButton.Right:
                    FontSize -= 5;
                    break;
            }
        }
    }
}
