using Microsoft.Azure.Devices.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Die Vorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 dokumentiert.

namespace IsmDevice
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet werden kann oder auf die innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public DeviceClient DeviceClient { get; set; }

        public CloudBlobClient BlobClient { get; set; }
        public List<byte[]> CameraCaptures { get; set; }

        public MainPage()
        {
            this.InitializeComponent();

            // TODO: Config-File
            //string iotHubUri = "iothubism.azure-devices.net";

            // Device 1: Trage IsmDevice1 in  der Package.appxmanifest im Reiter Verpacken, bei Paketname  ein!
            //string DeviceId = "ISM_DEVICE_1";
            //string containerName = "picturecontainer1";
            //string deviceKey = "ay0aqPc714HBye2mLgnVfvMOjya4F9UKSdT1VTKUHDo="; // ISM_DEVICE_1

            // Device 2: Trage IsmDevice2 in  der Package.appxmanifest im Reiter Verpacken, bei Paketname  ein!
            //string DeviceId = "ISM_DEVICE_2";
            //string containerName = "picturecontainer2";
            //string deviceKey = "xUxN1w78PEcu1tYe9PEHNoJ3Tyy1QzlsP/lcovriu8w="; // ISM_DEVICE_2

            //string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=picturesto;AccountKey=IxESdcVI3BxmL0SkoDsWx1+B5ZDArMHNrQlQERpcCo3e6eOCYptJTTKMin6KIbwbRO2CcmVpcn/hJ2/krrUltA==";

            // args[0] = DeviceId args[1] = IoTHubUri args[2] = DeviceKey args[3] = storageConnectionString args[4] = containerName
            // Beispiel:args[0] = ISM_HSMA_SWT args[1] = arciothubtest.azure-devices.net args[2] = /8plm7aDgyPtwkaVlTTnNzQBQkciAt8hthfB3r8BBHo= args[3] = DefaultEndpointsProtocol=https;AccountName=picturesto;AccountKey=IxESdcVI3BxmL0SkoDsWx1+B5ZDArMHNrQlQERpcCo3e6eOCYptJTTKMin6KIbwbRO2CcmVpcn/hJ2/krrUltA== args[4] = picturecontainer
            //

            //Device device = new Device(DeviceId, iotHubUri, deviceKey, storageConnectionString, containerName);
            Device device = new Device(IsmIoTSettings.Settings.DeviceId, IsmIoTSettings.Settings.iotHubUri, IsmIoTSettings.Settings.deviceKey, IsmIoTSettings.Settings.storageConnection, IsmIoTSettings.Settings.containerCaptureSet);

            // Device Events
            //
            device.StateChanged += Device_StateChanged;
            device.WriteLine += Device_WriteLine;

            //
            device.StartDevice();
        }

        // Event Handler für die Events vom Device
        //
        private void Device_WriteLine(object sender, EventArgs e)
        {
            if (e is WriteLineEventArgs)
            {
                WriteLineEventArgs writeLineEventArgs = e as WriteLineEventArgs;
                WriteLine(writeLineEventArgs.Text, writeLineEventArgs.Color);
            }
        }

        private void Device_StateChanged(object sender, EventArgs e)
        {
            if (e is StateEventArgs)
            {
                StateEventArgs stateEventArgs = e as StateEventArgs;
                Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    stateTextBlock.Text = stateEventArgs.State;
                });
            }
        }


        // WriteLine Methode um auf RichTextBlock zu schreiben
        //
        private void WriteLine(string text, Color color)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var p = new Paragraph();
                p.Inlines.Add(new Run { Foreground = new SolidColorBrush(Colors.Gray), Text = DateTime.Now.ToString() + " "});
                p.Inlines.Add(new Run { Foreground = new SolidColorBrush(color), Text = text });
                console.Blocks.Add(p);

                // Auto-Scroll ans Ende
                scrollViewer.ScrollToVerticalOffset(double.MaxValue);               
            });
        }

    }
}
