using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace IsmIoTPortal.Models
{
    public class Command
    {
        [Key]
        public int CommandId { get; set; }
        public string Cmd { get; set; }
        public DateTime Timestamp { get; set; }
        public string CommandStatus { get; set; }
        public int IsmDeviceId { get; set; }
        [ForeignKey("IsmDeviceId")]
        public IsmDevice IsmDevice { get; set; }
    }
}