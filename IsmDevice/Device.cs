using IsmIoTPortal;
using IsmIoTPortal.Models;
using Microsoft.Azure.Devices.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;

namespace IsmDevice
{
    // Events Args für GUI
    //
    public class StateEventArgs : EventArgs
    {
        public StateEventArgs(string state)
        {
            State = state;
        }

        public string State { get; set; }
    }

    public class WriteLineEventArgs : EventArgs
    {
        public WriteLineEventArgs(string text, Color color)
        {
            Text = text;
            Color = color;
        }

        public WriteLineEventArgs(string text)
        {
            Text = text;
            Color = Colors.Black;
        }

        public string Text { get; set; }
        public Color Color { get; set; }
    }

    class Device
    {
        // Events für GUI
        //
        public event EventHandler StateChanged;
        public event EventHandler WriteLine;

        public void OnStateChanged(StateEventArgs e)
        {
            EventHandler stateChanged = StateChanged;
            if (stateChanged != null)
                stateChanged(this, e);
        }
        public void OnWriteLine(WriteLineEventArgs e)
        {
            EventHandler writeLine = WriteLine;
            if (writeLine != null)
                writeLine(this, e);
        }



        // 
        public System.Threading.Timer Timer { get; set; }

        //
        public DeviceClient DeviceClient { get; set; }
        private string iotHubUri;
        private string deviceKey;

        //
        public IDeviceState state;
        public ReadyState readyState;
        public PreviewState previewState;
        public RunState runState;

        // Device Settings sind Algorithmus-Parameter, Kamera-Parameter und Pulser-Parameter
        public DeviceState DeviceState { get; set; }

        //
        public string ContainerName { get; set; }
        public CloudBlobClient BlobClient { get; set; }
        // Simulate camera with a list of image streams
        public List<byte[]> CameraCaptures { get; set; }
        public Random Rand { get; set; } = new Random();

        public byte[] CurrentCameraCapture { get; set; }

        //
        public string DeviceId { get; set; }

        public Device(string deviceid, string iothuburi, string devicekey, string storageConnectionString, string containerName)
        {
            // Initialisiere DeviceState und setze Default Werte
            DeviceState = new DeviceState();
            DeviceState.DeviceId = deviceid;
            DeviceState.VarianceThreshold = 0.0025;
            DeviceState.DistanceMapThreshold = 8.5;
            DeviceState.RGThreshold = 3.75;
            DeviceState.RestrictedFillingThreshold = 4;
            DeviceState.DilateValue = 16;
            DeviceState.CapturePeriod = 10;

            // Timer that ticks in DeviceState.CapturePeriod
            /*
                Im Ready-State ist der Timer gestoppt (dueTime ist Infinite) 
                und wird beim Übergang in Run- bzw. Preview-State gestartet (dueTime ist dann 0)
            */
            Timer = new System.Threading.Timer((s) =>
                {
                    state.DoWorkAsync();
                },
                null,
                System.Threading.Timeout.Infinite,
                DeviceState.CapturePeriod * 1000
            );

            //
            readyState = new ReadyState(this);
            previewState = new PreviewState(this);
            runState = new RunState(this);

            //
            ContainerName = containerName;
            BlobClient = CloudStorageAccount.Parse(storageConnectionString).CreateCloudBlobClient();
            //CameraCaptures = CreateSimulatedCameraCaptures();

            // connect to IoT Hub
            this.DeviceId = deviceid;
            this.iotHubUri = iothuburi;
            this.deviceKey = devicekey;
            this.DeviceClient = DeviceClient.Create(iotHubUri, new DeviceAuthenticationWithRegistrySymmetricKey(DeviceId, deviceKey), TransportType.Http1);
        }

