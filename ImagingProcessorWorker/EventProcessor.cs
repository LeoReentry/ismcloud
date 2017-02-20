using IsmIoTPortal.Models;
using IsmIoTSettings;
using Logging;
using MathWorks.MATLAB.NET.Arrays;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.Azure.Devices;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using MLFilamentDetection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Commands;

namespace ImagingProcessorWorker
{
    // Event Args
    class LogMessageEventArgs : EventArgs
    {
        public string Message { get; set; }
        public Logging.Fontcolors Color { get; set; }
        public bool List { get; set; }

        public LogMessageEventArgs(string msg)
        {
            Message = msg;

            // TODO: Könnte durch Überladene Konstruktoren noch wählbar sein
            Color = Fontcolors.BLACK;
            List = false;
        }
    }


    class EventProcessor : IEventProcessor
    {
        private Stopwatch checkpointStopWatch;
        // connection string for storage
        //public static string StorageConnectionString = "DefaultEndpointsProtocol=https;AccountName=iothubismstorage;AccountKey=ieSaRzvsZQk6towpz/6uVobGszRQMFulk+noM3yNeWBFWV6d+Won6DKiZTdZCnO0DJM10Ghch6Z0Majk2qYIaA==";

        // connection string for the queues send rule
        //public static string ServiceBusConnectionString = "Endpoint=sb://filamentdataevents-ns.servicebus.windows.net/;SharedAccessKeyName=send;SharedAccessKey=ThUGPxhO0WEowGf4Kog8qTw/YQQz36euZTpnTIcUTx0=";

        // EventHub that is input for Stream-Analytics
        //static string eventHubName = "ismioteventhub";
        //static string eventHubConnectionString = "Endpoint=sb://filamentdataevents-ns.servicebus.windows.net/;SharedAccessKeyName=SendRule;SharedAccessKey=fhGK380m88VIR0HxrPdyrGsDJbZKOneqmJoUz2bgX+A=";
        EventHubClient eventHubClient;

        // SignalR
        //static HubConnection signalRHubConnection = null;
        //static IHubProxy signalRHubProxy = null;

        // SB Queue
        private QueueClient queueClient;

        // Events
        public event EventHandler LogMessage;
        public void OnLogMessage(LogMessageEventArgs e)
        {
            EventHandler logMessage = LogMessage;
            if (logMessage != null)
                logMessage(this, e);
        }

        public event EventHandler ProcessorClosed;
        public void OnProcessorClosed()
        {
            EventHandler handler = this.ProcessorClosed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        public EventProcessor()
        {
            // In OpenAsync verschoben
            //queueClient = QueueClient.CreateFromConnectionString(ServiceBusConnectionString, "dashboardqueue");
            //this.OnLogMessage(new LogMessageEventArgs(String.Format("{0} > EventProcessor Constructor: Initialize Queue Client for {1} <br>", DateTime.Now.ToString(), "dashboardqueue")));
        }

        public Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            // where you close the processor.
            this.OnLogMessage(new LogMessageEventArgs(String.Format("{0} > Close called for processor with PartitionId '{1}' and Owner: {2} with reason '{3}'. <br>", DateTime.Now.ToString(), context.Lease.PartitionId, context.Lease.Owner ?? string.Empty, reason)));

            this.checkpointStopWatch.Stop();
            this.OnProcessorClosed();
            return context.CheckpointAsync();
        }

        public Task OpenAsync(PartitionContext context)
        {
            // here is the place for you to initialize your processor, e.g. db connection or other stuff. please ensure you have
            // retry or fault handling properly
            this.OnLogMessage(new LogMessageEventArgs(String.Format("{0} > Processor Initializing for PartitionId '{1}' and Owner: {2}. <br>", DateTime.Now.ToString(), context.Lease.PartitionId, context.Lease.Owner ?? string.Empty)));

            // SB Queue
            queueClient = QueueClient.CreateFromConnectionString(CloudConfigurationManager.GetSetting("sbRootManage"), CloudConfigurationManager.GetSetting("dashboardqueue_name"));
            this.OnLogMessage(new LogMessageEventArgs(String.Format("{0} > EventProcessor Constructor: Initialize Queue Client for {1} <br>", DateTime.Now.ToString(), CloudConfigurationManager.GetSetting("dashboardqueue_name"))));

            // SignalR
            //InitializeSignalR();

            this.checkpointStopWatch = new Stopwatch();
            this.checkpointStopWatch.Start();

            //eventHubClient = EventHubClient.CreateFromConnectionString(eventHubConnectionString, eventHubName);
            eventHubClient = EventHubClient.CreateFromConnectionString(CloudConfigurationManager.GetSetting("sbRootManage")/*System.Configuration.ConfigurationSettings.AppSettings.Get("ismioteventhub_send")*/, CloudConfigurationManager.GetSetting("eventHubName"));

            return Task.FromResult<object>(null);
        }

