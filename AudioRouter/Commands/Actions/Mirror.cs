using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using CSCore.SoundOut;
using CSCore.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AudioRouter.Commands.Actions
{
    internal class Mirror
    {
        public static int Run(Formats.Mirror options)
        {
            // is latency valid
            if (options.Latency < 1)
            {
                Console.WriteLine("Error: Latency can not be negative or zero.");
                return -2;
            }

            using (var deviceEnum = new MMDeviceEnumerator())
            {
                // enumerate devices
                using (var devices = deviceEnum.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active))
                {
                    // devices
                    var defaultDevice = deviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                    // get default devices
                    var isSourceDeviceDefaultDevice = String.IsNullOrEmpty(options.Source);
                    var sourceDevice = isSourceDeviceDefaultDevice ? defaultDevice : devices.Where(d => d.DeviceID == options.Source).First();
                    var destinationDevice = devices.Where(d => d.DeviceID == options.Destination).FirstOrDefault();

                    // prevent source and target device as equal
                    if (sourceDevice == destinationDevice)
                    {
                        Console.WriteLine("Error: Source device and target device is the same.");
                        return -1;
                    }

                    // prevent unknown render device
                    if (destinationDevice == null)
                    {
                        Console.WriteLine("Error: Failed to select render device.");
                        return -3;
                    }

                    Console.WriteLine($"Source: {sourceDevice.FriendlyName}{(isSourceDeviceDefaultDevice?" Default":"")}");
                    Console.WriteLine($"Destination: {destinationDevice.FriendlyName}");

                    // make sure the devices stay active
                    deviceEnum.DeviceStateChanged += (object sender, DeviceStateChangedEventArgs e) =>
                    {
                        if (e.DeviceState == DeviceState.Active) return;

                        // make sure it is not one of the used devices
                        if (e.DeviceId == destinationDevice.DeviceID/* || e.DeviceId == sourceDevice.DeviceID*/)
                        {
                            Console.WriteLine("Error: Destination device no longer valid! Please try reconnecting the device, then restart the application.");
                            Environment.Exit(-4);
                        }
                        //else if (e.DeviceId == sourceDevice.DeviceID) { }//Do we need this?
                    };

                    // start capture
                    var capture = new WasapiLoopbackCapture { Device = sourceDevice };
                    var render = new WasapiOut() { Latency = options.Latency, Device = destinationDevice };

                    // initialize apis
                    capture.Initialize();
                    render.Initialize(new SoundInSource(capture));

                    // start capture
                    capture.Start();
                    render.Play();

                    Console.WriteLine("Mirroring audio stream...");

                    // keep capture running
                    while (true)
                    {
                        if(isSourceDeviceDefaultDevice)
                        {
                            var newDefaultDevice = deviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                            if(newDefaultDevice.DeviceID != defaultDevice.DeviceID)
                            {
                                Console.WriteLine($"Default device changed from {defaultDevice.FriendlyName} to {newDefaultDevice.FriendlyName}");
                                capture.Stop();
                                capture.Device = newDefaultDevice;
                                capture.Initialize();
                                capture.Start();
                                sourceDevice = defaultDevice = newDefaultDevice;
                                Console.WriteLine($"Source: {sourceDevice.FriendlyName}{(isSourceDeviceDefaultDevice ? " Default" : "")}");
                                Console.WriteLine($"Destination: {destinationDevice.FriendlyName}");
                                Console.WriteLine("Mirroring audio stream...");
                            }
                        }

                        if (render.PlaybackState == PlaybackState.Playing) Thread.Sleep(50);
                        render.Play();
                    }
                }
            }
        }
    }
}
