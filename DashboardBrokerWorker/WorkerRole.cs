using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Text;
using System.Runtime.Serialization;
using IsmIoTPortal;
using IsmIoTPortal.Models;
using IsmIoTSettings;

namespace DashboardBrokerWorker
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        //
        static HubConnection signalRHubConnection = null;
        static IHubProxy signalRHubProxy = null;

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

        public override bool OnStart()
        {
            // Legen Sie die maximale Anzahl an gleichzeitigen Verbindungen fest.
            ServicePointManager.DefaultConnectionLimit = 12;

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

                        // Sende Daten an Dashboards
                        signalRHubProxy.Invoke<string>("DataForDashboard", data.DeviceId, data.BlobUriImg, data.FC.ToString(), data.FL.ToString(), data.BlobUriColoredImg).ContinueWith(t =>
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

        private static void InitializeSignalR()
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
