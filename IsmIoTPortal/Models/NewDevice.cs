using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace IsmIoTPortal.Models
{
    public class NewDevice
    {
        public int IsmDeviceId { get; set; }
        public string DeviceId { get; set; }
        public int LocationId { get; set; }
        public int SoftwareId { get; set; }
        public int HardwareId { get; set; }
        public string PASS { get; set; }
    }
}