        public Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            try
            {

                foreach (EventData eventData in messages)
                {
                    // This case shows the new planed convention. Use EventType as Key and CommandType as Value
                    if (eventData.Properties.ContainsKey(Commands.EventType.D2C_COMMAND) && ((string)eventData.Properties[Commands.EventType.D2C_COMMAND] == Commands.CommandType.CAPTURE_UPLOADED))
                    {
                        string serializedDeviceState = Encoding.UTF8.GetString(eventData.GetBytes());
                        DeviceSettings DeviceSettings = JsonConvert.DeserializeObject<DeviceSettings>(serializedDeviceState);
                        Task.Factory.StartNew(() => ProcessImage(DeviceSettings));
                    }
                    // UPDATE_DASHBOARD_CONTROLS
                    else if (eventData.Properties.ContainsKey(IsmIoTPortal.CommandType.D2C_COMMAND) && (string)eventData.Properties[IsmIoTPortal.CommandType.D2C_COMMAND] == IsmIoTPortal.CommandType.UPDATE_DASHBOARD_CONTROLS)
                    {
                        string serializedDeviceSettings = Encoding.UTF8.GetString(eventData.GetBytes());
                        DeviceSettings DeviceSettings = JsonConvert.DeserializeObject<DeviceSettings>(serializedDeviceSettings);
                        Task.Factory.StartNew(() => UpdateDashboardDeviceStateControls(DeviceSettings));
                    }
                    else if (eventData.Properties.ContainsKey(IsmIoTPortal.CommandType.D2C_COMMAND) && (string)eventData.Properties[IsmIoTPortal.CommandType.D2C_COMMAND] == IsmIoTPortal.CommandType.FIRMWARE_UPDATE_STATUS)
                    {
                        string serializedUpdateState = Encoding.UTF8.GetString(eventData.GetBytes());
                        var updateState = JsonConvert.DeserializeObject<UpdateState>(serializedUpdateState);
                        Task.Factory.StartNew(() => UpdateFirmwareUpdateStatus(updateState));
                    }
                }
            }
            catch (Exception ex)
            {
                //...
            }

            if (this.checkpointStopWatch.Elapsed > TimeSpan.FromMinutes(5))
            {
                lock (this)
                {
                    this.checkpointStopWatch.Reset();
                    return context.CheckpointAsync();
                }
            }

            return Task.FromResult<object>(null);

        }

        private void UpdateFirmwareUpdateStatus(UpdateState updateState)
        {
            using (IsmIoTPortalContext db = new IsmIoTPortalContext())
            {
                string deviceId = updateState.DeviceId;
                var device = db.IsmDevices.FirstOrDefault(d => d.DeviceId.Equals(deviceId));
                if (device == null)
                {
                    this.OnLogMessage(new LogMessageEventArgs(String.Format("{0} > UpdateFirmwareUpdateStatus Error: DeviceId unknown.<br>", DateTime.Now.ToString())));
                    return;
                }
                device.UpdateStatus = IsmIoTSettings.UpdateStatus.READY;
                var release = db.Releases.First(r => r.Num == device.Software.Num + 1);
                device.SoftwareId = release.SoftwareId;
                db.SaveChanges();
                this.OnLogMessage(new LogMessageEventArgs(String.Format("{0} > UpdateFirmwareUpdateStatus Info: Update Log: {1} <br>", DateTime.Now.ToString(), updateState.Log)));
            }
        }
        private void UpdateDashboardDeviceStateControls(DeviceSettings DeviceSettings)
        {
            try
            {

                // Send message to queue for dashboard controls update
                //    
                var queueMessage = new BrokeredMessage(DeviceSettings);
                queueMessage.Label = IsmIoTPortal.CommandType.UPDATE_DASHBOARD_CONTROLS; // BrokeredMessage.Label reicht aus um Nachrichtentyp festzulegen
                queueClient.SendAsync(queueMessage).Wait();


                /*
                //Sende Values der Controls mit SignalR and Dashboard
                signalRHubProxy.Invoke("ValuesForDashboardControls", DeviceState.DeviceId, DeviceState.CapturePeriod,
                    DeviceState.VarianceThreshold, DeviceState.DistanceMapThreshold, DeviceState.RGThreshold,
                    DeviceState.RestrictedFillingThreshold, DeviceState.DilateValue).ContinueWith(t =>
                    {
                        //Console.WriteLine(t.Result);
                        this.OnLogMessage(new LogMessageEventArgs(String.Format("{0} > Send Values for Controls to Dashboard <br>", DateTime.Now.ToString())));
                    });
                */
            }
            catch (Exception ex)
            {
                this.OnLogMessage(new LogMessageEventArgs(String.Format("{0} > UpdateDashboardDeviceStateControls Exception: {1} <br>", DateTime.Now.ToString(), ex.Message)));
            }
        }

