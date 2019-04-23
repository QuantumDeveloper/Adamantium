using System;

namespace Adamantium.XInput
{
    /// <summary>
    /// Interface for XInput to allow extensibility and support for new XInput versions in future
    /// </summary>
    internal interface IXinput
    {
        int XInputGetState(int userIndex, out State state);

        int XInputSetState(int userIndex, Vibration vibration);

        int XInputGetCapabilities(int userIndex, DeviceQueryType flags, out Capabilities capabilities);

        void XInputEnable(bool enable);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userIndex">Index of the gamer associated with the device</param>
        /// <param name="renderDeviceId">Windows Core Audio device ID string for render (speakers)</param>
        /// <param name="renderCount">Size of render device ID string buffer (in wide-chars)</param>
        /// <param name="captureDeviceId">Windows Core Audio device ID string for capture (microphone)</param>
        /// <param name="captureCount">Size of capture device ID string buffer (in wide-chars)</param>
        /// <returns></returns>
        int XInputGetAudioDeviceIds(int userIndex, IntPtr renderDeviceId, IntPtr renderCount, IntPtr captureDeviceId, IntPtr captureCount);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userIndex">Index of the gamer associated with the device</param>
        /// <param name="deviceType">Which device on this user index</param>
        /// <param name="batteryInformation">Contains the level and types of batteries</param>
        /// <returns></returns>
        int XInputGetBatteryInformation(
            int userIndex,
            BatteryDeviceType deviceType,
            out BatteryInformation batteryInformation);

        /// <summary>
        /// Retrieves a gamepad input event.
        /// </summary>
        /// <param name="userIndex">Index of the gamer associated with the device</param>
        /// <param name="reserved">Reserved for future use</param>
        /// <param name="keystroke">Pointer to an <see cref="Keystroke"/> structure that receives an input event.</param>
        /// <returns></returns>
        int XInputGetKeystroke(int userIndex, int reserved, out Keystroke keystroke);
    }
}