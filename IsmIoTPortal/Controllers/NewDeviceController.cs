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
        private readonly IsmIoTPortalContext db = new IsmIoTPortalContext();
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

        public object Get(string id)
        {
            if (RegexHelper.Text.IsMatch(id))
            {
                var response = new
                {
                    id = id,
                    guid = System.Web.Security.Membership.GeneratePassword(20, 0),

                };
                return response;
            }
            return new string[]
            {
             "Hello",
             "World"
            };
        }

    }
}
