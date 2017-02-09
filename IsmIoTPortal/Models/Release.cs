using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace IsmIoTPortal.Models
{
    public class Release
    {
        [Key]
        public int SoftwareId { get; set; }
        public string SoftwareVersion { get; set; }
        public int[] ReleaseNum { get; set; }
        public string Url { get; set; }
        public string Status { get; set; }
        public string Checksum { get; set; }
        public string Author { get; set; }
        [DataType(DataType.MultilineText)]
        public string Changelog { get; set; }
        public DateTime Date { get; set; }
        public virtual List<IsmDevice> IsmDevices { get; set; }
    }
}