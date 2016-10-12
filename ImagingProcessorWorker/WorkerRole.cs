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
using System.Net.Sockets;
using IsmIoTPortal;
using Microsoft.Azure.Devices;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.ServiceBus.Messaging;
using Logging;
using System.IO;
using IsmIoTPortal.Models;
using System.Data.Entity;
using IsmIoTSettings;

namespace ImagingProcessorWorker
{
    public class WorkerRole : RoleEntryPoint
    {
        // Update: Nach dem Startup Task keine schreibrechte mehr auf die lokalen Platten
        // --> Deshalb ist das McrInstalled FLag jetzt in der DB und nicht mehr im startuplog.txt
        //static string startuplog = "E:\\approot\\startupLog.txt"; 

        static string mcrlog = "C:\\MCRDownload\\InstallMcr.log";
        //static string mcrlog = string.Format("{0}\\MCRDownload\\InstallMcr.log", System.Environment.GetEnvironmentVariable("LocalRoot").TrimEnd('\\'));
        //static string logfile = RoleEnvironment.CurrentRoleInstance.Id + ".html";

        // *************DEBUG*****************
        static string logfile = "debug.html"; // DEBUG

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        //
        EventProcessorHost eventProcessorHost;
        EventProcessorFactory factory;

        // Role Topology Changes
        private int originalNumOfInstances;
        private int targetNumOfInstances;
        private string highestRoleInstanceId; // This is the Id to delete from db in case of downscale
        private bool lowestRoleInstanceId = false; // The role instance with the lowest id deletes the db entry in case of downscale

        //
        //static string iotHubConnectionString = "HostName=iothubism.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=nhhwSNpr3p68FcTZfvPEfU7xvJRH/jOpTcWQbQMoKAg=";

        public override void Run()
        {
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
            // 
            //Logfile.Get(logfile).ClearLogfile();
            Logfile.Get(logfile).WriteTopic(DateTime.UtcNow.ToString() + "\t" + logfile, 2);
            Logfile.Get(logfile).Update();

            //
            Logfile.Get(logfile).fTextout("{0} > LocalRoot: {1} <br>", DateTime.Now.ToString(), System.Environment.GetEnvironmentVariable("LocalRoot"));
            Logfile.Get(logfile).Update();

            // Role Topology Changes Eventhandler registrieren
            // Tutorial: http://www.codeproject.com/Articles/819175/Understanding-azure-role-topology-changes
            // Artikel: https://azure.microsoft.com/de-de/blog/responding-to-role-topology-changes/
            //
            RoleEnvironment.Changed += RoleEnvironmentChanged;
            RoleEnvironment.Changing += RoleEnvironmentChanging;

            // Legen Sie die maximale Anzahl an gleichzeitigen Verbindungen fest.
            ServicePointManager.DefaultConnectionLimit = 12;

            // Informationen zum Behandeln von Konfigurationsänderungen
            // finden Sie im MSDN-Thema unter http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            return result;
        }

        public async override void OnStop()
        {
            // Unregister EventProcessorHost
            Logfile.Get(logfile).fTextout("{0} > OnStop: Unregister Event Processor <br>", DateTime.Now.ToString());
            Logfile.Get(logfile).Update();
            //eventProcessorHost.UnregisterEventProcessorAsync().Wait();
            await eventProcessorHost.UnregisterEventProcessorAsync();

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();
        }

