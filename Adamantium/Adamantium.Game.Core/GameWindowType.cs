namespace Adamantium.Game.Core
{
    /// <summary>
    /// Describes possible <see cref="GameOutput"/> types
    /// </summary>
    public enum GameWindowType
    {
        /// <summary>
        /// <see cref="GameOutput"/> is using hardware swapchain
        /// </summary>
        SwapchainWindow = 0,

        /// <summary>
        /// <see cref="GameOutput"/> is using shared texture as swapchain
        /// </summary>
        RenderTarget = 1
    }
}
