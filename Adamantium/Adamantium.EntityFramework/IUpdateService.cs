using Adamantium.Core;
using Adamantium.Engine.Core;

namespace Adamantium.EntityFramework;

public interface IUpdateService : IContentable
{
    /// <summary>
    /// This method is called when this application component is updated.
    /// </summary>
    /// <param name="gameTime">The current timing.</param>
    void Update(AppTime gameTime);

    /// <summary>
    /// Gets a value indicating whether the application component's Update method should be called by <see cref="SystemManager.Update"/>.
    /// </summary>
    /// <value><c>true</c> if update is enabled; otherwise, <c>false</c>.</value>
    bool Enabled { get; set; }

    /// <summary>
    /// Gets the update order relative to other game components. Lower values are updated first.
    /// </summary>
    /// <value>The update order.</value>
    /// <remarks>This property is valid on if <see cref="ExecutionType"/> is <see cref="ExecutionType.Sync"/>. Otherwise priority will bw ignored</remarks>
    int UpdatePriority { get; set; }

    /// <summary>
    /// Gets or sets the way how this system will be processed in Update phase
    /// </summary>
    ExecutionType UpdateExecutionType { get; set; }
}