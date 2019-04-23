using System;

namespace Adamantium.Engine.Core
{
   /// <summary>
   /// An interface that is called by <see cref="SystemManager.Update"/>.
   /// </summary>
   public interface IUpdatable
   {
      /// <summary>
      /// Starts application update
      /// </summary>
      void BeginUpdate();

      /// <summary>
      /// This method is called when this application component is updated.
      /// </summary>
      /// <param name="gameTime">The current timing.</param>
      void Update(IGameTime gameTime);

      /// <summary>
      /// Gets a value indicating whether the application component's Update method should be called by <see cref="SystemManager.Update"/>.
      /// </summary>
      /// <value><c>true</c> if update is enabled; otherwise, <c>false</c>.</value>
      bool Enabled { get; }

      /// <summary>
      /// Gets the update order relative to other game components. Lower values are updated first.
      /// </summary>
      /// <value>The update order.</value>
      /// <remarks>This property is valid on if <see cref="ExecutionType"/> is <see cref="ExecutionType.Sync"/>. Otherwise priority will bw ignored</remarks>
      int UpdatePriority { get; }

      /// <summary>
      /// Gets or sets the way how this system will be processed in Update phase
      /// </summary>
      ExecutionType UpdateExecutionType { get; set; }

      /// <summary>
      /// Occurs when the <see cref="UpdateExecutionType"/> property changes.
      /// </summary>
      event EventHandler<ExecutionTypeEventArgs> UpdateExecutionTypeChanged;

      /// <summary>
      /// Occurs when the <see cref="Enabled"/> property changes.
      /// </summary>
      event EventHandler<StateEventArgs> EnabledChanged;

      /// <summary>
      /// Occurs when the <see cref="UpdatePriority"/> property changes.
      /// </summary>
      event EventHandler<PriorityEventArgs> UpdatePriorityChanged;
   }
}
