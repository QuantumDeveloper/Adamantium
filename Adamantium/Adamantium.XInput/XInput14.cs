using System;
using System.Runtime.InteropServices;

namespace Adamantium.XInput
{
    internal class XInput14 : IXinput
    {
        public int XInputGetState(int userIndex, out State state)
        {
            return XInputNative14.XInputGetState(userIndex, out state);
        }

        public int XInputSetState(int userIndex, Vibration vibration)
        {
            return XInputNative14.XInputSetState(userIndex, vibration);
        }

        public int XInputGetCapabilities(int userIndex, DeviceQueryType flags, out Capabilities capabilities)
        {
            return XInputNative14.XInputGetCapabilities(userIndex, flags, out capabilities);
        }

        public void XInputEnable(bool enable)
        {
            XInputNative14.XInputEnable(enable);
        }

        public int XInputGetAudioDeviceIds(
            int userIndex,
            IntPtr renderDeviceId,
            IntPtr renderCount,
            IntPtr captureDeviceId,
            IntPtr captureCount)
        {
            return XInputNative14.XInputGetAudioDeviceIds(
                userIndex,
                renderDeviceId,
                renderCount,
                captureDeviceId,
                captureCount);
        }

        public int XInputGetBatteryInformation(int userIndex, BatteryDeviceType deviceType, out BatteryInformation batteryInformation)
        {
            return XInputNative14.XInputGetBatteryInformation(userIndex, deviceType, out batteryInformation);
        }

        public int XInputGetKeystroke(int userIndex, int reserved, out Keystroke keystroke)
        {
            return XInputNative14.XInputGetKeystroke(userIndex, reserved, out keystroke);
        }

        private static class XInputNative14
        {
            [DllImport("xinput1_4.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "XInputGetState")]
            public static extern int XInputGetState(int userIndex, out State state);

            public static unsafe int XInputSetState(int userIndex, Vibration vibration)
            {
                return XInputSetState(userIndex, &vibration);
            }

            [DllImport("xinput1_4.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "XInputSetState")]
            public static extern unsafe int XInputSetState(int userIndex, void* vibration);

            [DllImport("xinput1_4.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "XInputGetCapabilities")]
            public static extern int XInputGetCapabilities(int userIndex, DeviceQueryType flags, out Capabilities capabilities);

            [DllImport("xinput1_4.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "XInputEnable")]
            public static extern void XInputEnable(bool enable);

            [DllImport("xinput1_4.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "XInputGetAudioDeviceIds")]
            public static extern int XInputGetAudioDeviceIds(int userIndex, IntPtr renderDeviceId, IntPtr renderCount, IntPtr captureDeviceId, IntPtr captureCount);

            [DllImport("xinput1_4.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "XInputGetBatteryInformation")]
            public static extern int XInputGetBatteryInformation(int userIndex, BatteryDeviceType deviceType, out BatteryInformation batteryInformation);

            [DllImport("xinput1_4.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "XInputGetKeystroke")]
            public static extern int XInputGetKeystroke(int userIndex, int reserved, out Keystroke keystroke);
        }
    }
}