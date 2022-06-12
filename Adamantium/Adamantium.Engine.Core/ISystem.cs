using Adamantium.Core;

namespace Adamantium.Engine.Core
{
    /// <summary>
    /// Defines an interface for abstract system.
    /// </summary>
    public interface ISystem : IName, IIdentifiable
    {
        /// <summary>
        /// This method is called when the component is added to the game.
        /// </summary>
        /// <remarks>
        /// This method can be used for tasks like querying for services the component needs and setting up non-graphics resources.
        /// </remarks>
        void Initialize();
        void Update(AppTime gameTime);
    }
}
