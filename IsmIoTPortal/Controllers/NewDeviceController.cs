using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using IsmIoTPortal.Models;
using IsmIoTSettings;
using Newtonsoft.Json;

namespace IsmIoTPortal.Controllers
{
    [AllowAnonymous]
    public class NewDeviceController : ApiController
    {
        private static Random generator = new Random();
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
        public NewDevice Get(string id, int loc, int hw, int sw)
        {
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
            return dev;
        }

        public object Get(string id, string code)
        {

            return null;
        }

    }
}
