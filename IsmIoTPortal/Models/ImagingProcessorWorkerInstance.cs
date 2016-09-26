using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace IsmIoTPortal.Models
{
    public class ImagingProcessorWorkerInstance
    {
        //[Key]
        //public int ImagingProcessorWorkerInstanceId { get; set; }
        [Key]
        public string RoleInstanceId { get; set; }
        public bool McrInstalled { get; set; }
        public DateTime Timestamp { get; set; }
    }
}