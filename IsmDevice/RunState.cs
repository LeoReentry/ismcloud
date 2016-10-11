using IsmIoTPortal;
using IsmIoTPortal.Models;
using Microsoft.Azure.Devices.Client;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace IsmDevice
{
    class RunState : IDeviceState
    {
        private Device device;

        public RunState(Device device)
        {
            this.device = device;
        }

        public async Task StartAsync(Message msg)
        {
            device.OnWriteLine(new WriteLineEventArgs(" - Device is already running"));
            await device.DeviceClient.RejectAsync(msg);
        }

        public async Task StartPreviewAsync(Message msg)
        {
            device.OnWriteLine(new WriteLineEventArgs(" - Can't go to preview mode from running device, stop it first!"));
            await device.DeviceClient.RejectAsync(msg);
        }

        public async Task StopAsync(Message msg)
        {
            device.OnWriteLine(new WriteLineEventArgs(" + Stop running the device and go back to ready"));
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

        public async Task StopPreviewAsync(Message msg)
        {
            device.OnWriteLine(new WriteLineEventArgs(" - Device is not running in preview mode"));
            await device.DeviceClient.RejectAsync(msg);
        }

        public async Task SetDeviceStateAsync(Message msg)
        {
            await this.device.DeviceClient.RejectAsync(msg);
            device.OnWriteLine(new WriteLineEventArgs("Reject setting DeviceState in Run State", Colors.Red));
        }

        public async Task DoWorkAsync()
        {
            // Zum testen simuliere Kamera Capture durch zufälliges Element aus List<byte[]> mit vorgeladenen Bildern
            device.CurrentCameraCapture = device.CameraCaptures.ElementAt(device.Rand.Next(device.CameraCaptures.Count));
            // Get reference to BLOB
            var blob = await device.GenerateBlobUriAsync();
            // Upload BLOB (we don't need a SAS here since we're already authenticated)
            await blob.UploadFromByteArrayAsync(device.CurrentCameraCapture, 0, device.CurrentCameraCapture.Length);
            device.DeviceState.CurrentCaptureUri = blob.Uri.ToString();
            await device.SendDeviceToCloudMessagesAsync();
        }
    }
}
