using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IoTEESensingDevice
{
    public static class IoTHoLConfig
    {
        public static readonly string IoTHubEndpoint = "[IoT Hub Endpoint URL]";
        public static readonly string DeviceKey = "[Device Key]";
        public static int UploadIntervalSec = 120;

        public static readonly string StorageAccount = "[Account Name]";
        public static readonly string StorageKey = "[Account Key]";
        public static int PhotoUploadIntervalSec = 20;
    }
}
