using IsmIoTPortal;
using IsmIoTPortal.Models;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace IsmDevice
{
    class ReadyState : IDeviceState
    {
        private Device device;
        public ReadyState(Device device)
        {
            this.device = device;
        }

        public async Task StartAsync(Message msg)
        {
            device.OnWriteLine(new WriteLineEventArgs(" + Starting to run device!"));
            await device.DeviceClient.CompleteAsync(msg);

            // Erstelle List<byte[]> aus alten Aufnahmen zur Simulation
            //device.CameraCaptures = await device.CreateSimulatedCameraCaptures();

            device.state = device.runState;
            device.DeviceState.State = DeviceStates.RUN_STATE; //-->CommandType.DAT;
            // fire state changed event
            StateEventArgs stateEventArgs = new StateEventArgs(device.DeviceState.State);
            device.OnStateChanged(stateEventArgs);

            //await device.state.DoWork(device.CancellationTokenSource.Token);
            //device.Timer.Start();
            device.Timer.Change(0, device.DeviceState.CapturePeriod * 1000);
        }

        public async Task StartPreviewAsync(Message msg)
        {
            device.OnWriteLine(new WriteLineEventArgs(" + Starting to run device in preview mode!"));
            await device.DeviceClient.CompleteAsync(msg);

            // Erstelle List<byte[]> aus alten Aufnahmen zur Simulation
            //device.CameraCaptures = await device.CreateSimulatedCameraCaptures();

            device.state = device.previewState;
            device.DeviceState.State = DeviceStates.PREVIEW_STATE; //-->CommandType.PRV;
            // fire state changed event
            StateEventArgs stateEventArgs = new StateEventArgs(device.DeviceState.State);
            device.OnStateChanged(stateEventArgs);

            //await device.state.DoWork(device.CancellationTokenSource.Token);
            //device.Timer.Start();
            device.Timer.Change(0, device.DeviceState.CapturePeriod * 1000);
        }

        public async Task StopAsync(Message msg)
        {
            device.OnWriteLine(new WriteLineEventArgs(" - Device is not running"));
            await device.DeviceClient.RejectAsync(msg);
        }

        public async Task StopPreviewAsync(Message msg)
        {
            device.OnWriteLine(new WriteLineEventArgs(" - Device is not running"));
            await device.DeviceClient.RejectAsync(msg);
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
            //...
        }
    }
}
