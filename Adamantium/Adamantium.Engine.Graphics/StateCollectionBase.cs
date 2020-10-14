using System;
using Adamantium.Core;

namespace Adamantium.Engine.Graphics
{
   /// <summary>
   /// Base collection for Graphics device states (BlendState, DepthStencilState, RasterizerState).
   /// </summary>
   /// <typeparam name="T">Type of the state.</typeparam>
   public abstract class StateCollectionBase<T>:ComponentCollection<T> where T : NamedObject
   {
      /// <summary>
      /// An allocator of state.
      /// </summary>
      /// <param name="device">The device.</param>
      /// <param name="name">The name of the state to create.</param>
      /// <returns>An instance of T or null if not supported.</returns>
      public delegate T StateAllocatorDelegate(string name);

      /// <summary>
      /// Initializes a new instance of the <see cref="StateCollectionBase{T}" /> class.
      /// </summary>
      /// <param name="device">The device.</param>
      protected StateCollectionBase()
      {
      }

      /// <summary>
      /// Sets this callback to create a state when a state with a particular name is not found.
      /// </summary>
      public StateAllocatorDelegate StateAllocatorCallback;

      /// <summary>
      /// Registers the specified state.
      /// </summary>
      /// <param name="state">The state.</param>
      /// <remarks>
      /// The name of the state must be defined.
      /// </remarks>
      public void Register(T state)
      {
         Add(state);
      }

      protected override T TryToGetOnNotFound(string name)
      {
         return StateAllocatorCallback?.Invoke(name);
      }
   }
}
