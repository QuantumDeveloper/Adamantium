namespace Adamantium.Graphics.Core.Presentation
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