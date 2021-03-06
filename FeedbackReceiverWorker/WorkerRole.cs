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
        private static string connectionString = CloudConfigurationManager.GetSetting("ismiothub");//System.Configuration.ConfigurationSettings.AppSettings.Get("ismiothub");
        static ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(connectionString);

        static FeedbackReceiver<FeedbackBatch> feedbackReceiver = null;

        private static readonly string AadInstance = CloudConfigurationManager.GetSetting("ida-AADInstance");
        private static readonly string Tenant = CloudConfigurationManager.GetSetting("ida-TenantId");
        private static readonly string PortalResourceId = CloudConfigurationManager.GetSetting("ida-PortalResourceId");
        private static readonly string ClientId = CloudConfigurationManager.GetSetting("ida-FeedbackClientId");
        private static readonly string AppKey = CloudConfigurationManager.GetSetting("ida-FeedbackAppKey");
        private static readonly IsmIoTSettings.SignalRHelper signalRHelper = new IsmIoTSettings.SignalRHelper(AadInstance, Tenant, PortalResourceId, ClientId, AppKey);

        private static void Initialize()
        {
            // Nur einmal für die Webseiten dieser Web App Instanz einen FeedbackReceiver anlegen und ReceiveFeedbackAsync aufrufen  (wird erkannt, wenn er noch null ist)
            if (feedbackReceiver == null)
            {
                // Start receiving feedbacks
                feedbackReceiver = serviceClient.GetFeedbackReceiver();
                ReceiveFeedbackAsync();
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
                    // https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messaging#cloud-to-device-messages
                    // Message is always success, because MQTT clients can't reject C2D Messages
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

                await signalRHelper.IsmDevicesIndexChangedTask();
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
