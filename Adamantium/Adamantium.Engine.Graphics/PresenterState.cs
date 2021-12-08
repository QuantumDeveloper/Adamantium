namespace Adamantium.Engine.Graphics
{
    public enum PresenterState
    {
        Unknown,
        Success,
        Suboptimal,
        OutOfDate,
        OutOfHostMemory,
        OutOfDeviceMemory,
        DeviceLost,
        SurfaceLost,
        FullScreenExclusiveModeLost
    }
}