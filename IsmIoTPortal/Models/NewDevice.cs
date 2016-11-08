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
        [Key]
        public int IsmDeviceId { get; set; }
        public string DeviceId { get; set; }
        public int LocationId { get; set; }
        [ForeignKey("LocationId")]
        public virtual Location Location { get; set; }
        public int SoftwareId { get; set; }
        [ForeignKey("SoftwareId")]
        public virtual Software Software { get; set; }
        public int HardwareId { get; set; }
        [ForeignKey("HardwareId")]
        public virtual Hardware Hardware { get; set; }
        public string Code { get; set; }
    }
}