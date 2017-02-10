using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace IsmIoTPortal.Models
{
    public class SoftwareVersion
    {
        [Key]
        public int SoftwareVersionId { get; set; }
        public string Prefix { get; set; }
        public string Suffix { get; set; }
        public int[] CurrentReleaseNum { get; set; }
    }
}