        public async void StartDevice()
        {
            state = readyState; // initial state
            DeviceState.State = DeviceStates.READY_STATE;

            // fire state changed event
            StateEventArgs stateEventArgs = new StateEventArgs(DeviceState.State);
            OnStateChanged(stateEventArgs);

            OnWriteLine(new WriteLineEventArgs("Loading captures..."));
            CameraCaptures = await CreateSimulatedCameraCaptures();
            OnWriteLine(new WriteLineEventArgs("Captures loaded!"));

            // ReceiveC2dAsync läuft mit while(true) Schleife während der gesamten Lebensdauer des Devices
            ReceiveC2dAsync();
            OnWriteLine(new WriteLineEventArgs("Device is ready to receive cloud to device messages!"));
        }


        private async void ReceiveC2dAsync()
        {
            while (true)
            {
                Message receivedMessage = null;
                try 
                {
                    receivedMessage = await DeviceClient.ReceiveAsync();
                    if (receivedMessage == null) continue;
                }
                // Unhandled exceptions restart the UWP app. Retry receiving C2D messages after delay and log the exception on screen.
                catch (Exception ex)
                {
                    OnWriteLine(new WriteLineEventArgs("Connection lost. Retry.", Colors.DarkOrange));
                    await Task.Delay(10000);
                    continue;
                }

                // Anmerkung: Message Body darf nur einmal ausgelesen werden mit GetBytes(). Wenn man das hier schon macht, sollte man
                // ihn also speichern für die einzelnen cases, weil ein erneutes Lesen dort zu Exception führen würde      
                //string msg = Encoding.UTF8.GetString(receivedMessage.GetBytes());

                if (receivedMessage.Properties.ContainsKey("command"))
                {
                    OnWriteLine(new WriteLineEventArgs(String.Format("Received message: {0}", receivedMessage.Properties["command"]), Colors.Red));
                    switch (receivedMessage.Properties["command"])
                    {
                        // Commands from Portal
                        //
                        case CommandType.START: Start(receivedMessage); continue;

                        case CommandType.STOP: Stop(receivedMessage); continue;

                        case CommandType.START_PREVIEW: StartPreview(receivedMessage); continue;

                        case CommandType.STOP_PREVIEW: StopPreview(receivedMessage); continue;

                        // Commands from Dashboard
                        //

                        // TODO: D2CSendDeviceStateAsync auch abhängig von state regeln? oder ist es überall erlaubt und das selbe?
                        case CommandType.GET_DEVICE_STATE: await GetDeviceState(receivedMessage); continue;

                        case CommandType.SET_DEVICE_STATE: await SetDeviceStateAsync(receivedMessage); continue;

                        default:
                            await DeviceClient.RejectAsync(receivedMessage);
                            break;
                    }
                }
            }
        }

        public async Task SendDeviceToCloudMessagesAsync()
        {
            // Das DeviceState Objekt enthält Infos, alle einstellbaren Parameter und die Uri des aktuellen Captures
            string serializedDeviceState = JsonConvert.SerializeObject(DeviceState);

            var message = new Message(Encoding.ASCII.GetBytes(serializedDeviceState));
            //Depending on State use messageType DAT | PRV
            if (DeviceState.State == DeviceStates.RUN_STATE)
                message.Properties["messageType"] = CommandType.DAT; // DAT
            else if (DeviceState.State == DeviceStates.PREVIEW_STATE)
                message.Properties["messageType"] = CommandType.PRV; // PRV

            message.Properties["DeviceId"] = DeviceId;
            message.MessageId = Guid.NewGuid().ToString();

            await DeviceClient.SendEventAsync(message);
            //OnWriteLine(new WriteLineEventArgs(String.Format("{0} > Sending d2c message: {1}", DateTime.Now, DeviceState.CurrentCaptureUri)));
        }

