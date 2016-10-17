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
        HubConnection signalRHubConnection = null;
        IHubProxy signalRHubProxy = null;

        //
        // connection string for the queues listen rule
        //static string sbQueueConnectionString = "Endpoint=sb://filamentdataevents-ns.servicebus.windows.net/;SharedAccessKeyName=listen;SharedAccessKey=nTwzf8vN3PEglktcEuhqY5/bZLK7MI/Hv8A3uiU9IU4=";
        static string sbQueueConnectionString = Settings.sbRootManage;//System.Configuration.ConfigurationSettings.AppSettings.Get("dashboardqueue_receive");
        //QueueClient Client = QueueClient.CreateFromConnectionString(sbQueueConnectionString, "dashboardqueue", ReceiveMode.ReceiveAndDelete);
        QueueClient Client = QueueClient.CreateFromConnectionString(sbQueueConnectionString, Settings.dashboardqueue_name, ReceiveMode.ReceiveAndDelete);
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

        #region Authentication

        //
        // The Client ID is used by the application to uniquely identify itself to Azure AD.
        // The App Key is a credential used by the application to authenticate to Azure AD.
        // The Tenant is the name of the Azure AD tenant in which this application is registered.
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Authority is the sign-in URL of the tenant.
        //
        private  string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        private  string tenant = ConfigurationManager.AppSettings["ida:TenantId"];
        private  string clientId = ConfigurationManager.AppSettings["ida:DashboardClientId"];
        private  string appKey = ConfigurationManager.AppSettings["ida:DashboardAppKey"];
        private string authority = "";
        //
        // To authenticate for SignalR, the client needs to know the service's App ID URI.
        //
        private  string portalResourceId = ConfigurationManager.AppSettings["ida:PortalResourceId"];
        
        private  AuthenticationContext authContext = null;
        private  ClientCredential clientCredential = null;
        private AuthenticationResult authResult = null;

        /// <summary>
        /// This functions tries to authenticate the WorkerRole on the IoT Portal. That way it can access SignalR
        /// </summary>
        /// <returns>True for success, false for failure</returns>
        private async Task<bool> Authenticate()
        {
            //
            // Get an access token from Azure AD using client credentials.
            // If the attempt to get a token fails because the server is unavailable, retry twice after 3 seconds each.
            //
            AuthenticationResult result = null;
            var retryCount = 0;
            var retry = false;

            do
            {
                Trace.TraceInformation("Authenticate. Try number {0}\n", retryCount+1);
                retry = false;
                try
                {
                    // ADAL includes an in memory cache, so this call will only send a message to the server if the cached token is expired.
                    result = await authContext.AcquireTokenAsync(this.portalResourceId, this.clientCredential);
                }
                catch (AdalException ex)
                {
                    if (ex.ErrorCode == "temporarily_unavailable")
                    {
                        retry = true;
                        retryCount++;
                        Thread.Sleep(3000);
                    }

                    Trace.TraceInformation(
                        String.Format("An error occurred while acquiring a token\nTime: {0}\nError: {1}\nRetry: {2}\n",
                            DateTime.Now.ToString(),
                            ex.ToString(),
                            retry.ToString()));
                }
                catch (Exception ex)
                {
                    Trace.TraceInformation("Error during authentication: {0}\n", ex.Message);
                }

            } while (retry && (retryCount < 3));

            if (result == null)
            {
                Trace.TraceInformation("Authentication was unsuccessful.\n");
                return false;
            }
            authResult = result;
            Trace.TraceInformation("Authentication was successful.\n");
            return true;
        }

        #endregion

        public override bool OnStart()
        {
            // Legen Sie die maximale Anzahl an gleichzeitigen Verbindungen fest.
            ServicePointManager.DefaultConnectionLimit = 12;
            
            // Authenticate 
            authority = aadInstance + tenant;
            authContext = new AuthenticationContext(authority);
            clientCredential = new ClientCredential(clientId, appKey);
            Authenticate().Wait();

            // Informationen zum Behandeln von Konfigurationsänderungen
            // finden Sie im MSDN-Thema unter http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            //
            InitializeSignalR();

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
            var storageAccount = CloudStorageAccount.Parse(IsmIoTSettings.Settings.storageConnection);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var blobContainer = blobClient.GetContainerReference(IsmIoTSettings.Settings.containerPortal);
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

                        // Sende Daten an Dashboards
                        signalRHubProxy.Invoke<string>("DataForDashboard", data.DeviceId, imgUri, data.FC.ToString(), data.FL.ToString(), colImgUri).ContinueWith(t =>
                        {
                            //Console.WriteLine(t.Result);
                        });

                        /*sw.Start();*/

                        //message.Complete();
                        break;
                    // UPDATE_DASHBOARD_CONTROLS: Update the Control Elements in Dashboard with the Values from the Device
                    case CommandType.UPDATE_DASHBOARD_CONTROLS:
                        DeviceState deviceState = message.GetBody<DeviceState>();

                        //Sende Values der Controls mit SignalR and Dashboard
                        signalRHubProxy.Invoke("ValuesForDashboardControls", deviceState.DeviceId, deviceState.CapturePeriod,
                            deviceState.VarianceThreshold, deviceState.DistanceMapThreshold, deviceState.RGThreshold,
                            deviceState.RestrictedFillingThreshold, deviceState.DilateValue).ContinueWith(t =>
                            {
                                //Console.WriteLine(t.Result);
                            });

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

        private void InitializeSignalR()
        {
            // SignalR 
            //
            if (signalRHubConnection == null)
            {
                // local
                //signalRHubConnection = new HubConnection("http://localhost:39860/");
                // cloud
                // TODO: No hardcoded domain
                signalRHubConnection = new HubConnection(Settings.webCompleteAddress);
                signalRHubConnection.Headers.Add("Authorization", "Bearer " + authResult.AccessToken);
                signalRHubConnection.Headers.Add("Bearer", authResult.AccessToken);
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
    }
}
