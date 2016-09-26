using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace IsmIoTPortal.Models
{
    public class Software
    {
        [Key]
        public int SoftwareId { get; set; }
        public string SoftwareVersion { get; set; }
        public virtual List<IsmDevice> IsmDevices { get; set; }
    }
}