        /*
            In the Changing event, RoleEnvironment.CurrentRoleInstance.Role.Instances returns the original role instances,
            not the target role instances. There is no way of finding out the target role instances at this time.
        */
        private void RoleEnvironmentChanging(object sender, RoleEnvironmentChangingEventArgs e)
        {
            originalNumOfInstances = RoleEnvironment.CurrentRoleInstance.Role.Instances.Count;
            Logfile.Get(logfile).fTextout(Logging.Fontcolors.RED, "{0} > RoleEnvironmentChanging Eventhandler logging original instance count of {1} <br>", DateTime.Now.ToString(), originalNumOfInstances);
            Logfile.Get(logfile).Update();

            highestRoleInstanceId = RoleEnvironment.CurrentRoleInstance.Role.Instances.ElementAt(originalNumOfInstances - 1).Id;

            // The role instance with the lowest id deletes the db entry in case of downscale.
            if ("ImagingProcessorWorker_IN_0" == RoleEnvironment.CurrentRoleInstance.Id)
            {
                Logfile.Get(logfile).fTextout(Logging.Fontcolors.RED, "{0} > This ({1}) is the role instance with the lowest RoleInstanceId <br>", DateTime.Now.ToString(), RoleEnvironment.CurrentRoleInstance.Id);
                Logfile.Get(logfile).Update();

                lowestRoleInstanceId = true;
            }
            else
            {
                Logfile.Get(logfile).fTextout(Logging.Fontcolors.RED, "{0} > This ({1}) is NOT the role instance with the lowest RoleInstanceId <br>", DateTime.Now.ToString(), RoleEnvironment.CurrentRoleInstance.Id);
                Logfile.Get(logfile).Update();
            }
        }

