using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace IsmIoTPortal.Models
{
    /* DeviceState ist kein Model welches in der Datenbank persistiert wird, es dient dem eleganten Informationsaustausch zwischen 
       Controller und View. Außerdem wird die Klasse in C2D, D2C und Queue Messages eingesetzt
       und ist deshalb als serialisierbares Objekt implementiert.
    */
    [DataContract]
    public class DeviceState
    {
        // 
        [DataMember]
        public string DeviceId { get; set; }
        [DataMember]
        public string State { get; set; }
        [DataMember]
        public int CapturePeriod { get; set; }
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
        public int Brightness { get; set; }
        [DataMember]
        public int Exposure { get; set; }

        // Pulser Settings
        [DataMember]
        public int PulseWidth { get; set; }
        [DataMember]
        public int Current { get; set; }
        [DataMember]
        public int Predelay { get; set; }
        [DataMember]
        public bool IsOn { get; set; }
    }
}