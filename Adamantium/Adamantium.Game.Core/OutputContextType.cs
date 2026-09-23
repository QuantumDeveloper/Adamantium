using Adamantium.Game.Core;

namespace Adamantium.Game
{
    /// <summary>
    /// Define type of <see cref="OutputContext"/>
    /// </summary>
    public enum OutputContextType
    {
        Window,

        /// <summary>
        /// Game running on Desktop using <see cref="RenderTargetPanel"/> in Adamantium window
        /// </summary>
        RenderTargetPanel,
        
        Custom
    }
}
