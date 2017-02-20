using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace IsmIoTPortal.Models
{
    /* UpdateState ist kein Model welches in der Datenbank persistiert wird, es dient dem eleganten Informationsaustausch zwischen 
       Eventprocessor und Device. Außerdem wird die Klasse in C2D, D2C und Queue Messages eingesetzt
       und ist deshalb als serialisierbares Objekt implementiert.
    */
    [DataContract]
    public class UpdateState
    {
        // 
        [DataMember]
        public string DeviceId { get; set; }
        [DataMember]
        public string FwUpdateStatus { get; set; }
        [DataMember]
        public string Log { get; set; }
        [DataMember]
        public string Message { get; set; }
        [DataMember]
        public string Version { get; set; }
    }
}