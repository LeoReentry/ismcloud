using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Azure.Devices;
using Microsoft.AspNet.SignalR.Client;
using IsmIoTPortal.Models;
using IsmIoTPortal;
using System.Data.Entity;
using IsmIoTSettings;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace FeedbackReceiverWorker
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        //static string connectionString = "HostName=iothubism.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=nhhwSNpr3p68FcTZfvPEfU7xvJRH/jOpTcWQbQMoKAg=";
        static string connectionString = Settings.ismiothub;//System.Configuration.ConfigurationSettings.AppSettings.Get("ismiothub");
        static ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(connectionString);

        static FeedbackReceiver<FeedbackBatch> feedbackReceiver = null;

        // local
        static HubConnection signalRHubConnection = null;
        // cloud
        //static HubConnection signalRHubConnection = null;

        static IHubProxy signalRHubProxy = null;

        #region Authentication

        //
        // The Client ID is used by the application to uniquely identify itself to Azure AD.
        // The App Key is a credential used by the application to authenticate to Azure AD.
        // The Tenant is the name of the Azure AD tenant in which this application is registered.
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Authority is the sign-in URL of the tenant.
        //
        private readonly string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        private readonly string tenant = ConfigurationManager.AppSettings["ida:TenantId"];
        private readonly string clientId = ConfigurationManager.AppSettings["ida:FeedbackClientId"];
        private readonly string appKey = ConfigurationManager.AppSettings["ida:FeedbackAppKey"];
        private string authority = "";
        //
        // To authenticate for SignalR, the client needs to know the service's App ID URI.
        //
        private string portalResourceId = ConfigurationManager.AppSettings["ida:PortalResourceId"];

        private AuthenticationContext authContext = null;
        private ClientCredential clientCredential = null;
        private static AuthenticationResult authResult = null;

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
                Trace.TraceInformation("Authenticate. Try number {0}\n", retryCount + 1);
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
            // If authentication was successful, save the authentication result to private field, so it can be read later
            authResult = result;
            Trace.TraceInformation("Authentication was successful.\n");
            return true;
        }

        #endregion

        private static void Initialize()
        {
            // Nur einmal für die Webseiten dieser Web App Instanz einen FeedbackReceiver anlegen und ReceiveFeedbackAsync aufrufen  (wird erkannt, wenn er noch null ist)
            if (feedbackReceiver == null)
            {
                // Start receiving feedbacks
                feedbackReceiver = serviceClient.GetFeedbackReceiver();
                ReceiveFeedbackAsync();
            }
            // 
            if (signalRHubConnection == null)
            {
                // local
                //signalRHubConnection = new HubConnection("http://localhost:39860/");
                // cloud
                // TODO: No hardcoded domain
                signalRHubConnection = new HubConnection(Settings.webCompleteAddress);
                // Add authentication token to headers
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
                        //Console.WriteLine("Verbindung aufgebaut!");
                    }
                }).Wait();
            }
        }

        private async static void ReceiveFeedbackAsync()
        {
            var db = new IsmIoTPortalContext();

            while (true)
            {
                var feedbackBatch = await feedbackReceiver.ReceiveAsync();
                if (feedbackBatch == null) continue;

                //
                foreach (FeedbackRecord record in feedbackBatch.Records)
                {
                    switch (record.OriginalMessageId.Substring(0, 3)) // Substring(0, 3) is the MessageIdPrefix
                    {
                        // CMD
                        case MessageIdPrefix.CMD:
                            await Task.Factory.StartNew(() => ProcessCmdMessage(record));
                            break;

                        default:
                            break;
                    }
                }

                await feedbackReceiver.CompleteAsync(feedbackBatch);
            }
        }

        private static async Task ProcessCmdMessage(FeedbackRecord record)
        {
            using (var db = new IsmIoTPortalContext())
            {
                // Achtung .Substring(4), weil die ersten 3 Zeichen das Präfix "CMD" sind 
                int CommandId = Convert.ToInt32(record.OriginalMessageId.Substring(4)); // Beim Senden des Commands wurde der Schlüssel des DB Eintrages als MessageId angegeben
                var entry = db.Commands.Where(c => c.CommandId == CommandId).First(); // Es gibt natürlich nur ein DB Eintrag mit dem Schlüssel CommandId

                if (record.StatusCode == FeedbackStatusCode.Success)
                {
                    db.Entry(entry).Entity.CommandStatus = CommandStatus.SUCCESS;
                }
                else // Rejected,...
                {
                    db.Entry(entry).Entity.CommandStatus = CommandStatus.FAILURE;
                }

                db.Entry(entry).State = EntityState.Modified;
                db.SaveChanges();

                await signalRHubProxy.Invoke<string>("IsmDevicesIndexChanged").ContinueWith(t =>
                {
                    //Console.WriteLine(t.Result);
                });
            }
        }

        public override void Run()
        {
            Trace.TraceInformation("FeedbackReceiverWorker is running");

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

            // Authentication
            authority = aadInstance + tenant;
            authContext = new AuthenticationContext(authority);
            clientCredential = new ClientCredential(clientId, appKey);
            Authenticate().Wait();

            // Informationen zum Behandeln von Konfigurationsänderungen
            // finden Sie im MSDN-Thema unter http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            Trace.TraceInformation("FeedbackReceiverWorker has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("FeedbackReceiverWorker is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("FeedbackReceiverWorker has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Ersetzen Sie Folgendes durch Ihre eigene Logik.

            // Initialisiere static Komponenten wie SignalR Hub connection + proxy, sowie Feedback Receiver
            // and start receiving feedbacks
            Initialize();

            while (!cancellationToken.IsCancellationRequested)
            {
                //Trace.TraceInformation("Working");
                await Task.Delay(1000);
            }
        }
    }
}
