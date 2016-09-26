using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace IsmIoTPortal.Models
{
    public class Hardware
    {
        [Key]
        public int HardwareId { get; set; }
        public string Board { get; set; }
        public string Camera { get; set; }
        public virtual List<IsmDevice> IsmDevices { get; set; }
    }
}