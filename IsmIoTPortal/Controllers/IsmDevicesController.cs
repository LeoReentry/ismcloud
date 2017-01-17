using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using IsmIoTPortal.Models;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Newtonsoft.Json;
using System.Text;
using System.Threading;

namespace IsmIoTPortal.Controllers
{
    public class IsmDevicesController : Controller
    {
        private readonly IsmIoTPortalContext db = new IsmIoTPortalContext();

        //static string connectionString = "HostName=iothubism.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=nhhwSNpr3p68FcTZfvPEfU7xvJRH/jOpTcWQbQMoKAg=";
        //static RegistryManager registryManager = RegistryManager.CreateFromConnectionString(connectionString);
        //static ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
        private static readonly RegistryManager registryManager = RegistryManager.CreateFromConnectionString(ConfigurationManager.ConnectionStrings["ismiothub"].ConnectionString);
        private static readonly ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(ConfigurationManager.ConnectionStrings["ismiothub"].ConnectionString);

        private static readonly IsmIoTSettings.SignalRHelper signalRHelper = new IsmIoTSettings.SignalRHelper("DeviceController");


        private static async Task SendCloudToDevicePortalCommandAsync(int commandId, string deviceId, string cmd)
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

            commandMessage.Properties["C2D_Command"] = cmd;
            commandMessage.Ack = DeliveryAcknowledgement.Full;
            commandMessage.MessageId = MessageIdPrefix.CMD + " " + commandId.ToString();
            await serviceClient.SendAsync(deviceId, commandMessage); 
        }


        // Device State Setzen
        private static async Task C2DSetDeviceStateAsync(string deviceId, DeviceState deviceState)
        {
            // Durch View veränderter DeviceState in Message Body packen
            string serializedDeviceState = JsonConvert.SerializeObject(deviceState);
            var commandMessage = new Message(Encoding.ASCII.GetBytes(serializedDeviceState));

            commandMessage.Properties["C2D_Command"] = CommandType.SET_DEVICE_STATE;

            //commandMessage.Ack = DeliveryAcknowledgement.Full;
            commandMessage.MessageId = Guid.NewGuid().ToString();
            await serviceClient.SendAsync(deviceId, commandMessage);
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
        public async Task<ActionResult> Dashboard(string deviceId)
        {
            // Check Device ID against a whitelist of values to prevent XSS
            if (!IsmIoTSettings.RegexHelper.Text.IsMatch(deviceId))
                return HttpNotFound();

            // If device doesn't exist, redirect to index
            if (await registryManager.GetDeviceAsync(deviceId) == null)
                return RedirectToAction("Index");

            // Load page
            DeviceState deviceState = new DeviceState();
            deviceState.DeviceId = deviceId;
            //await C2DGetDeviceStateAsync(DeviceId);
            return View(deviceState);
        }

        // POST: IsmDevices/Dashboard/<DeviceState>
        // Use Bind to whitelist values which can be set through web interface
        // Malicious attacks with additional form data can not be successful
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Dashboard([Bind(Include = "DeviceId,VarianceThreshold,DistanceMapThreshold,RGThreshold,RestrictedFillingThreshold,DilateValue,CapturePeriod")]DeviceState deviceState)
        {
            // Check Device ID against a whitelist of values to prevent XSS
            if (!IsmIoTSettings.RegexHelper.Text.IsMatch(deviceState.DeviceId))
                return HttpNotFound();

            // If device doesn't exist, redirect to index
            // The rest of the user input is sanitized by parsing the value to numbers
            if (await registryManager.GetDeviceAsync(deviceState.DeviceId) == null)
                return RedirectToAction("Index");

            // C2D Message die dem Device einen durch die Controls veränderten DeviceState mitteilt
            await C2DSetDeviceStateAsync(deviceState.DeviceId, deviceState);
            //ModelState.Clear();
            return View(deviceState);
        }


        public async Task<ActionResult> ShowKey(string deviceId)
        {
            // Check Device ID against a whitelist of values to prevent XSS
            if (!IsmIoTSettings.RegexHelper.Text.IsMatch(deviceId))
                return HttpNotFound();

            // If device doesn't exist, redirect to index
            if (await registryManager.GetDeviceAsync(deviceId) == null)
                return RedirectToAction("Index");

            Device device = await registryManager.GetDeviceAsync(deviceId);
            string key = device.Authentication.SymmetricKey.PrimaryKey;
            return View(model:key);
        }

        private static async Task<string> AddDeviceAsync(string deviceId)
        {
            Device device = await registryManager.AddDeviceAsync(new Device(deviceId));
            return device.Authentication.SymmetricKey.PrimaryKey;
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
            // Check Device ID against a whitelist of values to prevent XSS
            if (!IsmIoTSettings.RegexHelper.Text.IsMatch(ismDevice.DeviceId))
                return HttpNotFound();

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
        public ActionResult Edit(int? id, [Bind(Include = "IsmDeviceId,DeviceId,LocationId,SoftwareId,HardwareId")] IsmDevice ismDevice)
        {
            // Check Device ID against a whitelist of values to prevent XSS
            if (!IsmIoTSettings.RegexHelper.Text.IsMatch(ismDevice.DeviceId))
                return HttpNotFound();
            // Check that POST device ID is the same as ID parameter
            if (id == null || id != ismDevice.IsmDeviceId)
                return HttpNotFound();
            // Return error if device doesn't exist
            if (db.IsmDevices.Find(ismDevice.IsmDeviceId) == null)
                return HttpNotFound();

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
            NewDevice newDevice;
            try
            {
                // Look for device in NewDevices database, since it might still be around.
                newDevice = db.NewDevices.First(d => d.DeviceId.Equals(ismDevice.DeviceId));
            }
            // If database is empty, we'll receive InvalidOperationException
            catch (InvalidOperationException e)
            {
                newDevice = null;
            }

            // Check that id was correct
            if (ismDevice == null)
                return HttpNotFound();

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
                    if (newDevice != null)
                        db.NewDevices.Remove(newDevice);
                    db.IsmDevices.Remove(ismDevice);
                    db.SaveChanges();
                    cts.Cancel();
                    return RedirectToAction("Index");
            }, TaskContinuationOptions.OnlyOnRanToCompletion)
            .ContinueWith((ant, ct) =>
            {
            }, cts.Token)
            .ContinueWith<ActionResult>((ant) => RedirectToAction("Index"), TaskContinuationOptions.NotOnCanceled);
        }

