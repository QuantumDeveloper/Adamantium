namespace Adamantium.Engine
{
    /// <summary>
    /// Describes possible <see cref="GameWindow"/> types
    /// </summary>
    public enum GameWindowType
    {
        /// <summary>
        /// <see cref="GameWindow"/> is using hardware swapchain
        /// </summary>
        SwapchainWindow = 0,

        /// <summary>
        /// <see cref="GameWindow"/> is using shared texture as swapchain
        /// </summary>
        Rendertarget = 1
    }
}
