using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Commands
{
    // EventType represents the KEYS for event properties
    //
    public struct EventType
    {
        public static string C2D_COMMAND = "C2D_Command";
        public static string D2C_COMMAND = "D2C_Command";

        // More possible event types:
        //public static string TELEMETRY ...
        //public static string INQUIRY ...
        //public static string NOTIFICATION ...
    }

    // CommandType represents the VALUES for event properties (for the KEY EventType::COMMAND)
    //
    public struct CommandType
    {
        // Device identity registry Commands
        public static string UNPROVISION = "Unprovision";
        public static string PROVISION = "Provision";

        // C2D Commands
        public static string START = "Start";
        public static string STOP = "Stop";
        public static string START_PREVIEW = "StartPreview";
        public static string STOP_PREVIEW = "StopPreview";

        // C2D Dashboard Commands
        public static string GET_DEVICE_SETTINGS = "GetDeviceSettings";
        public static string SET_DEVICE_SETTINGS = "SetDeviceSettings";

        // D2C Commands
        /* Obsolete. Use CAPTURE_UPLOADED and look at the StateName field
         * RunState -> DAT
         * PreviewState -> PRV
         * 
        public static string DAT = "D_DAT";
        public static string PRV = "D_PRV";
        */
        public static string CAPTURE_UPLOADED = "CaptureUploaded";

        // D2C Dashboard Commands 
        public static string UPDATE_DASHBOARD_CONTROLS = "UpdateDashboardControls";
    };

    // More possible event types:
    // struct TelemetryType ...
    // struct InquiryType ...
    // struct NotificationType ...



    struct CommandStatus
    {
        public static string SUCCESS = "Success";
        public static string FAILURE = "Failure";
        public static string PENDING = "Pending";
    };

}
