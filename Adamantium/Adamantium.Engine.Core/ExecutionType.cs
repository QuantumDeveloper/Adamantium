namespace Adamantium.Engine.Core
{
    /// <summary>
    /// Defines on of the following execution types for <see cref="ISystem"/>
    /// </summary>
    public enum ExecutionType
   {
      /// <summary>
      /// Synchronous execution (<see cref="ISystem"/> will be executed in series with other <see cref="ISystem"/>s in the main thread)
      /// </summary>
      Sync = 0,

      /// <summary>
      /// Asynchronous execution (<see cref="ISystem"/> will be executed in parallel with other <see cref="ISystem"/>s in the separate thread)
      /// </summary>
      Async = 1
   }
}
