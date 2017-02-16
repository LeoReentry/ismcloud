using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.ServiceBus.Messaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Runtime.Serialization;
using IsmIoTPortal;
using IsmIoTPortal.Models;
using IsmIoTSettings;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;

namespace DashboardBrokerWorker
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        //
        private static readonly string AadInstance = CloudConfigurationManager.GetSetting("ida-AADInstance");
        private static readonly string Tenant = CloudConfigurationManager.GetSetting("ida-TenantId");
        private static readonly string PortalResourceId = CloudConfigurationManager.GetSetting("ida-PortalResourceId");
        private static readonly string ClientId = CloudConfigurationManager.GetSetting("ida-DashboardClientId");
        private static readonly string AppKey = CloudConfigurationManager.GetSetting("ida-DashboardAppKey");
        private static readonly IsmIoTSettings.SignalRHelper signalRHelper = new IsmIoTSettings.SignalRHelper(AadInstance, Tenant, PortalResourceId, ClientId, AppKey);

        //
        // connection string for the queues listen rule
        //static string sbQueueConnectionString = "Endpoint=sb://filamentdataevents-ns.servicebus.windows.net/;SharedAccessKeyName=listen;SharedAccessKey=nTwzf8vN3PEglktcEuhqY5/bZLK7MI/Hv8A3uiU9IU4=";
        static string sbQueueConnectionString = CloudConfigurationManager.GetSetting("sbRootManage"); //System.Configuration.ConfigurationSettings.AppSettings.Get("dashboardqueue_receive");
        //QueueClient Client = QueueClient.CreateFromConnectionString(sbQueueConnectionString, "dashboardqueue", ReceiveMode.ReceiveAndDelete);
        QueueClient Client = QueueClient.CreateFromConnectionString(sbQueueConnectionString, CloudConfigurationManager.GetSetting("dashboardqueue_name"), ReceiveMode.ReceiveAndDelete);
        //QueueClient Client = QueueClient.CreateFromConnectionString(sbQueueConnectionString, "dashboardqueue");

        public override void Run()
        {
            Trace.TraceInformation("DashboardBrokerWorker is running");
            try
            {
                this.RunAsync(this.cancellationTokenSource.Token).Wait();
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }
        

        public override bool OnStart()
        {
            // Legen Sie die maximale Anzahl an gleichzeitigen Verbindungen fest.
            ServicePointManager.DefaultConnectionLimit = 12;

            // Informationen zum Behandeln von Konfigurationsänderungen
            // finden Sie im MSDN-Thema unter http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();
            

            Trace.TraceInformation("DashboardBrokerWorker has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("DashboardBrokerWorker is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("DashboardBrokerWorker has stopped");
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

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // Receive Messages from dashboardqueue Queue and send message to SignalR Hub to update dashboards
            OnMessageOptions options = new OnMessageOptions();
            options.AutoComplete = true;
            //options.AutoRenewTimeout = TimeSpan.FromMinutes(1); // Gets or sets the maximum duration within which the lock will be renewed automatically. This value should be greater than the longest message lock duration; for example, the LockDuration Property. 
            options.MaxConcurrentCalls = 1;
            options.ExceptionReceived += OnMessage_ExceptionReceived;
            
            //Stopwatch sw = new Stopwatch();


            Client.OnMessage((message) =>
            {
                switch (message.Label)
                {
                    // PRV: Update the Canvas in Dashboard with Images and Matlab Results
                    case CommandType.PRV:
                        //
                        /*
                        object cp;
                        message.Properties.TryGetValue("capturePeriod", out cp);
                        int capturePeriod = (int)cp;
                        capturePeriod *= 1000;
                        int delay = 0; 
                        */

                        //FilamentData data = message.GetBody<FilamentData>();
                        IsmIoTPortal.Models.FilamentData data = message.GetBody<IsmIoTPortal.Models.FilamentData>();

                        //
                        /*
                        if(sw.IsRunning)
                        {
                            sw.Stop();
                            if((delay = sw.Elapsed.Milliseconds - capturePeriod) < 0)
                            {
                                delay = Math.Abs(delay);
                                Thread.Sleep(delay);
                                sw.Reset();
                            }
                        }
                        */

                        // Get full URI with Shared Access Signature to send to Portal
                        var imgUri = GetBlobSasUri(data.BlobUriImg);
                        var colImgUri = GetBlobSasUri(data.BlobUriColoredImg);

                        signalRHelper.DataDorDashboardTask(data.DeviceId, imgUri, data.FC.ToString(), data.FL.ToString(),
                            colImgUri).ContinueWith(t => { }, cancellationToken);

                        /*sw.Start();*/

                        //message.Complete();
                        break;
                    // UPDATE_DASHBOARD_CONTROLS: Update the Control Elements in Dashboard with the Values from the Device
                    case CommandType.UPDATE_DASHBOARD_CONTROLS:
                        DeviceSettings deviceSettings = message.GetBody<DeviceSettings>();

                        //Sende Values der Controls mit SignalR and Dashboard
                        signalRHelper.ValuesForDashboardControlsTask(deviceSettings.DeviceId, (int)deviceSettings.CapturePeriod,
                            deviceSettings.VarianceThreshold, deviceSettings.DistanceMapThreshold, deviceSettings.RGThreshold,
                            deviceSettings.RestrictedFillingThreshold, deviceSettings.DilateValue).ContinueWith(t => { }, cancellationToken);

                        //message.Complete();
                        break;

                    default:
                        break;
                }
            }, options);

            // TODO: Ersetzen Sie Folgendes durch Ihre eigene Logik.
            while (!cancellationToken.IsCancellationRequested)
            {
                //Trace.TraceInformation("Working");
                await Task.Delay(1000);
            }
        }

        private void OnMessage_ExceptionReceived(object sender, ExceptionReceivedEventArgs e)
        {
            Trace.WriteLine(String.Format("Exception in OnMessage_ExceptionReceived: {0}"), e.Exception.Message);
        }
    }
}
