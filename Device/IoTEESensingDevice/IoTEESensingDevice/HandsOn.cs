//#define ACCESS_IOT_HUB
//#define PHOTO_UPLOAD
#if (ACCESS_IOT_HUB)
using Microsoft.Azure.Devices.Client;
#endif
#if (PHOTO_UPLOAD)
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.Xaml;

namespace IoTEESensingDevice
{
    public partial class MainPage
    {
        private async void StartHoL()
        {
            StartSenseHatMeasuring();
            StartIoTHubSending();
            await StartPhotoUpload();
        }

        private DispatcherTimer measureTimer;
        private int measureIntervalMSec = 10000;    // default 10 sec
        private List<SenseHATSensorReading> measuredBuffer = new List<SenseHATSensorReading>();
        private void StartSenseHatMeasuring()
        {
            measureTimer = new DispatcherTimer();
            measureTimer.Interval = TimeSpan.FromMilliseconds(measureIntervalMSec);
            measureTimer.Tick += (s, o) =>
            {
                measureTimer.Stop();

                var sensor = senseHat.Sensors;
                sensor.HumiditySensor.Update();
                sensor.PressureSensor.Update();
                sensor.ImuSensor.Update();
                lock (this)
                {
                    var reading = new SenseHATSensorReading();
                    if (sensor.Temperature.HasValue)
                    {
                        reading.Temperature = sensor.Temperature.Value;
                    }
                    if (sensor.Humidity.HasValue)
                    {
                        reading.Humidity = sensor.Humidity.Value;
                    }
                    if (sensor.Pressure.HasValue)
                    {
                        reading.Pressure = sensor.Pressure.Value;
                    }
                    if (sensor.Acceleration.HasValue)
                    {
                        var accel = sensor.Acceleration.Value;
                        reading.AccelX = accel.X;
                        reading.AccelY = accel.Y;
                        reading.AccelZ = accel.Z;
                    }
                    if (sensor.Gyro.HasValue)
                    {
                        var gyro = sensor.Gyro.Value;
                        reading.GyroX = gyro.X;
                        reading.GyroY = gyro.Y;
                        reading.GyroZ = gyro.Z;
                    }
                    if (sensor.MagneticField.HasValue)
                    {
                        var mag = sensor.MagneticField.Value;
                        reading.MagX = mag.X;
                        reading.MagY = mag.Y;
                        reading.MagZ = mag.Z;
                    }
                    reading.MeasuredTime = DateTime.Now;
#if (ACCESS_IOT_HUB)
                    measuredBuffer.Add(reading);
#endif
                    Debug.WriteLine(String.Format("{0}:T={1},H={2},P={3},A={4}:{5}:{6},G={7}:{8}:{9},M={10}:{11}:{12}", reading.MeasuredTime.ToString("yyyyMMdd-hhmmss"), reading.Temperature, reading.Humidity, reading.Pressure, reading.AccelX, reading.AccelY, reading.AccelZ, reading.GyroX, reading.GyroY, reading.GyroZ, reading.MagX, reading.MagY, reading.MagZ));
                }
                measureTimer.Start();
            };
            measureTimer.Start();
        }

#if (ACCESS_IOT_HUB)
        DeviceClient deviceClient;
#endif
        DispatcherTimer uploadTimer;
        bool iotHubConnected = false;
        private void StartIoTHubSending()
        {
#if (ACCESS_IOT_HUB)
            string iotHubConnectionString= "HostName=" + IoTEESensingDevice.IoTHoLConfig.IoTHubEndpoint + ";DeviceId=" + deviceId + ";SharedAccessKey=" + IoTEESensingDevice.IoTHoLConfig.DeviceKey;
            try
            {
                deviceClient = DeviceClient.CreateFromConnectionString(iotHubConnectionString, TransportType.Http1);
                iotHubConnected = true;
                uploadTimer = new DispatcherTimer();
                uploadTimer.Interval = TimeSpan.FromSeconds(IoTHoLConfig.UploadIntervalSec);
                uploadTimer.Tick +=async (o, e) => 
                {
                    await SendMessage();
                };
                uploadTimer.Start();
                ReceiveCommands();
            }
            catch(Exception ex)
            {
                Debug.WriteLine("IoT Connection Failed - " + ex.Message);
            }
#endif
        }
#if (ACCESS_IOT_HUB)
        int sendCount = 0;
        public async Task SendMessage()
        {
            uploadTimer.Stop();
            var sendReadings = new List<SensorReadingForIoT>();
            lock (this)
            {
                foreach (var sr in measuredBuffer)
                {
                    sendReadings.Add(new SensorReadingForIoT()
                    {
                        deviceId = deviceId,
                        AccelX = sr.AccelX,
                        AccelY = sr.AccelY,
                        AccelZ = sr.AccelZ,
                        GyroX = sr.GyroX,
                        GyroY = sr.GyroY,
                        GyroZ = sr.GyroZ,
                        Humidity = sr.Humidity,
                        MagX = sr.MagX,
                        MagY = sr.MagY,
                        MagZ = sr.MagZ,
                        msgId = deviceId + sr.MeasuredTime.ToString("yyyyMMddhhmmssfff"),
                        MeasuredTime = sr.MeasuredTime,
                        Pressure = sr.Pressure,
                        Temperature = sr.Temperature
                    });
                }
                measuredBuffer.Clear();
            }
            var content = Newtonsoft.Json.JsonConvert.SerializeObject(sendReadings);
            var message = new Message(Encoding.UTF8.GetBytes(content));
            try
            {
                if (!iotHubConnected)
                {
                    Debug.WriteLine("IoT Hub seems to not be connected!");
                }
                await deviceClient.SendEventAsync(message);
                Debug.WriteLine("Send[" + sendCount++ + "]" + measuredBuffer.Count + " messages @" + DateTime.Now);
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Exception happned in sending - " + ex.Message);
                iotHubConnected = false;
            }
            uploadTimer.Start();
        }
        public async Task ReceiveCommands()
        {
            Debug.WriteLine("Device waiting for commands from IoT Hub...");
            Message receivedMessage;
            string messageData;
            while (true)
            {
                try
                {
                    receivedMessage = await deviceClient.ReceiveAsync();
                    if (receivedMessage != null)
                    {
                        messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                        Debug.WriteLine("\t{0}> Received message:{1}", DateTime.Now.ToLocalTime(), messageData);
                        if (messageData.ToLower().IndexOf("on") > 0)
                        {
                            motorPin.Write(Windows.Devices.Gpio.GpioPinValue.Low);
                        }
                        else
                        {
                            motorPin.Write(Windows.Devices.Gpio.GpioPinValue.High);
                        }
                        await deviceClient.CompleteAsync(receivedMessage);
                    }
                }
                catch(Exception ex)
                {
                    Debug.WriteLine("Exception Happened in receive waiting - " + ex.Message);
                    iotHubConnected = false;
                }
            }
        }
#endif
        MediaCapture mediaCaptureManager;
        StorageFile photoStorageFile;
        string capturedPhotoFile = "captured.jpg";
#if (PHOTO_UPLOAD)
        CloudBlobContainer photoContainer;
#endif
        string containerName = "photos";
        DispatcherTimer photoUploadTimer;

