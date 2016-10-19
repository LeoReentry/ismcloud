using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using IsmIoTPortal.Models;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common.Exceptions;
using Microsoft.AspNet.SignalR.Client;
using Newtonsoft.Json;
using System.Text;
using System.Threading;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace IsmIoTPortal.Controllers
{
    [Authorize]
    public class IsmDevicesController : Controller
    {
        private IsmIoTPortalContext db = new IsmIoTPortalContext();

        //static string connectionString = "HostName=iothubism.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=nhhwSNpr3p68FcTZfvPEfU7xvJRH/jOpTcWQbQMoKAg=";
        //static RegistryManager registryManager = RegistryManager.CreateFromConnectionString(connectionString);
        //static ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
  
        static RegistryManager registryManager = RegistryManager.CreateFromConnectionString(IsmIoTSettings.Settings.ismiothub);
        static ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(IsmIoTSettings.Settings.ismiothub);

        static AuthenticationHelper signalRHelper = new AuthenticationHelper();


        private async static Task SendCloudToDevicePortalCommandAsync(int CommandId, string DeviceId, string cmd)
        {
            //var commandMessage = new Message();
            //var commandMessage = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(cmd)));
            var commandMessage = new Message(Encoding.UTF8.GetBytes(cmd));

            /*Anmerkung: 
              bei Clients mit TransportType.Http1 darf kein Ack verwendet werden 
              und die Properties zu setzen bringt nichts, weil diese leer ankommen.
              Todo: Lösung finden, mit dem Amqp und Http Devices gleichzeitig zurechtkommen
                    --> Infos von Properties auch in Message String stecken 
                    --> Nachricht einmal mit Ack Flag und einmal ohne versenden?
                        --> Amqp Device muss dann die Nachricht ohne Ack Flag anhand von Property vielleicht erkennen und ignorieren
                        --> Http Device empfängt die Nachrichtmit Ack Flag erst garnicht soweit Beobachtet?
            */

            commandMessage.Properties["command"] = cmd;
            commandMessage.Ack = DeliveryAcknowledgement.Full;
            commandMessage.MessageId = MessageIdPrefix.CMD + " " + CommandId.ToString();
            await serviceClient.SendAsync(DeviceId, commandMessage); 
        }

        //
        // Device State Anfordern
        private async static Task C2DGetDeviceStateAsync(string DeviceId)
        {
            //var commandMessage = new Message();
            var commandMessage = new Message(Encoding.UTF8.GetBytes(CommandType.GET_DEVICE_STATE));
            commandMessage.Properties["command"] = CommandType.GET_DEVICE_STATE;
            /* Die Reaktion des Devices auf GET_DEVICE_STATE, soll das Senden des gesamten Device-States
               sein (Algorithm-Parameter, Cam & Pulser-Parameter, aktueller state (running, preview,...) usw.)
               --> Diese vielen Infos alle in die MessageId zu "tricksen" ist nicht schön, desshalb wird hier ohne ACK's gearbeitet.
               Das Device sendet als Reaktion eine D2C UPDATE_DASHBOARD_CONTROLS message die vom DashboardBroker entsprechend
               mit dem update der Controls auf dem Dashboard per SignalR behandelt werden kann.
            */
            //commandMessage.Ack = DeliveryAcknowledgement.Full;
            commandMessage.MessageId = Guid.NewGuid().ToString();
            await serviceClient.SendAsync(DeviceId, commandMessage);
        }

        // Device State Setzen
        private async static Task C2DSetDeviceStateAsync(string DeviceId, DeviceState deviceState)
        {
            // Durch View veränderter DeviceState in Message Body packen
            string serializedDeviceState = JsonConvert.SerializeObject(deviceState);
            var commandMessage = new Message(Encoding.ASCII.GetBytes(serializedDeviceState));

            commandMessage.Properties["command"] = CommandType.SET_DEVICE_STATE;

            //commandMessage.Ack = DeliveryAcknowledgement.Full;
            commandMessage.MessageId = Guid.NewGuid().ToString();
            await serviceClient.SendAsync(DeviceId, commandMessage);
        }


        // GET: IsmDevices
        public async Task<ActionResult> Index()
        {

            var ismDevices = db.IsmDevices.Include(i => i.Hardware).Include(i => i.Location).Include(i => i.Software);
            
            // Get the whole Info from Identity Registry via a Device object for each DeviceId in DB 
            Dictionary<string, Device> devices = new Dictionary<string, Device>();
            foreach (IsmDevice d in ismDevices)
            {
                Device device = await registryManager.GetDeviceAsync(d.DeviceId);
                devices.Add(d.DeviceId, device);
            }
            ViewBag.DeviceList = devices;

            return View(ismDevices.ToList());
        }

        // GET: IsmDevices/Dashboard/<DeviceId>
        public async Task<ActionResult> Dashboard(string DeviceId)
        {
            DeviceState deviceState = new DeviceState();
            deviceState.DeviceId = DeviceId;

            //await C2DGetDeviceStateAsync(DeviceId);
            return View(deviceState);
        }

        // POST: IsmDevices/Dashboard/<DeviceState>
        [HttpPost]
        public async Task<ActionResult> Dashboard(DeviceState deviceState)
        {
            // C2D Message die dem Device einen durch die Controls veränderten DeviceState mitteilt
            await C2DSetDeviceStateAsync(deviceState.DeviceId, deviceState);
            //ModelState.Clear();
            return View(deviceState);
        }


        public async Task<ActionResult> ShowKey(string deviceId)
        {
            Device device = await registryManager.GetDeviceAsync(deviceId);
            string key = device.Authentication.SymmetricKey.PrimaryKey.ToString();
            return View(model:key);
        }

        private async static Task<string> AddDeviceAsync(string deviceId)
        {
            Device device = await registryManager.AddDeviceAsync(new Device(deviceId));
            return device.Authentication.SymmetricKey.PrimaryKey.ToString();
        }


        // GET: IsmDevices/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            IsmDevice ismDevice = db.IsmDevices.Find(id);
            if (ismDevice == null)
            {
                return HttpNotFound();
            }
            return View(ismDevice);
        }

        // GET: IsmDevices/Create
        public ActionResult Create()
        {
            ViewBag.HardwareId = new SelectList(db.Hardware, "HardwareId", "Board");
            ViewBag.LocationId = new SelectList(db.Locations, "LocationId", "Country");
            ViewBag.SoftwareId = new SelectList(db.Software, "SoftwareId", "SoftwareVersion");
            return View();
        }

        // POST: IsmDevices/Create
        // Aktivieren Sie zum Schutz vor übermäßigem Senden von Angriffen die spezifischen Eigenschaften, mit denen eine Bindung erfolgen soll. Weitere Informationen 
        // finden Sie unter http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "IsmDeviceId,DeviceId,LocationId,SoftwareId,HardwareId")] IsmDevice ismDevice)
        {
            if (ModelState.IsValid)
            {
                // Zuerst einmal in DB anlegen
                // Es darf nicht passieren, dass man einen eintrag in der Identity Registry anlegt
                // und diesen nicht in der DB registriert (= Device Leak)
                db.IsmDevices.Add(ismDevice);
                db.SaveChanges();

                try
                {
                    await AddDeviceAsync(ismDevice.DeviceId);
                }
                catch (Exception)
                {
                    // Anzeigen, dass etwas schief ging
                    // Eintrag aus DB entfernen
                }

                /*
                // Create Dashboard Queue for the new Device
                // http://stackoverflow.com/questions/30749945/create-azure-service-bus-queue-shared-access-policy-programmatically
                // http://www.cloudcasts.net/devguide/Default.aspx?id=12018
                //
                // Create a token provider with the relevant credentials.
                TokenProvider credentials = TokenProvider.CreateSharedAccessSignatureTokenProvider(IsmIoTSettings.Settings.sbRootSasName, IsmIoTSettings.Settings.sbRootSasKey);
                // Create a URI for the service bus.
                Uri serviceBusUri = ServiceBusEnvironment.CreateServiceUri("sb", IsmIoTSettings.Settings.sbNamespace, string.Empty);
                // Create a NamespaceManager for the specified namespace using the specified credentials.
                NamespaceManager namespaceManager = new NamespaceManager(serviceBusUri, credentials);
                string queueName = ismDevice.DeviceId; // Queue name equals DeviceId
                QueueDescription queueDescription = await namespaceManager.CreateQueueAsync(queueName);
                */

                return RedirectToAction("Index");
            }

            ViewBag.HardwareId = new SelectList(db.Hardware, "HardwareId", "Board", ismDevice.HardwareId);
            ViewBag.LocationId = new SelectList(db.Locations, "LocationId", "Country", ismDevice.LocationId);
            ViewBag.SoftwareId = new SelectList(db.Software, "SoftwareId", "SoftwareVersion", ismDevice.SoftwareId);
            return View(ismDevice);
        }


        // GET: IsmDevices/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            IsmDevice ismDevice = db.IsmDevices.Find(id);
            if (ismDevice == null)
            {
                return HttpNotFound();
            }
            ViewBag.HardwareId = new SelectList(db.Hardware, "HardwareId", "Board", ismDevice.HardwareId);
            ViewBag.LocationId = new SelectList(db.Locations, "LocationId", "Country", ismDevice.LocationId);
            ViewBag.SoftwareId = new SelectList(db.Software, "SoftwareId", "SoftwareVersion", ismDevice.SoftwareId);
            return View(ismDevice);
        }

        // POST: IsmDevices/Edit/5
        // Aktivieren Sie zum Schutz vor übermäßigem Senden von Angriffen die spezifischen Eigenschaften, mit denen eine Bindung erfolgen soll. Weitere Informationen 
        // finden Sie unter http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "IsmDeviceId,DeviceId,LocationId,SoftwareId,HardwareId")] IsmDevice ismDevice)
        {
            if (ModelState.IsValid)
            {
                db.Entry(ismDevice).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.HardwareId = new SelectList(db.Hardware, "HardwareId", "Board", ismDevice.HardwareId);
            ViewBag.LocationId = new SelectList(db.Locations, "LocationId", "Country", ismDevice.LocationId);
            ViewBag.SoftwareId = new SelectList(db.Software, "SoftwareId", "SoftwareVersion", ismDevice.SoftwareId);
            return View(ismDevice);
        }

        // GET: IsmDevices/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            IsmDevice ismDevice = db.IsmDevices.Find(id);
            if (ismDevice == null)
            {
                return HttpNotFound();
            }
            return View(ismDevice);
        }
        /* Abgelöst von eleganter Lösung mit Continuations
        // POST: IsmDevices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            // Zuerst aus Identity Registry löschen, dann aus DB
            // Nur wenn erster Schritt gut ging weitermachen und aus DB löschen
            // Vermeidet Device Leak
            IsmDevice ismDevice = db.IsmDevices.Find(id);
            Task t = registryManager.RemoveDeviceAsync(ismDevice.DeviceId);
                  
            try
            {
                await t;
            }
            catch(Exception ex)
            {

            }

            switch (t.Status)
            {
                case TaskStatus.RanToCompletion:
                    // completed to remove from identity registry, so it's save to remove it from db too
                    db.IsmDevices.Remove(ismDevice);
                    db.SaveChanges();
                    return RedirectToAction("Index");
                case TaskStatus.Faulted:
                    return RedirectToAction("Index");
                default:
                    return RedirectToAction("Index");
            }
        }*/

        // Elegant mit Continuations gelöst
        // 
        // POST: IsmDevices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            // Zuerst aus Identity Registry löschen, dann aus DB
            // Nur wenn erster Schritt gut ging weitermachen und aus DB löschen
            // Vermeidet Device Leak
            IsmDevice ismDevice = db.IsmDevices.Find(id);

            Task deleteFromIoTHubTask =
                Task.Factory.StartNew((stateObj) =>
                {
                    //registryManager.RemoveDeviceAsync(db.IsmDevices.Find(id).DeviceId);
                    registryManager.RemoveDeviceAsync(((IsmDevice)stateObj).DeviceId);
                }, ismDevice);

            var cts = new CancellationTokenSource();

            return await deleteFromIoTHubTask
            .ContinueWith<ActionResult>((ant) =>
            {
                db.IsmDevices.Remove(ismDevice);
                db.SaveChanges();
                cts.Cancel();
                return RedirectToAction("Index");
            }, TaskContinuationOptions.OnlyOnRanToCompletion)
            .ContinueWith((ant, ct) =>
            {
            }, cts.Token)
            .ContinueWith<ActionResult>((ant) =>
            {
                return RedirectToAction("Index");
            }, TaskContinuationOptions.NotOnCanceled);
        }

        // GET: IsmDevices/Unprovision/5
        public async Task<ActionResult> Unprovision(int id)
        {

            IsmDevice ismDevice = db.IsmDevices.Find(id);
            Device device = await registryManager.GetDeviceAsync(ismDevice.DeviceId);

            // Command for Command History
            Command cmd = new Command();
            cmd.Cmd = CommandType.UNPROVISION;
            cmd.Timestamp = DateTime.Now;
            cmd.IsmDeviceId = id;
            cmd.IsmDevice = ismDevice;

            device.Status = DeviceStatus.Disabled;

            Task t = registryManager.UpdateDeviceAsync(device);
            try
            {
                await t;
            }
            catch (Exception)
            {
            }

            switch (t.Status)
            {
                case TaskStatus.RanToCompletion:
                    cmd.CommandStatus = CommandStatus.SUCCESS;
                    break;
                default:
                    cmd.CommandStatus = CommandStatus.FAILURE;
                    break;
            }

            // Write Command to DB
            db.Commands.Add(cmd);
            db.SaveChanges();

            return RedirectToAction("Index");
        }

        // GET: IsmDevices/Provision/5
        public async Task<ActionResult> Provision(int id)
        {
            IsmDevice ismDevice = db.IsmDevices.Find(id);
            Device device = await registryManager.GetDeviceAsync(ismDevice.DeviceId);

            // Command for Command History
            Command cmd = new Command();
            cmd.Cmd = CommandType.PROVISION;
            cmd.Timestamp = DateTime.Now;
            cmd.IsmDeviceId = id;
            cmd.IsmDevice = ismDevice;

            device.Status = DeviceStatus.Enabled;

            Task t = registryManager.UpdateDeviceAsync(device);
            try
            {
                await t;
            }
            catch (Exception)
            {
            }

            switch (t.Status)
            {
                case TaskStatus.RanToCompletion:
                    cmd.CommandStatus = CommandStatus.SUCCESS;
                    break;
                default:
                    cmd.CommandStatus = CommandStatus.FAILURE;
                    break;
            }

            // Write Command to DB
            db.Commands.Add(cmd);
            db.SaveChanges();

            return RedirectToAction("Index");
        }

        // GET: IsmDevices/Start/5
        public async Task<ActionResult> Start(int id)
        {
            IsmDevice ismDevice = db.IsmDevices.Find(id);
            Device device = await registryManager.GetDeviceAsync(ismDevice.DeviceId);

            // Command for Command History
            Command cmd = new Command();
            cmd.Cmd = CommandType.START;
            cmd.Timestamp = DateTime.Now;
            cmd.IsmDeviceId = id;
            cmd.IsmDevice = ismDevice;
            cmd.CommandStatus = CommandStatus.PENDING;

            // Write Command to DB
            db.Commands.Add(cmd);
            db.SaveChanges();
            int CommandId = db.Entry(cmd).Entity.CommandId; 

            await SendCloudToDevicePortalCommandAsync(CommandId, device.Id, CommandType.START);

            if (signalRHelper.Authenticated)
            {
                // Damit alle offenen Portal Clients das Hinzufügen eines neuen Commands mitbekommen
                await signalRHelper.SignalRHubProxy.Invoke<string>("IsmDevicesIndexChanged").ContinueWith(t =>
                {
                    //Console.WriteLine(t.Result);
                });
            }

            return RedirectToAction("Index");
        }

        // GET: IsmDevices/Stop/5
        public async Task<ActionResult> Stop(int id)
        {
            IsmDevice ismDevice = db.IsmDevices.Find(id);
            Device device = await registryManager.GetDeviceAsync(ismDevice.DeviceId);

            // Command for Command History
            Command cmd = new Command();
            cmd.Cmd = CommandType.STOP;
            cmd.Timestamp = DateTime.Now;
            cmd.IsmDeviceId = id;
            cmd.IsmDevice = ismDevice;
            cmd.CommandStatus = CommandStatus.PENDING;

            // Write Command to DB
            db.Commands.Add(cmd);
            db.SaveChanges();
            int CommandId = db.Entry(cmd).Entity.CommandId;

            await SendCloudToDevicePortalCommandAsync(CommandId, device.Id, CommandType.STOP);

            if (signalRHelper.Authenticated)
            {
                // Damit alle offenen Portal Clients das Hinzufügen eines neuen Commands mitbekommen
                await signalRHelper.SignalRHubProxy.Invoke<string>("IsmDevicesIndexChanged").ContinueWith(t =>
                {
                    //Console.WriteLine(t.Result);
                });
            }

            return RedirectToAction("Index");
        }

        // GET: IsmDevices/StartPreview/5
        public async Task<ActionResult> StartPreview(int id)
        {
            IsmDevice ismDevice = db.IsmDevices.Find(id);
            Device device = await registryManager.GetDeviceAsync(ismDevice.DeviceId);

            // Command for Command History
            Command cmd = new Command();
            cmd.Cmd = CommandType.START_PREVIEW;
            cmd.Timestamp = DateTime.Now;
            cmd.IsmDeviceId = id;
            cmd.IsmDevice = ismDevice;
            cmd.CommandStatus = CommandStatus.PENDING;

            // Write Command to DB
            db.Commands.Add(cmd);
            db.SaveChanges();
            int CommandId = db.Entry(cmd).Entity.CommandId;

            await SendCloudToDevicePortalCommandAsync(CommandId, device.Id, CommandType.START_PREVIEW);
            
            if (signalRHelper.Authenticated)
            {
                // Damit alle offenen Portal Clients das Hinzufügen eines neuen Commands mitbekommen
                await signalRHelper.SignalRHubProxy.Invoke<string>("IsmDevicesIndexChanged").ContinueWith(t =>
                {
                    //Console.WriteLine(t.Result);
                });
            }

            return RedirectToAction("Index");
        }

        // GET: IsmDevices/StopPreview/5
        public async Task<ActionResult> StopPreview(int id)
        {
            IsmDevice ismDevice = db.IsmDevices.Find(id);
            Device device = await registryManager.GetDeviceAsync(ismDevice.DeviceId);

            // Command for Command History
            Command cmd = new Command();
            cmd.Cmd = CommandType.STOP_PREVIEW;
            cmd.Timestamp = DateTime.Now;
            cmd.IsmDeviceId = id;
            cmd.IsmDevice = ismDevice;
            cmd.CommandStatus = CommandStatus.PENDING;

            // Write Command to DB
            db.Commands.Add(cmd);
            db.SaveChanges();
            int CommandId = db.Entry(cmd).Entity.CommandId;

            await SendCloudToDevicePortalCommandAsync(CommandId, device.Id, CommandType.STOP_PREVIEW);
            
            if (signalRHelper.Authenticated)
            {
                // Damit alle offenen Portal Clients das Hinzufügen eines neuen Commands mitbekommen
                await signalRHelper.SignalRHubProxy.Invoke<string>("IsmDevicesIndexChanged").ContinueWith(t =>
                {
                    //Console.WriteLine(t.Result);
                });
            }

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
