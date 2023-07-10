using Adamantium.Game.Core;

namespace Adamantium.Game
{
    /// <summary>
    /// Define type of <see cref="GameContext"/>
    /// </summary>
    public enum GameContextType
    {
        Window,

        /// <summary>
        /// Game running on Desktop using <see cref="RenderTargetPanel"/> in Adamantium window
        /// </summary>
        RenderTargetPanel,
        
        Custom
    }
}
