namespace Adamantium.Engine
{
    /// <summary>
    /// Define type of <see cref="GameContext"/>
    /// </summary>
    public enum GameContextType
    {
        /// <summary>
        /// Game running on desktop in a <see cref="System.Windows.Forms.Form"/> control or in <see cref="System.Windows.Forms.Control"/>
        /// </summary>
        WinForms,

        Window,

        /// <summary>
        /// Game running on Desktop using <see cref="RenderTargetPanel"/> in Adamantium window
        /// </summary>
        RenderTargetPanel
    }
}
