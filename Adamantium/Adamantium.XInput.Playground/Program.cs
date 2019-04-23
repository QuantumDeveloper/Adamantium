using System;
using System.Threading;

namespace Adamantium.XInput.Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Start XInput playground");
            // Initialize XInput
            var controllers = new[] { new XBoxController(UserIndex.One), new XBoxController(UserIndex.Two), new XBoxController(UserIndex.Three), new XBoxController(UserIndex.Four) };
            // Get 1st controller available
            XBoxController controller = null;
            foreach (var selectControler in controllers)
            {
                if (selectControler.IsConnected)
                {
                    controller = selectControler;
                    break;
                }
            }

            if (controller == null)
            {
                Console.WriteLine("No XInput controller installed");
            }
            else
            {

                Console.WriteLine("Found a XInput controller available");
                Console.WriteLine("Press buttons on the controller to display events or escape key to exit... ");

                // Poll events from joystick
                var previousState = controller.GetState();
                while (!IsKeyPressed(ConsoleKey.Escape))
                {
                    if (IsKeyPressed(ConsoleKey.Escape))
                    {
                        break;
                    }
                    if (controller.IsConnected)
                    {
                        var state = controller.GetState();

                        //if (previousState.PacketNumber != state.PacketNumber)
                        //Console.WriteLine(state.Gamepad);

                        var battery = controller.GetBatteryInformation(BatteryDeviceType.Gamepad);
                        var res = controller.GetCapabilities(DeviceQueryType.Any, out var capabilities);
                        var errCode = controller.GetKeyStroke(out var keystroke);
                        if (errCode == Result.ERROR_SUCCESS)
                        {
                            Console.WriteLine(keystroke);
                        }

                        if (state.Gamepad.Buttons == GamepadButton.A)
                        {
                            controller.SetVibration(new Vibration() { LeftMotorSpeed = 65000, RightMotorSpeed = 60000 });
                        }
                        else
                        {
                            controller.SetVibration(new Vibration() { LeftMotorSpeed = 0, RightMotorSpeed = 0 });
                        }
                        if (state.Gamepad.Buttons == GamepadButton.B)
                        {
                            controller.SetVibration(new Vibration() { LeftMotorSpeed = 65000, RightMotorSpeed = 0 });
                        }
                        else
                        {
                            controller.SetVibration(new Vibration() { LeftMotorSpeed = 0, RightMotorSpeed = 0 });
                        }

                        Thread.Sleep(10);
                        previousState = state;
                    }
                }
            }
            Console.WriteLine("End XInput playground");
        }

        /// <summary>
        /// Determines whether the specified key is pressed.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        ///   <c>true</c> if the specified key is pressed; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsKeyPressed(ConsoleKey key)
        {
            return Console.KeyAvailable && Console.ReadKey(true).Key == key;
        }
    }
}
