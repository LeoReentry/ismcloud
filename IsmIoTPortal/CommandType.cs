using System;
using System.Collections.Generic;
using System.Linq;

namespace IsmIoTPortal
{
    public struct CommandType
    {
        // C2D Commands
        public const string UNPROVISION = "Unprovision";
        public const string PROVISION = "Provision";

        public const string START = "Start";
        public const string STOP = "Stop";
        public const string START_PREVIEW = "StartPreview";
        public const string STOP_PREVIEW = "StopPreview";

        // C2D Dashboard Commands
        public const string GET_DEVICE_STATE = "GetDeviceSettings";
        public const string SET_DEVICE_STATE = "SetDevicesettings";
        public const string UPDATE_DASHBOARD_CONTROLS = "UpdateDashboardControls";

        // D2C Commands
        public const string DAT = "DAT";
        public const string PRV = "PRV";
    }

    public struct CommandStatus
    {
        public const string SUCCESS = "Success";
        public const string FAILURE = "Failure";
        public const string PENDING = "Pending";
    }

    public struct DeviceStates
    {
        public const string READY_STATE = "ReadyState";
        public const string RUN_STATE = "RunState";
        public const string PREVIEW_STATE = "PreviewState";
    }

    // 3-Buchstaben-Präfixe für MessageId
    // um Command Msg aus Portal von Data-Upload Message aus WR zu unterscheiden
    public struct MessageIdPrefix
    {
        public const string CMD = "CMD";
        //public const string DAT = "DAT";
    }
}