        /*
            In the Changed event, RoleEnvironment.CurrentRoleInstance.Role.Instances returns the target role instances, 
            not the original role instances. If you need to know about the original instances, 
            you can save this information when the Changing event fires and access it from the Changed event 
            (since these events are always fired in sequence).
        */
        private void RoleEnvironmentChanged(object sender, RoleEnvironmentChangedEventArgs e)
        {
            targetNumOfInstances = RoleEnvironment.CurrentRoleInstance.Role.Instances.Count;
            Logfile.Get(logfile).fTextout(Logging.Fontcolors.RED, "{0} > RoleEnvironmentChanged Eventhandler logging target instance count of {1} <br>", DateTime.Now.ToString(), targetNumOfInstances);
            Logfile.Get(logfile).Update();

            // The change is a downscale! Delete Mcr flag entry from db
            if (originalNumOfInstances > targetNumOfInstances)
            {
                using (IsmIoTPortalContext db = new IsmIoTPortalContext())
                {
                    try
                    {
                        // The highest role instance id was determined in the changing event
                        ImagingProcessorWorkerInstance entry = null;
                        var queryResults = db.ImagingProcessorWorkerInstances.Where(en => en.RoleInstanceId == highestRoleInstanceId);
                        if (queryResults.Count() > 0)
                            entry = queryResults.First();
                     
                        if (lowestRoleInstanceId) // The role instance with the lowest id deletes the db entry in case of downscale.
                        {
                            Logfile.Get(logfile).fTextout(Logging.Fontcolors.RED, "{0} > Delete database entry with key {1} <br>", DateTime.Now.ToString(), highestRoleInstanceId);
                            Logfile.Get(logfile).Update();

                            // Delete entry from DB
                            db.ImagingProcessorWorkerInstances.Remove(entry);
                            db.SaveChanges();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logfile.Get(logfile).fTextout(Logging.Fontcolors.RED, "{0} > Failed to delete McrInstalled flag DB entry. <br>", DateTime.Now.ToString());
                        Logfile.Get(logfile).fTextout(Logging.Fontcolors.RED, "{0} > Exception: {1} <br>", DateTime.Now.ToString(), ex.Message);
                        Logfile.Get(logfile).Update();
                        throw;
                    }
                }
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // 1. Check if this is after restart and "MCR is installed" already
            //
            bool mcrAlreadyInstalled = false;
            string line = "";
            try
            {
                using (IsmIoTPortalContext db = new IsmIoTPortalContext())
                {
                    ImagingProcessorWorkerInstance entry = null;
                    var queryResults = db.ImagingProcessorWorkerInstances.Where(e => e.RoleInstanceId == RoleEnvironment.CurrentRoleInstance.Id);
                    if (queryResults.Count() > 0)
                        entry = queryResults.First(); // Es gibt natürlich nur ein DB Eintrag mit dem Schlüssel RoleInstanceId
                    if (entry == null) // First start of this Instance -> Create and add new DB entry
                    {
                        //
                        entry = new ImagingProcessorWorkerInstance();
                        entry.RoleInstanceId = RoleEnvironment.CurrentRoleInstance.Id;
                        entry.McrInstalled = false;
                        entry.Timestamp = DateTime.UtcNow;
                        db.ImagingProcessorWorkerInstances.Add(entry);
                        db.SaveChanges();
                        // mcrAlreadyInstalled is still false
                    }
                    else
                    {
                        if (entry.McrInstalled == true)
                        {
                            mcrAlreadyInstalled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logfile.Get(logfile).fTextout(Logging.Fontcolors.RED, "{0} > Failed to use McrInstalled flag. <br>", DateTime.Now.ToString());
                Logfile.Get(logfile).fTextout(Logging.Fontcolors.RED, "{0} > Exception: {1} <br>", DateTime.Now.ToString(), ex.Message);
                Logfile.Get(logfile).Update();
                mcrAlreadyInstalled = false;
            }

            // *************DEBUG*****************
            mcrAlreadyInstalled = true; // DEBUG


            // 2. Poll the MCR Installation Log if mcrAlreadyInstalled == false
            //
            Stopwatch stopWatch = new Stopwatch();
            bool timeout = false;
            stopWatch.Start();
            while (!mcrAlreadyInstalled && true)
            {
                try
                {
                    // Timeout
                    if (stopWatch.Elapsed > TimeSpan.FromMinutes(30))
                    {
                        timeout = true;
                        break;
                    }

                    // Sleep
                    await Task.Delay(TimeSpan.FromMinutes(5));

                    if (!File.Exists(mcrlog))
                        continue;

                    line = File.ReadLines(mcrlog).Last();
                    if (!line.Contains("End - Successful"))
                        continue;
                    else
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    // Probably file reading errors because the MCR installer uses the file
                    continue;
                }
            }


            //
            // MCR is installed now -->Set the flag in DB and Reboot
            if (!mcrAlreadyInstalled && timeout == false)
            {
                try
                {
                    using (IsmIoTPortalContext db = new IsmIoTPortalContext())
                    {
                        // Update DB entry and set the McrInstalled Flag to true
                        var entry = db.ImagingProcessorWorkerInstances.Where(e => e.RoleInstanceId == RoleEnvironment.CurrentRoleInstance.Id).First(); // // Es gibt natürlich nur ein DB Eintrag mit dem Schlüssel RoleInstanceId
                        db.Entry(entry).Entity.McrInstalled = true;
                        db.Entry(entry).Entity.Timestamp = DateTime.UtcNow;
                        db.Entry(entry).State = EntityState.Modified;
                        db.SaveChanges();

                        // Restart Worker Role
                        // Update: Der User nach dem Startup Task darf wohl kein shuttdown ausführen --> benutze schtasks
                        //string stat = ExecuteCommandSync("shutdown /R /F");

                        // Mit schtasks
                        DateTime executionTime = DateTime.Now.Add(new TimeSpan(0, 1, 0));
                        string date = string.Format("{0}/{1}/{2}", executionTime.Month.ToString("d2"), executionTime.Day.ToString("d2"), executionTime.Year);
                        string time = string.Format("{0}:{1}", executionTime.Hour.ToString("d2"), executionTime.Minute.ToString("d2"));
                        string cmd = string.Format("schtasks /CREATE /TN RebootRoleInstance /SC ONCE /SD {0} /ST {1} /RL HIGHEST /RU scheduser /RP Qwer123 /TR \"shutdown /R /F\" /F", date, time);
                        string stat = ExecuteCommandSync(cmd);
                        Logfile.Get(logfile).fTextout(Logging.Fontcolors.RED, "{0} > {1} <br>", DateTime.Now.ToString(), stat);
                        Logfile.Get(logfile).Update();
                    }
                }
                catch (Exception ex)
                {
                    Logfile.Get(logfile).fTextout(Logging.Fontcolors.RED, "{0} > Failed writing to update McrInstalled flag and reboot. <br>", DateTime.Now.ToString());
                    Logfile.Get(logfile).fTextout(Logging.Fontcolors.RED, "{0} > Exception: {1} <br>", DateTime.Now.ToString(), ex.Message);
                    Logfile.Get(logfile).Update();
                }

            }
            // Timeout --> Write to log and do Not start Event Processor
            if (!mcrAlreadyInstalled && timeout == true)
            {
                Logfile.Get(logfile).fTextout(Logging.Fontcolors.RED, "{0} > Timeout occured during MCR Installation Polling. <br>", DateTime.Now.ToString());
                Logfile.Get(logfile).Update();
            }
            // Reboot and MCR is already installed --> Start Event Processor
            if (mcrAlreadyInstalled)
            {
                //
                // Create EventProcessorHost
                /*the iotHubD2cEndpoint in the example is the name of the device-to - cloud endpoint on your IoT hub.
                Technically, and IoT hub could have multiple endpoints that are compatible with Event Hubs.
                At this time the only Event Hubs - compatible endpoint is called "messages/events", so it is a fixed string. */
                string iotHubD2cEndpoint = "messages/events";
                string eventHubName = Settings.iotHubName; //"IsmIoTHub";
                string consumerGroupName = EventHubConsumerGroup.DefaultGroupName;
                // Alternativ geht auch CurrentRoleInstance.Id (ist auch unique)
                //string eventProcessorHostName = Guid.NewGuid().ToString();

                string eventProcessorHostName = RoleEnvironment.CurrentRoleInstance.Id;
                // leaseContainerName-Parameter wie im scaled out event processing beispiel:
                // here it's using eventhub as lease name. but it can be specified as any you want.
                // if the host is having same lease name, it will be shared between hosts.
                // by default it is using eventhub name as lease name.
                eventProcessorHost = new EventProcessorHost(eventProcessorHostName,
                                                            iotHubD2cEndpoint, // eigentlich steht hier der EventHub Name aber siehe Kommentar, bei IoTHubs ist hier der fixe string "messages/events" notwendig
                                                            consumerGroupName,
                                                            //iotHubConnectionString,
                                                            Settings.ismiothub,//System.Configuration.ConfigurationSettings.AppSettings.Get("ismiothub"),
                                                            //EventProcessor.StorageConnectionString, 
                                                            Settings.ismiotstorage,//System.Configuration.ConfigurationSettings.AppSettings.Get("ismiotstorage"),
                                                            eventHubName.ToLowerInvariant());

                factory = new EventProcessorFactory(logfile);
                await eventProcessorHost.RegisterEventProcessorFactoryAsync(factory);

                // Register EventProcessorHost
                Logfile.Get(logfile).fTextout("{0} > Registering EventProcessor... <br>", DateTime.Now.ToString());
                Logfile.Get(logfile).Update();

                // Ansatz ohne Factory. Hatte jedoch den Nachteil, dass man keine Referenz auf das EventProcessor Objekt
                // hat und somit z.B. keine Eventhandler registrieren konnte. Parameter an Konstruktor gigen ja auch nicht,
                // weil dieser niemals sichtbar aufgerufen wird bei dem Ansatz
                //eventProcessorHost.RegisterEventProcessorAsync<EventProcessor>().Wait();
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
            }
        }

        // http://www.codeproject.com/Articles/25983/How-to-Execute-a-Command-in-C
        /// <span class="code-SummaryComment"><summary></span>
        /// Executes a shell command synchronously.
        /// <span class="code-SummaryComment"></summary></span>
        /// <span class="code-SummaryComment"><param name="command">string command</param></span>
        /// <span class="code-SummaryComment"><returns>string, as output of the command.</returns></span>
        public static string ExecuteCommandSync(object command)
        {
            try
            {
                // create the ProcessStartInfo using "cmd" as the program to be run,
                // and "/c " as the parameters.
                // Incidentally, /c tells cmd that we want it to execute the command that follows,
                // and then exit.
                System.Diagnostics.ProcessStartInfo procStartInfo =
                    new System.Diagnostics.ProcessStartInfo("cmd", "/c " + command);

                // The following commands are needed to redirect the standard output.
                // This means that it will be redirected to the Process.StandardOutput StreamReader.
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                // Do not create the black window.
                procStartInfo.CreateNoWindow = true;
                // Now we create a process, assign its ProcessStartInfo and start it
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;
                proc.Start();
                // Get the output into a string
                string result = proc.StandardOutput.ReadToEnd();
                // Display the command output.
                Console.WriteLine(result);
                return result;
            }
            catch (Exception objException)
            {
                // Log the exception
                return objException.Message;
            }
        }
    }
}
