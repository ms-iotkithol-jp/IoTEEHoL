using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Gpio;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 を参照してください

namespace IoTEESensingDevice
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
        }

        private string deviceId = "";
        private ISenseHat senseHat;

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            FixDeviceId();
            if (deviceId == "minwinpc")
            {
                Debug.Write("Please set deviceID or unique machine name");
                throw new ArgumentOutOfRangeException("Please set deviceID or unique machine name");
            }
            Debug.WriteLine("Fixed - DeviceId:" + deviceId);
            InitGpio();

            senseHat = await SenseHatFactory.GetSenseHat();
            LedArrayOff();
            senseHat.Display.Update();

            StartHoL();
        }

        private void LedArrayOff()
        {
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    senseHat.Display.Screen[x, y] = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                }
            }
            senseHat.Display.Update();
        }

        private void FixDeviceId()
        {
            foreach (var hn in Windows.Networking.Connectivity.NetworkInformation.GetHostNames())
            {
                IPAddress ipAddr;
                if (!hn.DisplayName.EndsWith(".local") && !IPAddress.TryParse(hn.DisplayName, out ipAddr))
                {
                    deviceId = hn.DisplayName;
                    break;
                }
            }
        }

        private GpioPin motorPin;
        private int motorGpIoPin = 18; // Pin 12 6th from Right Top(0)
        private void InitGpio()
        {
            var gpio = GpioController.GetDefault();
            motorPin = gpio.OpenPin(motorGpIoPin);
            motorPin.SetDriveMode(GpioPinDriveMode.Output);
        }
    }
}
