using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using System.Threading;
using System.Threading.Tasks;

namespace IsmIoTPortal
{
    [Authorize]
    public class DashboardHub : Hub
    {
        public override Task OnConnected()
        {
            var user = Context.User;
            return Clients.Caller.hubReceived("Welcome, " + user.Identity.Name);
        }
        // Dashboard
        //
        public void RegisterForDashboard(string DeviceId)
        {
            this.Groups.Add(this.Context.ConnectionId, DeviceId);
        }

        public void UnRegisterForDashboard(string DeviceId)
        {
            this.Groups.Remove(this.Context.ConnectionId, DeviceId);
        }

        public void DataForDashboard(string DeviceId, string blobUri, string fc, string fl, string blobUriColored)
        {
            this.Clients.Group(DeviceId).UpdateDashboard(blobUri, fc, fl, blobUriColored);
        }

        public void ValuesForDashboardControls(string DeviceId, int uploadDelay,
            double varianceThreshold, double distanceMapThreshold, double rgThreshold,
            double restrictedFillingThreshold, double DilateValue)
        {
            Thread.Sleep(3000); // Sicherheits-Verzögerung, damit Browser Dashboard sicher geladen hat bevor SignalR Nachricht kommt
            this.Clients.Group(DeviceId).UpdateDashboardControls(uploadDelay, varianceThreshold, distanceMapThreshold, rgThreshold,
                restrictedFillingThreshold, DilateValue);
        }


        // IsmDevices Index
        // (Konstanter Group-Name: "IsmDevicesIndex")
        public void RegisterForIsmDevicesIndex()
        {
            this.Groups.Add(this.Context.ConnectionId, "IsmDevicesIndex");
        }

        public void UnRegisterForIsmDevicesIndex(string DeviceId)
        {
            this.Groups.Remove(this.Context.ConnectionId, DeviceId);
        }
        public void IsmDevicesIndexChanged()
        {
            this.Clients.Group("IsmDevicesIndex").RefreshView();
        }
    }
}