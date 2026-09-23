namespace Adamantium.Game.Core
{
    /// <summary>
    /// Describes possible <see cref="UniverseOutput"/> types
    /// </summary>
    public enum GameWindowType
    {
        /// <summary>
        /// <see cref="UniverseOutput"/> is using hardware swapchain
        /// </summary>
        SwapchainWindow = 0,

        /// <summary>
        /// <see cref="UniverseOutput"/> is using shared texture as swapchain
        /// </summary>
        RenderTarget = 1
    }
}