        /// <summary>
        /// Generates a URI with SAS to a specific BLOB.
        /// Grants Read Access for 15 minutes.
        /// </summary>
        /// <param name="blobName">Name of the BLOB access to is wanted.</param>
        /// <returns>Complete URI as string.</returns>
        private string GetBlobSasUri(string blobName)
        {
            // Get access to container
            var storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("storageConnection"));
            var blobClient = storageAccount.CreateCloudBlobClient();
            var blobContainer = blobClient.GetContainerReference(CloudConfigurationManager.GetSetting("containerPortal"));
            // Get BLOB (by filename, not full URI)
            var blob = blobContainer.GetBlockBlobReference(blobName);
            // Access Policy
            var policy = new SharedAccessBlobPolicy()
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessStartTime = DateTimeOffset.UtcNow.AddMinutes(-15),
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddMinutes(15)
            };
            // Return full URI
            return blob.Uri + blob.GetSharedAccessSignature(policy);
        }

        private void ProcessImage(DeviceSettings DeviceSettings)
        {
            try
            {
                //
                MWArray[] argsIn = new MWArray[6];
                string blobname = DeviceSettings.CurrentCaptureName;
                argsIn[0] = GetBlobSasUri(blobname); // Path / Uri of the image (with Shared Access Signature for MATLAB)            
                argsIn[1] = DeviceSettings.VarianceThreshold; //var_thresh = 0.0025; % variance threshold
                argsIn[2] = DeviceSettings.DistanceMapThreshold; //dist_thresh = 8.5; % distance - map threshold
                argsIn[3] = DeviceSettings.RGThreshold; //RG_thresh = 3.75; % R.R.G.threshold
                argsIn[4] = DeviceSettings.RestrictedFillingThreshold; //fill_area = 4; % Restricted filling threshold
                argsIn[5] = DeviceSettings.DilateValue; // dilate_value = 16; % Size of square SE used for dilation of dist.- map mask

                MWArray[] argsOut = new MWArray[3]; // [TEFL, real_length, img_colored]
                FilamentDetection filamentDetection = new FilamentDetection();

                filamentDetection.new_detectFilaments(3, ref argsOut, argsIn); // MATLAB call

                //
                double fl = (double)((MWNumericArray)argsOut[0]); // FL (TEFL)

                double[] single_lengths;
                if (((MWNumericArray)argsOut[1]).IsEmpty)
                    single_lengths = new double[] { };
                else
                    single_lengths = (double[])((MWNumericArray)argsOut[1]).ToVector(MWArrayComponent.Real); // TODO: NullReference checks

                int h1 = 0, h2 = 0, h3 = 0, h4 = 0, h5 = 0, h6 = 0, h7 = 0, h8 = 0, h9 = 0, h10 = 0;
                for (int i = 0; i < single_lengths.Length; i++)
                {
                    switch (GetInterval(single_lengths[i]))
                    {
                        case 0: h1++; break;
                        case 1: h2++; break;
                        case 2: h3++; break;
                        case 3: h4++; break;
                        case 4: h5++; break;
                        case 5: h6++; break;
                        case 6: h7++; break;
                        case 7: h8++; break;
                        case 8: h9++; break;
                        case 9: h10++; break;
                        default:
                            break;
                    }
                }

                double fc = single_lengths.Length; // FC

                // colored image
                CloudBlockBlob blob;
                string coloredImage = argsOut[2].ToString();
                using (System.IO.FileStream fs = new System.IO.FileStream(coloredImage, System.IO.FileMode.Open))
                {
                    blob = GenerateBlobAsync().Result;
                    fs.Position = 0;
                    blob.UploadFromStream(fs);
                }
                File.Delete(coloredImage);

                //
                using (IsmIoTPortalContext db = new IsmIoTPortalContext())
                {
                    /*
                        IsmDeviceId ist Foreign-Key von FilamentData zu IsmDevice
                    */
                    int id = db.IsmDevices.Where(d => d.DeviceId == DeviceSettings.DeviceId).First().IsmDeviceId;

                    IsmIoTPortal.Models.FilamentData fildata = new IsmIoTPortal.Models.FilamentData(fc, fl, id, h1, h2, h3, h4, h5, h6, h7, h8, h9, h10,
                        DeviceSettings.DeviceId, DeviceSettings.CurrentCaptureName, blob.Name);

                    // 1.) Sende in Queue für DashboardBroker
                    // FilamentData ist [DataContract], somit sind die Objekte mit DataContractSerializer serialisierbar
                    var queueMessage = new BrokeredMessage(fildata);
                    queueMessage.Label = IsmIoTPortal.CommandType.PRV; // BrokeredMessage.Label reicht aus um Nachrichtentyp festzulegen

                    // CapturePeriod als Information für DashboardBroker mitsenden.
                    queueMessage.Properties.Add(new KeyValuePair<string, object>("capturePeriod", DeviceSettings.CapturePeriod));

                    queueClient.SendAsync(queueMessage).Wait();

                    /*
                    // 1.) Sende Daten an Dashboards
                    signalRHubProxy.Invoke<string>("DataForDashboard", fildata.DeviceId, fildata.BlobUriImg, fildata.FC.ToString(), fildata.FL.ToString(), fildata.BlobUriColoredImg).ContinueWith(t =>
                    {
                        //Console.WriteLine(t.Result);
                        this.OnLogMessage(new LogMessageEventArgs(String.Format("{0} > Send Filament Data to Dashboard <br>BlobUriimg: {1}", DateTime.Now.ToString(), fildata.BlobUriImg)));
                    });
                    */

                    // 2.) Bei DAT sende die Daten in EventHub, bei PRV nicht
                    if (DeviceSettings.StateName == IsmIoTPortal.DeviceStates.RUN_STATE)
                    {
                        ForwaredFilamentDataToEventHub(fildata);
                    }
                }
            }
            catch (Exception ex)
            {
                this.OnLogMessage(new LogMessageEventArgs(String.Format("{0} > ProcessImage Exception: {1} <br>", DateTime.Now.ToString(), ex.Message)));
            }
        }

        private void ForwaredFilamentDataToEventHub(FilamentData filamentData)
        {
            try
            {
                // Code aus Testprojekt EventHubTest
                string serializedData = JsonConvert.SerializeObject(filamentData);
                EventData eventData = new EventData(Encoding.UTF8.GetBytes(serializedData));
                eventHubClient.Send(eventData);
            }
            catch (Exception ex)
            {
                this.OnLogMessage(new LogMessageEventArgs(String.Format("{0} > ForwaredFilamentDataToEventHub Exception: {1} <br>", DateTime.Now.ToString(), ex.Message)));
            }
        }

        /// <summary>
        /// Generates a new BLOB.
        /// </summary>
        /// <returns>Reference to BLOB.</returns>
        public async Task<CloudBlockBlob> GenerateBlobAsync()
        {
            //var storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=picturesto;AccountKey=IxESdcVI3BxmL0SkoDsWx1+B5ZDArMHNrQlQERpcCo3e6eOCYptJTTKMin6KIbwbRO2CcmVpcn/hJ2/krrUltA==");
            // Connect to BLOB container
            string conStr = CloudConfigurationManager.GetSetting("storageConnection");
            var storageAccount = CloudStorageAccount.Parse(conStr);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var blobContainer = blobClient.GetContainerReference(CloudConfigurationManager.GetSetting("containerPortal"));
            // Generate new BLOB
            var blobName = String.Format("coloredImage_{0}", Guid.NewGuid().ToString());
            return blobContainer.GetBlockBlobReference(blobName);
        }

        public int GetInterval(double val)
        {
            if (val < 0)
                throw new ArgumentException("Invalid parameter. Require positive double value.", "val");

            var v = Math.Round(val);

            if (v >= 0 && v <= 15)
                return 0;
            else if (v >= 16 && v <= 25)
                return 1;
            else if (v >= 26 && v <= 35)
                return 2;
            else if (v >= 36 && v <= 45)
                return 3;
            else if (v >= 46 && v <= 55)
                return 4;
            else if (v >= 56 && v <= 65)
                return 5;
            else if (v >= 66 && v <= 75)
                return 6;
            else if (v >= 76 && v <= 85)
                return 7;
            else if (v >= 86 && v <= 95)
                return 8;
            else
                return 9;
        }

        /*
        private static void InitializeSignalR()
        {
            // SignalR 
            //
            if (signalRHubConnection == null)
            {
                // local
                //signalRHubConnection = new HubConnection("http://localhost:39860/");
                // cloud
                signalRHubConnection = new HubConnection("http://" + Settings.webDomain);

                signalRHubProxy = signalRHubConnection.CreateHubProxy("DashboardHub");

                // Connect
                signalRHubConnection.Start().ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        t.Exception.Handle(e =>
                        {
                            Console.WriteLine(e.Message);
                            return true;
                        });
                    }
                    else
                    {
                        Console.WriteLine("Verbindung aufgebaut!");
                    }
                }).Wait();
            }
        }
        */
    }
}