        private async Task StartPhotoUpload()
        {
#if (PHOTO_UPLOAD)
            mediaCaptureManager = new MediaCapture();
            try
            {
                await InitializeCloudPhotoContainer();
                await mediaCaptureManager.InitializeAsync();
                previewElement.Source = mediaCaptureManager;
                await mediaCaptureManager.StartPreviewAsync();
                photoUploadTimer = new DispatcherTimer();
                photoUploadTimer.Interval = TimeSpan.FromSeconds(IoTHoLConfig.PhotoUploadIntervalSec);
                photoUploadTimer.Tick +=async (s, o) =>
                {
                    await UploadPhoto();
                };
                photoUploadTimer.Start();
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Exception Happen in initialize photo uploading - " + ex.Message);
            }
#endif

        }

#if (PHOTO_UPLOAD)
        private async Task InitializeCloudPhotoContainer()
        {
            var storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=" + IoTEESensingDevice.IoTHoLConfig.StorageAccount + ";AccountKey=" + IoTEESensingDevice.IoTHoLConfig.StorageKey;
            var cloudStorageAccount = CloudStorageAccount.Parse(storageConnectionString);
            var blobClient = cloudStorageAccount.CreateCloudBlobClient();
            photoContainer = blobClient.GetContainerReference(containerName);
            await photoContainer.CreateIfNotExistsAsync();
        }

        private async Task UploadPhoto()
        {
            photoUploadTimer.Stop();
            photoStorageFile = await Windows.Storage.KnownFolders.PicturesLibrary.CreateFileAsync(capturedPhotoFile, CreationCollisionOption.ReplaceExisting);
            var imageProperties = ImageEncodingProperties.CreateJpeg();
            try
            {
                await mediaCaptureManager.CapturePhotoToStorageFileAsync(imageProperties, photoStorageFile);
                var fileName = "device-" + deviceId + "-" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".jpg";
                var blockBlob = photoContainer.GetBlockBlobReference(fileName);
                await blockBlob.UploadFromFileAsync(photoStorageFile);
                Debug.WriteLine(string.Format("Uploaded: {0} at {1}", fileName, DateTime.Now.ToString("yyyy/MM/dd - hh:mm:ss")));
            }
            catch (Exception ex)
            {
                Debug.Write(ex.Message);
            }
            photoUploadTimer.Start();
        }
#endif
    }

    public class SenseHATSensorReading
    {
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public double Pressure { get; set; }
        public double AccelX { get; set; }
        public double AccelY { get; set; }
        public double AccelZ { get; set; }
        public double GyroX { get; set; }
        public double GyroY { get; set; }
        public double GyroZ { get; set; }
        public double MagX { get; set; }
        public double MagY { get; set; }
        public double MagZ { get; set; }
        public DateTime MeasuredTime { get; set; }
    }

    public class SensorReadingForIoT
    {
        public string deviceId { get; set; }
        public string msgId { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public double Pressure { get; set; }
        public double AccelX { get; set; }
        public double AccelY { get; set; }
        public double AccelZ { get; set; }
        public double GyroX { get; set; }
        public double GyroY { get; set; }
        public double GyroZ { get; set; }
        public double MagX { get; set; }
        public double MagY { get; set; }
        public double MagZ { get; set; }
        public DateTime MeasuredTime { get; set; }
    }
}