        // GET: IsmDevices/Unprovision/5
        public async Task<ActionResult> Unprovision(int id)
        {

            IsmDevice ismDevice = db.IsmDevices.Find(id);

            // Check that id was correct
            if (ismDevice == null)
                return HttpNotFound();

            Device device = await registryManager.GetDeviceAsync(ismDevice.DeviceId);

            // Command for Command History
            Command cmd = new Command
            {
                Cmd = CommandType.UNPROVISION,
                Timestamp = DateTime.Now,
                IsmDeviceId = id,
                IsmDevice = ismDevice
            };

            device.Status = DeviceStatus.Disabled;

            Task t = registryManager.UpdateDeviceAsync(device);
            try
            {
                await t;
            }
            catch (Exception)
            {
                // ignored
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


            // Check that id was correct
            if (ismDevice == null)
                return HttpNotFound();

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
                // ignored
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


            // Check that id was correct
            if (ismDevice == null)
                return HttpNotFound();

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
            int commandId = db.Entry(cmd).Entity.CommandId; 

            await SendCloudToDevicePortalCommandAsync(commandId, device.Id, CommandType.START);
            // Damit alle offenen Portal Clients das Hinzufügen eines neuen Commands mitbekommen
            await signalRHelper.IsmDevicesIndexChangedTask();

            return RedirectToAction("Index");
        }

        // GET: IsmDevices/Stop/5
        public async Task<ActionResult> Stop(int id)
        {
            IsmDevice ismDevice = db.IsmDevices.Find(id);

            // Check that id was correct
            if (ismDevice == null)
                return HttpNotFound();

            Device device = await registryManager.GetDeviceAsync(ismDevice.DeviceId);

            // Command for Command History
            Command cmd = new Command
            {
                Cmd = CommandType.STOP,
                Timestamp = DateTime.Now,
                IsmDeviceId = id,
                IsmDevice = ismDevice,
                CommandStatus = CommandStatus.PENDING
            };

            // Write Command to DB
            db.Commands.Add(cmd);
            db.SaveChanges();
            int commandId = db.Entry(cmd).Entity.CommandId;

            await SendCloudToDevicePortalCommandAsync(commandId, device.Id, CommandType.STOP);

            // Damit alle offenen Portal Clients das Hinzufügen eines neuen Commands mitbekommen
            await signalRHelper.IsmDevicesIndexChangedTask();

            return RedirectToAction("Index");
        }

        // GET: IsmDevices/StartPreview/5
        public async Task<ActionResult> StartPreview(int id)
        {
            IsmDevice ismDevice = db.IsmDevices.Find(id);

            // Check that id was correct
            if (ismDevice == null)
                return HttpNotFound();

            Device device = await registryManager.GetDeviceAsync(ismDevice.DeviceId);

            // Command for Command History
            Command cmd = new Command
            {
                Cmd = CommandType.START_PREVIEW,
                Timestamp = DateTime.Now,
                IsmDeviceId = id,
                IsmDevice = ismDevice,
                CommandStatus = CommandStatus.PENDING
            };

            // Write Command to DB
            db.Commands.Add(cmd);
            db.SaveChanges();
            int commandId = db.Entry(cmd).Entity.CommandId;

            await SendCloudToDevicePortalCommandAsync(commandId, device.Id, CommandType.START_PREVIEW);

            // Damit alle offenen Portal Clients das Hinzufügen eines neuen Commands mitbekommen
            await signalRHelper.IsmDevicesIndexChangedTask();

            return RedirectToAction("Index");
        }

        // GET: IsmDevices/StopPreview/5
        public async Task<ActionResult> StopPreview(int id)
        {
            IsmDevice ismDevice = db.IsmDevices.Find(id);

            // Check that id was correct
            if (ismDevice == null)
                return HttpNotFound();

            Device device = await registryManager.GetDeviceAsync(ismDevice.DeviceId);

            // Command for Command History
            Command cmd = new Command
            {
                Cmd = CommandType.STOP_PREVIEW,
                Timestamp = DateTime.Now,
                IsmDeviceId = id,
                IsmDevice = ismDevice,
                CommandStatus = CommandStatus.PENDING
            };

            // Write Command to DB
            db.Commands.Add(cmd);
            db.SaveChanges();
            int commandId = db.Entry(cmd).Entity.CommandId;

            await SendCloudToDevicePortalCommandAsync(commandId, device.Id, CommandType.STOP_PREVIEW);

            // Damit alle offenen Portal Clients das Hinzufügen eines neuen Commands mitbekommen
            await signalRHelper.IsmDevicesIndexChangedTask();

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
