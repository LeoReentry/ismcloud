using IsmIoTPortal;
using IsmIoTPortal.Models;
using Microsoft.Azure.Devices.Client;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace IsmDevice
{
    class PreviewState : IDeviceState
    {
        private Device device;

        public PreviewState(Device device)
        {
            this.device = device;
        }

        public async Task StartAsync(Message msg)
        {
            device.OnWriteLine(new WriteLineEventArgs(" - Can't go to run mode from preview mode device, stop preview first!"));
            await device.DeviceClient.RejectAsync(msg);
        }

        public async Task StartPreviewAsync(Message msg)
        {
            device.OnWriteLine(new WriteLineEventArgs(" - Device is already running in preview mode!"));
            await device.DeviceClient.RejectAsync(msg);
        }

        public async Task StopAsync(Message msg)
        {
            device.OnWriteLine(new WriteLineEventArgs(" - Device is not running in run mode"));
            await device.DeviceClient.RejectAsync(msg);
        }

        public async Task StopPreviewAsync(Message msg)
        {
            device.OnWriteLine(new WriteLineEventArgs(" + Stop running the preview and go back to ready"));
            await device.DeviceClient.CompleteAsync(msg);

            device.state = device.readyState;
            device.DeviceState.State = DeviceStates.READY_STATE;
            // fire state changed event
            StateEventArgs stateEventArgs = new StateEventArgs(device.DeviceState.State);
            device.OnStateChanged(stateEventArgs);

            //await device.state.DoWork(device.CancellationTokenSource.Token);
            //device.Timer.Stop();
            device.Timer.Change(System.Threading.Timeout.Infinite, device.DeviceState.CapturePeriod * 1000);
        }

        public async Task SetDeviceStateAsync(Message msg)
        {
            device.OnWriteLine(new WriteLineEventArgs("Setting new DeviceState", Colors.Red));

            string msgbody = Encoding.UTF8.GetString(msg.GetBytes());
            DeviceState deviceState = JsonConvert.DeserializeObject<DeviceState>(msgbody);

            // Neue Werte setzen
            this.device.DeviceState.CapturePeriod = deviceState.CapturePeriod;
            //this.device.Timer.Interval = TimeSpan.FromSeconds(this.device.DeviceState.CapturePeriod);
            device.Timer.Change(0, device.DeviceState.CapturePeriod * 1000);

            this.device.DeviceState.VarianceThreshold = deviceState.VarianceThreshold;
            this.device.DeviceState.RestrictedFillingThreshold = deviceState.RestrictedFillingThreshold;
            this.device.DeviceState.RGThreshold = deviceState.RGThreshold;
            this.device.DeviceState.DistanceMapThreshold = deviceState.DistanceMapThreshold;
            this.device.DeviceState.DilateValue = deviceState.DilateValue;

            await this.device.DeviceClient.CompleteAsync(msg);
        }

        public async Task DoWorkAsync()
        {
            // Zum testen simuliere Kamera Capture durch zufälliges Element aus List<byte[]> mit vorgeladenen Bildern
            device.CurrentCameraCapture = device.CameraCaptures.ElementAt(device.Rand.Next(device.CameraCaptures.Count));
            // Get reference to BLOB
            var blob = await device.GenerateBlobAsync();
            // Upload BLOB (we don't need a SAS here since we're already authenticated)
            await blob.UploadFromByteArrayAsync(device.CurrentCameraCapture, 0, device.CurrentCameraCapture.Length);
            device.DeviceState.CurrentCaptureName = blob.Name;
            await device.SendDeviceToCloudMessagesAsync();
        }
    }
}
