using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace IsmIoTPortal.Models
{
    [DataContract]
    public class DeviceSettings
    {
        // 
        [DataMember]
        public string DeviceId { get; set; }
        [DataMember]
        public string StateName { get; set; }
        [DataMember]
        public uint CapturePeriod { get; set; }
        [DataMember]
        public string CurrentCaptureName { get; set; }

        // Matlab Filament-Algorithm Params
        [DataMember]
        public double VarianceThreshold { get; set; }
        [DataMember]
        public double DistanceMapThreshold { get; set; }
        [DataMember]
        public double RGThreshold { get; set; }
        [DataMember]
        public double RestrictedFillingThreshold { get; set; }
        [DataMember]
        public double DilateValue { get; set; }

        // Camera Settings
        [DataMember]
        public uint Gain { get; set; }
        [DataMember]
        public double Exposure { get; set; }

        // Pulser Settings
        [DataMember]
        public uint PulseWidth { get; set; }
        [DataMember]
        public int Current { get; set; }
        [DataMember]
        public uint Predelay { get; set; }
    }
}