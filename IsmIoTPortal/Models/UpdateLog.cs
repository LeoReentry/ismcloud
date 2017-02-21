using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace IsmIoTPortal.Models
{
    public class UpdateLog
    {
        [Key]
        public int UpdateLogId { get; set; }
        public int IsmDeviceId { get; set; }
        [ForeignKey("IsmDeviceId")]
        public virtual IsmDevice IsmDevice { get; set; }
        public int ReleaseId { get; set; }
        [ForeignKey("ReleaseId")]
        public virtual Release Release { get; set; }

        [DataType(DataType.MultilineText)]
        public string LogData { get; set; }
        public DateTime Date { get; set; }

    }
}