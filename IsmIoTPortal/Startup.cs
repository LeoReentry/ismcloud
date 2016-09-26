using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(IsmIoTPortal.Startup))]
namespace IsmIoTPortal
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);

            //string connectionString = "Endpoint=sb://sbbackplane.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZpiOSzx3IwtzV/kSQ2o4DXPLtZK5AlBiqFGjXganEMM=";
            string connectionString = IsmIoTSettings.Settings.sbRootManage;
            GlobalHost.DependencyResolver.UseServiceBus(connectionString, "IsmIoT");

            app.MapSignalR();
        }
    }
}
