using InTheHand.Bluetooth;
using System.Reflection.PortableExecutable;
using System.Runtime.Intrinsics.X86;
using System.Text;
using YamlDotNet.Serialization;

namespace BleExplorer
{
    public partial class Form1 : Form
    {
        private static ServiceUUID[] service_uuids;
        private static ServiceUUID[] characteristic_uuids;

        private bool showUnknownServices;
        private bool showUnknownCharacteristics;

        private List<GattCharacteristic> subscribedCharacteristics = new();
        private BluetoothLEScan? scan = null;

        public Form1()
        {
            InitializeComponent();
            UiSynchronization.Init();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var deserializer = new DeserializerBuilder()
                .Build();

            service_uuids = deserializer.Deserialize<Services>(Data.service_uuids).uuids;
            characteristic_uuids = deserializer.Deserialize<Services>(Data.characteristic_uuids).uuids;

            Bluetooth.AdvertisementReceived += Bluetooth_AdvertisementReceived;
        }

        private async void buttonStartStopScanning_Click(object sender, EventArgs e)
        {
            if (scan is not null)
            {
                scan.Stop();
                scan = null;
                buttonStartStopScanning.Text = "Start scanning";
                progressBarAdvertisement.Style = ProgressBarStyle.Blocks;
            }
            else
            {
                progressBarAdvertisement.Style = ProgressBarStyle.Marquee;
                buttonStartStopScanning.Text = "Stop scanning";
                scan = await Bluetooth.RequestLEScanAsync();
            }
        }

        private async void Bluetooth_AdvertisementReceived(object? sender, BluetoothAdvertisingEvent e)
        {
            if (e.Device is null)
                return;

            await UiSynchronization.SwitchToUiThread();

            listBoxAdvertisements.DisplayMember = nameof(Advertisement.Name);

            if (string.IsNullOrWhiteSpace(e.Name))
                return;

            var advertisement = new Advertisement(e.Name, e.Device.Id);
            if (!listBoxAdvertisements.Items.Contains(advertisement))
                listBoxAdvertisements.Items.Add(advertisement);
        }

        private async void listBoxAdvertisements_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxAdvertisements.SelectedIndex == -1 || listBoxAdvertisements.Items.Count == 0)
                return;

            listBoxServices.Items.Clear();
            listBoxCharacteristics.Items.Clear();
            byteViewer.SetBytes([]);

            if (listBoxAdvertisements.SelectedItem is not Advertisement advertisement)
                return;

            progressBarServices.Style = ProgressBarStyle.Marquee;
            try
            {
                var device = await BluetoothDevice.FromIdAsync(advertisement.DeviceId);
                if (device is null)
                    return;

                var services = await device.Gatt.GetPrimaryServicesAsync();

                if (services is null)
                    return;

                await UiSynchronization.SwitchToUiThread();
                listBoxServices.DisplayMember = nameof(Service.Name);

                foreach (var service in services)
                {
                    var uuid = service.Uuid.ToString();

                    var serviceName = service_uuids.FirstOrDefault(x => x.uuid.EndsWith(uuid))?.name;

                    if (serviceName is null)
                    {
                        if (!showUnknownServices)
                            continue;

                        serviceName = uuid;
                    }

                    if (string.IsNullOrWhiteSpace(serviceName))
                        continue;

                    var sservice = new Service(serviceName, service.Uuid);
                    if (!listBoxServices.Items.Contains(sservice))
                        listBoxServices.Items.Add(sservice);
                }
            }
            finally
            {
                await UiSynchronization.SwitchToUiThread();
                progressBarServices.Style = ProgressBarStyle.Blocks;
            }
        }

        private async void listBoxServices_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxServices.SelectedIndex == -1 || listBoxServices.Items.Count == 0)
                return;

            listBoxCharacteristics.Items.Clear();
            byteViewer.SetBytes([]);

            if (listBoxAdvertisements.SelectedItem is not Advertisement advertisement)
                return;

            if (listBoxServices.SelectedItem is not Service sservice)
                return;

            progressBarCharacteristics.Style = ProgressBarStyle.Marquee;
            try
            {
                var device = await BluetoothDevice.FromIdAsync(advertisement.DeviceId);
                if (device is null)
                    return;

                var service = await device.Gatt.GetPrimaryServiceAsync(sservice.Uuid);

                if (service is null)
                    return;

                var characteristics = await service.GetCharacteristicsAsync();

                if (characteristics is null)
                    return;

                await UiSynchronization.SwitchToUiThread();
                listBoxCharacteristics.DisplayMember = nameof(Characteristic.Name);

                foreach (var characteristic in characteristics)
                {
                    var uuid = characteristic.Uuid.ToString();
                    var characteristicName = characteristic_uuids.FirstOrDefault(x => x.uuid.EndsWith(uuid))?.name;

                    if (characteristicName is null)
                    {
                        if (!showUnknownCharacteristics)
                            continue;

                        characteristicName = uuid;
                    }

                    var ccharacteristic = new Characteristic(characteristicName, characteristic.Uuid);
                    if (!listBoxCharacteristics.Items.Contains(ccharacteristic))
                        listBoxCharacteristics.Items.Add(ccharacteristic);
                }
            }
            finally
            {
                progressBarCharacteristics.Style = ProgressBarStyle.Blocks;
            }
        }

        private async void listBoxCharacteristics_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxCharacteristics.SelectedIndex == -1 || listBoxCharacteristics.Items.Count == 0)
                return;

            byteViewer.SetBytes([]);

            if (listBoxAdvertisements.SelectedItem is not Advertisement advertisement)
                return;

            if (listBoxServices.SelectedItem is not Service sservice)
                return;

            if (listBoxCharacteristics.SelectedItem is not Characteristic ccharacteristic)
                return;

            progressBarByteViewer.Style = ProgressBarStyle.Marquee;

            try
            {
                var device = await BluetoothDevice.FromIdAsync(advertisement.DeviceId);
                if (device is null)
                    return;

                var service = await device.Gatt.GetPrimaryServiceAsync(sservice.Uuid);

                if (service is null)
                    return;

                var characteristic = await service.GetCharacteristicAsync(ccharacteristic.Uuid);

                if (characteristic is null)
                    return;

                var value = await characteristic.ReadValueAsync();

                if (value is null)
                    return;

                await UiSynchronization.SwitchToUiThread();
                byteViewer.SetBytes(value);
            }
            finally
            {
                await UiSynchronization.SwitchToUiThread();
                progressBarByteViewer.Style = ProgressBarStyle.Blocks;
            }
        }

        private void byteViewer_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var currentMode = (int)byteViewer.GetDisplayMode();

            currentMode++;

            if (currentMode > 4)
                currentMode = 1;

            byteViewer.SetDisplayMode((System.ComponentModel.Design.DisplayMode)currentMode);
        }
    }

    record Advertisement(string Name, string DeviceId);
    record Service(string Name, BluetoothUuid Uuid);
    record Characteristic(string Name, BluetoothUuid Uuid);

    record Services(ServiceUUID[] uuids)
    {
        public Services() : this([]) { }
    };

    record ServiceUUID(string uuid, string name, string id)
    {
        public ServiceUUID() : this("", "", "") { }
    };
}
