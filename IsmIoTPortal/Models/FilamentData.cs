using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace IsmIoTPortal.Models
{
    [DataContract]
    public class FilamentData
    {
        [Key]
        public int FilamentDataId { get; set; }
        [DataMember]
        public DateTime? Time { get; set; }
        [DataMember]
        public double? FC { get; set; }
        [DataMember]
        public double? FL { get; set; }

        //public int[] Histogram { get; set; }
        // Histogram Fields
        [DataMember]
        public int? H1 { get; set; }
        [DataMember]
        public int? H2 { get; set; }
        [DataMember]
        public int? H3 { get; set; }
        [DataMember]
        public int? H4 { get; set; }
        [DataMember]
        public int? H5 { get; set; }
        [DataMember]
        public int? H6 { get; set; }
        [DataMember]
        public int? H7 { get; set; }
        [DataMember]
        public int? H8 { get; set; }
        [DataMember]
        public int? H9 { get; set; }
        [DataMember]
        public int? H10 { get; set; }


        [DataMember]
        public int IsmDeviceId { get; set; }
        [ForeignKey("IsmDeviceId")]
        public IsmDevice IsmDevice { get; set; }
        [DataMember]
        public string DeviceId { get; set; }
        [DataMember]
        public string BlobImgName { get; set; } = "";
        [DataMember]
        public string BlobColoredImgName { get; set; } = "";

        public FilamentData()
        {
        }

        public FilamentData(double fc, double fl, int ismdeviceid, 
            int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10,
            string deviceid, string imgName, string coloredImgName)
        {
            //Time = DateTime.Now; // uninitialisiert lassen und in ASA Query dann SELECT System.Timestamp AS Time..
            FC = fc;
            FL = fl;
            IsmDeviceId = ismdeviceid;

            H1 = h1;
            H2 = h2;
            H3 = h3;
            H4 = h4;
            H5 = h5;
            H6 = h6;
            H7 = h7;
            H8 = h8;
            H9 = h9;
            H10 = h10;

            DeviceId = deviceid;
            BlobImgName = imgName;
            BlobColoredImgName = coloredImgName;
        }
    }
}