using IsmIoTPortal.Models;
using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsmDevice
{
    interface IDeviceState
    {
        Task StartAsync(Message msg);
        Task StopAsync(Message msg);
        Task StartPreviewAsync(Message msg);
        Task StopPreviewAsync(Message msg);
        // zusätzlicher Parameter deviceState, weil wegen dem Property Bug in der Device.cs schon das GetBytes() auf die Message aufgerufen wurde
        Task SetDeviceStateAsync(Message msg);
        Task DoWorkAsync();
    }
}