        public async Task D2CSendDeviceStateAsync()
        {
            string serializedDeviceState = JsonConvert.SerializeObject(DeviceState);

            // != Microsoft.Azure.Devices.Message von Cloud Seite. z.B. gibt es hier kein Ack Property
            var message = new Microsoft.Azure.Devices.Client.Message(Encoding.ASCII.GetBytes(serializedDeviceState));

            // Use messageType UPDATE_DASHBOARD_CONTROLS when sending the DeviceState für Dasboard Control updates
            message.Properties["messageType"] = CommandType.UPDATE_DASHBOARD_CONTROLS;
            message.Properties["DeviceId"] = DeviceId;
            message.MessageId = Guid.NewGuid().ToString();
            await DeviceClient.SendEventAsync(message);
            OnWriteLine(new WriteLineEventArgs(String.Format("{0} > Sending DeviceState for Dashboard-Controls", DateTime.Now), Colors.Red));
        }

        // BlobUri generation
        public async Task<string> GenerateBlobUriAsync()
        {
            var storageAccount = CloudStorageAccount.Parse(IsmIoTSettings.Settings.ismiotstorage); //CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=picturesto;AccountKey=IxESdcVI3BxmL0SkoDsWx1+B5ZDArMHNrQlQERpcCo3e6eOCYptJTTKMin6KIbwbRO2CcmVpcn/hJ2/krrUltA==");
            var blobClient = storageAccount.CreateCloudBlobClient();
            var blobContainer = blobClient.GetContainerReference(IsmIoTSettings.Settings.containerCaptureUploads); //blobClient.GetContainerReference("ismiot");
            await blobContainer.CreateIfNotExistsAsync();

            var blobName = String.Format("deviceUpload_{0}", Guid.NewGuid().ToString());
            CloudBlockBlob blob = blobContainer.GetBlockBlobReference(blobName);

            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy();
            //sasConstraints.SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-5);
            //sasConstraints.SharedAccessExpiryTime = DateTime.UtcNow.AddHours(24);
            sasConstraints.SharedAccessStartTime = DateTime.UtcNow.AddDays(-1);
            sasConstraints.SharedAccessExpiryTime = DateTime.UtcNow.AddDays(1);
            sasConstraints.Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write;
            string sasBlobToken = blob.GetSharedAccessSignature(sasConstraints);

            return blob.Uri + sasBlobToken;
        }

        public void Start(Message msg)
        {
            state.StartAsync(msg);
        }

        public void Stop(Message msg)
        {
            state.StopAsync(msg);
        }

        public void StartPreview(Message msg)
        {
            state.StartPreviewAsync(msg);
        }

        public void StopPreview(Message msg)
        {
            state.StopPreviewAsync(msg);
        }

        public async Task GetDeviceState(Message msg)
        {
            await D2CSendDeviceStateAsync();
            await DeviceClient.CompleteAsync(msg);
        }

        public async Task SetDeviceStateAsync(Message msg)
        {
            await state.SetDeviceStateAsync(msg);
        }

        public async Task<List<byte[]>> CreateSimulatedCameraCaptures()
        {
            // Simulate the camera, get some images from blob storage and hold them in List as Memory Streams / Byte arrays
            var container = BlobClient.GetContainerReference(ContainerName);

            // http://stackoverflow.com/questions/16052813/getting-list-the-blob-in-winrt-application
            BlobContinuationToken continuationToken = null;
            List<IListBlobItem> blobs = new List<IListBlobItem>();
            do
            {
                var listingResult = await container.ListBlobsSegmentedAsync(continuationToken);
                continuationToken = listingResult.ContinuationToken;
                blobs.AddRange(listingResult.Results);
            }
            while (continuationToken != null);

            List<byte[]> cameraCaptures = new List<byte[]>();
            byte[] buf;
            foreach (var item in blobs)
            {
                CloudBlockBlob blob = (CloudBlockBlob)item;

                buf = new byte[blob.Properties.Length];

                await blob.DownloadToByteArrayAsync(buf, 0);
                cameraCaptures.Add(buf);
            }

            return cameraCaptures;
        }

    }
}
