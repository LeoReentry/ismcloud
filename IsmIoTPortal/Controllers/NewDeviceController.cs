using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using IsmIoTPortal.Models;
using IsmIoTSettings;
using Microsoft.Azure.Devices;
using Newtonsoft.Json;

namespace IsmIoTPortal.Controllers
{
    [AllowAnonymous]
    public class NewDeviceController : ApiController
    {
        private static Random generator = new Random();

        private static readonly RegistryManager registryManager = RegistryManager.CreateFromConnectionString(ConfigurationManager.ConnectionStrings["ismiothub"].ConnectionString);
        private static readonly ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(ConfigurationManager.ConnectionStrings["ismiothub"].ConnectionString);
        private readonly IsmIoTPortalContext db = new IsmIoTPortalContext();
        /// <summary>
        /// The API call to /api/newdevice requests the possible location, hardware and software IDs.
        /// </summary>
        /// <returns>Returns JSON or XML formatted location, software and hardware IDs.</returns>
        public dynamic Get()
        {
            var locations = db.Locations.Select(i =>
                    new {i.LocationId, i.City, i.Country});
            var hardwares = db.Hardware.Select(i =>
                    new {i.HardwareId, i.Board, i.Camera});
            var softwares = db.Software.Select(i =>
                    new { i.SoftwareId, i.SoftwareVersion });
            return new 
            {
                Locations = locations,
                Hardware = hardwares,
                Software = softwares
            };
        }
        /// <summary>
        /// API call to add new device to database.
        /// </summary>
        /// <param name="id">Identifier of the device</param>
        /// <param name="loc">Location ID</param>
        /// <param name="hw">Hardware ID</param>
        /// <param name="sw">Software ID</param>
        /// <returns>Device that was created.</returns>
        public object Get(string id, int loc, int hw, int sw)
        {
            var err = new { Error = "An error occured." };
            // Check Device ID against a whitelist of values to prevent XSS
            if (!IsmIoTSettings.RegexHelper.Text.IsMatch(id))
                return err;

            // If device with same ID already exists, return error
            if (db.IsmDevices.Any(d => d.DeviceId == id) || db.NewDevices.Any(d => d.DeviceId == id))
                return new {Error = "This device ID is already taken."};

            var dev = new NewDevice
            {
                DeviceId = id,
                HardwareId = hw,
                LocationId = loc,
                SoftwareId = sw,
                Code = generator.Next(0, 999999).ToString("D6"),
                Approved = false
            };
            db.NewDevices.Add(dev);
            db.SaveChanges();
            return new
            {
                Id = dev.DeviceId,
                Code = dev.Code
            };
        }

        /// <summary>
        /// API call to retrieve IoT Hub key once device is approved.
        /// </summary>
        /// <param name="id">Identifier of the device.</param>
        /// <param name="code">6 digit code that identifies it as the same device.</param>
        /// <returns></returns>
        public async Task<object> Get(string id, string code)
        {
            // Check Device ID against a whitelist of values to prevent XSS
            if (!IsmIoTSettings.RegexHelper.Text.IsMatch(id))
                return new { Error = "An error occured." };
            
            // Check that device exists
            if (!db.NewDevices.Any(d => d.DeviceId == id && d.Code == code))
                return new { Error = "Device does not exist." };
            // Get reference to device
            var newDevice = db.NewDevices.First(d => d.DeviceId == id && d.Code == code);
            // Only if device is approved
            if (newDevice.Approved)
            {
                // If device doesn't exist in IoT Hub, some error has occured
                if (await registryManager.GetDeviceAsync(id) == null)
                    return new { Error = "An error occured." };
                // Get key from IoT Hub
                Device device = await registryManager.GetDeviceAsync(id);
                string key = device.Authentication.SymmetricKey.PrimaryKey;
                // Remove device from database
                db.NewDevices.Remove(newDevice);
                db.SaveChanges();
                // Return key
                return new { Key = key };
            }

            return new { Error = "An error occured." };
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
