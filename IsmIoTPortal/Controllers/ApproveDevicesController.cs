using System;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using IsmIoTPortal.Models;
using Microsoft.Azure.Devices;

namespace IsmIoTPortal.Controllers
{
    /// <summary>
    /// Controller to manage devices with pending approval.
    /// </summary>
    public class ApproveDevicesController : Controller
    {
        private static readonly RegistryManager registryManager = RegistryManager.CreateFromConnectionString(ConfigurationManager.ConnectionStrings["ismiothub"].ConnectionString);
        private readonly IsmIoTPortalContext db = new IsmIoTPortalContext();
        // GET: ApproveDevices
        public ActionResult Index()
        {
            var ismDevices = db.NewDevices.Include(i => i.Hardware).Include(i => i.Location).Include(i => i.Software);
            return View(ismDevices.ToList());
        }

        public ActionResult Delete(int? id)
        {
            // Check that an ID has been passed
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var newDevice = db.NewDevices.Find(id);
            // Check that device exists
            if (newDevice == null)
            {
                return HttpNotFound();
            }
            var device = db.IsmDevices.First(d => d.DeviceId.Equals(newDevice.DeviceId));
            return RedirectToAction("Delete", "IsmDevices", new {id = device.IsmDeviceId});
        }

        public async Task<ActionResult> Approve(int? id)
        {
            // Check that an ID has been passed
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var newDevice = db.NewDevices.Find(id);
            // Check that device exists
            if (newDevice == null)
            {
                return HttpNotFound();
            }
            // If device has already been approved, don't go any further
            if (newDevice.Approved)
                return RedirectToAction("Index");
            // Create new IsmDevice to add to databasse
            var device = new IsmDevice
            {
                DeviceId = newDevice.DeviceId,
                HardwareId = newDevice.HardwareId,
                SoftwareId = newDevice.SoftwareId,
                LocationId = newDevice.LocationId
            };
            // Add to DB
            db.IsmDevices.Add(device);
            db.SaveChanges();
            // Try to add to IoT Hub
            try
            {
                await AddDeviceAsync(device.DeviceId);
                // Approve device if exception hasn't been thrown
                newDevice.Approved = true;
                db.SaveChanges();
            }
            catch (Exception)
            {
                // Anzeigen, dass etwas schief ging
                // Eintrag aus DB entfernen
                db.IsmDevices.Remove(device);
                db.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        public ActionResult Remove(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            NewDevice device = db.NewDevices.Find(id);
            if (device == null)
            {
                return HttpNotFound();
            }
            db.NewDevices.Remove(device);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Adds device to IoT Hub.
        /// </summary>
        /// <param name="deviceId">Key for authentication.</param>
        /// <returns></returns>
        private static async Task<string> AddDeviceAsync(string deviceId)
        {
            Device device = await registryManager.AddDeviceAsync(new Device(deviceId));
            return device.Authentication.SymmetricKey.PrimaryKey;
        }
    }
}