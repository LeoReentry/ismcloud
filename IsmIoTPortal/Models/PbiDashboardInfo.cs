using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace IsmIoTPortal.Models
{
    public class PbiDashboardInfo
    {
        public string DashboardId { get; set; }
        public string UserId { get; set; }
        public string AccessToken { get; set; }
        public List<string> TileEmbedURLs { get; set; }
    }
}