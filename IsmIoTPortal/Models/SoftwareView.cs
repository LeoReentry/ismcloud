using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace IsmIoTPortal.Models
{
    public class SoftwareView
    {
        public Software Software { get; set; }
        public IEnumerable<Models.IsmDevice> Devices { get; set; }

    }
}