using System;
using System.Runtime.InteropServices;

namespace Adamantium.XInput
{
    public class XBoxController
    {
        public const int MaxControllers = 4;
        private readonly UserIndex _userIndex;
        private static readonly IXinput _xinput;

        public XBoxController(UserIndex userIndex = UserIndex.Any)
        {
            if (_xinput == null)
            {
                throw new NotSupportedException("XInput 1.4 dll was not found");
            }

            _userIndex = userIndex;
        }

        static XBoxController()
        {
            if (LoadLibrary("xinput1_4.dll") != IntPtr.Zero)
            {
                _xinput = new XInput14();
            }
        }

        public UserIndex UserIndex => _userIndex;

        public bool IsConnected => _xinput.XInputGetState((int) _userIndex, out var state) == 0;

        /// <summary>
        /// Retrieves the current state of the specified controller.
        /// </summary>
        /// <returns>current controller state</returns>
        public State GetState()
        {
            _xinput.XInputGetState((int)_userIndex, out var state);
            return state;
        }

        /// <summary>
        /// Sends data to a connected controller. This function is used to activate the vibration function of a controller.
        /// </summary>
        /// <param name="vibration"><see cref="Vibration"/> structure containing vibration information</param>
        public void SetVibration(Vibration vibration)
        {
            _xinput.XInputSetState((int) _userIndex, vibration);
        }

        /// <summary>
        /// Sets the reporting state of XInput.
        /// </summary>
        /// <param name="enable">If enable is FALSE, XInput will only send neutral data in response to <see cref="GetState"/> (all buttons up, axes centered, and triggers at 0). 
        /// <see cref="SetVibration"/> calls will be registered but not sent to the device. Sending any value other than FALSE will restore reading and writing functionality to normal.</param>
        public void SetReportingState(bool enable)
        {
            _xinput.XInputEnable(enable);
        }

        /// <summary>
        /// Retrieves the battery type and charge status of a wireless controller.
        /// </summary>
        /// <param name="deviceType">Specifies which device associated with this user index should be queried. Must be <see cref="BatteryDeviceType.Gamepad"/> or <see cref="BatteryDeviceType.Headset"/></param>
        /// <returns><see cref="BatteryInformation"/></returns>
        public BatteryInformation GetBatteryInformation(BatteryDeviceType deviceType)
        {
            _xinput.XInputGetBatteryInformation((int)_userIndex, deviceType, out var batteryDeviceType);
            return batteryDeviceType;
        }

        /// <summary>
        /// Retrieves the capabilities and features of a connected controller.
        /// </summary>
        /// <param name="deviceQueryType">Input flags that identify the controller type. If this value is <see cref="DeviceQueryType.Any"/>, then the capabilities of all controllers connected to the system are returned. Currently, only one value is supported:</param>
        /// <param name="capabilities"></param>
        /// <returns>True if capabilities were successfully received, otherwise - false. In out  param returns <see cref="Capabilities"/> struct with current capabilities</returns>
        public bool GetCapabilities(DeviceQueryType deviceQueryType, out Capabilities capabilities)
        {
            return _xinput.XInputGetCapabilities((int) _userIndex, deviceQueryType, out capabilities) == 0;
        }

        /// <summary>
        /// Retrieves a gamepad input event.
        /// </summary>
        /// <returns>Result and <see cref="Keystroke"/> as output</returns>
        public Result GetKeyStroke(out Keystroke keystroke)
        {
            return (Result)_xinput.XInputGetKeystroke((int) _userIndex, (int)DeviceQueryType.Any, out keystroke);
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, EntryPoint = "LoadLibrary", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
    }
}