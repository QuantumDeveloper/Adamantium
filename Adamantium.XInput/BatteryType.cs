namespace Adamantium.XInput
{
    public enum BatteryType : byte
    {
        /// <summary>
        /// This device is not connected
        /// </summary>
        Disconnected = 0,

        /// <summary>
        /// Wired device, no battery
        /// </summary>
        Wired = 1,

        /// <summary>
        /// Alkaline battery source
        /// </summary>
        Alkaline = 2,

        /// <summary>
        /// Nickel Metal Hydride battery source
        /// </summary>
        Nimh = 3,

        /// <summary>
        /// Cannot determine the battery type
        /// </summary>
        Unknown = 255
    }
}