using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
        public string InternalReleaseNum { get; set; }
        [NotMapped]
        public int[] CurrentReleaseNum
        {
            get
            {
                return Array.ConvertAll(InternalReleaseNum.Split(';'), Int32.Parse);
            }
            set
            {
                InternalReleaseNum = String.Join(";", value.Select(p => p.ToString()).ToArray());
            }
        }
    }
}