using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsmIoTSettings
{
    public class Settings
    {
        // 
        // ******** LOCAL ********
#if DEBUG
        public static string webDomain = "localhost";
        public static string webProtocol = "https";
        public static string webPort = "44338";
        public static string webCompleteAddress = webProtocol + "://" + webDomain + ":" + webPort;
#else
        public static string webDomain = "ismportal.azurewebsites.net";
        public static string webProtocol = "https";
        public static string webPort = "";
        public static string webCompleteAddress = webProtocol + "://" + webDomain;
#endif

   }
}
