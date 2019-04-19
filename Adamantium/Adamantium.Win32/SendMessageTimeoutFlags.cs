namespace Adamantium.Win32
{
    public enum SendMessageTimeoutFlags
    {
        Normal = 0x0,
        Block = 0x1,
        AbortIfHung = 0x2,
        NoTimeoutIfNotHung = 0x8,
        ErrorOnExit = 0x20
    }
}