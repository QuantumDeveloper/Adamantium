namespace Adamantium.XInput
{
    /// <summary>
    /// These are only valid for wireless, connected devices, with known battery types
    /// The amount of use time remaining depends on the type of device.
    /// </summary>
    public enum BatteryLevel
    {
        Empty = 0,
        Low = 1,
        Medium = 2,
        Full = 3
    }
}