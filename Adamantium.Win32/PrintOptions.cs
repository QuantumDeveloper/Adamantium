namespace Adamantium.Win32
{
    public enum PrintOptions : int
    {
        /// <summary>
        /// Draws the window only if it is visible.
        /// </summary>
        CheckVisible,

        /// <summary>
        /// Draws all visible children windows.
        /// </summary>
        Children,
        
        /// <summary>
        /// Draws the client area of the window.
        /// </summary>
        Client,

        /// <summary>
        /// Erases the background before drawing the window.
        /// </summary>
        EraseBkgnd,
        
        /// <summary>
        /// Draws the nonclient area of the window.
        /// </summary>
        NonClient,

        /// <summary>
        /// Draws all owned windows.
        /// </summary>
        Owned

    }
}