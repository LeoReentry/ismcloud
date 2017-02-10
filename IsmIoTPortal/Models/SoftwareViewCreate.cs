using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace IsmIoTPortal.Models
{
    public class SoftwareViewCreate
    {
        public Release Release { get; set; }
        public IEnumerable<Models.SoftwareVersion> SoftwareVersions { get; set; }

    }
}