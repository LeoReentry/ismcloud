using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace IsmIoTPortal.Models
{
    public class DateTimeInterval
    {
        public string DeviceId { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<FilamentData> List { get; set; }
    }
}