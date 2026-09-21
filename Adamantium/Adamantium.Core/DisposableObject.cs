using System;

namespace Adamantium.Core
{
   /// <summary>
   /// A disposable object base class.
   /// </summary>
   public abstract class DisposableObject : NamedObject, IDisposable
   {
      /// <summary>
      /// Gets or sets the disposables.
      /// </summary>
      /// <value>The disposables.</value>
      protected DisposeCollector DisposeCollector { get; set; }

      /// <summary>
      /// Initializes a new instance of the <see cref="DisposableObject"/> class.
      /// </summary>
      protected internal DisposableObject()
      {
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="DisposableObject" /> class with an immutable name.
      /// </summary>
      /// <param name="name">The name.</param>
      protected DisposableObject(string name) : base(name)
      {
      }

      /// <summary>
      /// Gets a value indicating whether this instance is disposed.
      /// </summary>
      /// <value>
      /// <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
      /// </value>
      public bool IsDisposed { get; private set; }

      /// <summary>
      /// Gets a value indicating whether this instance is currently disposing.
      /// </summary>
      /// <value>
      /// <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
      /// </value>
      protected internal bool IsDisposing { get; private set; }

      /// <summary>
      /// Occurs when Dispose is called.
      /// </summary>
      public event EventHandler<EventArgs> Disposing;

      /// <summary>
      /// Releases unmanaged and - optionally - managed resources
      /// </summary>
      public void Dispose()
      {
         lock (this)
         {
            if (!IsDisposed)
            {
               IsDisposing = true;

               // Call the disposing event.
               var handler = Disposing;
               handler?.Invoke(this, EventArgs.Empty);

               Dispose(true);
               IsDisposed = true;
            }
         }
      }

      /// <summary>
      /// Disposes of object resources.
      /// </summary>
      /// <param name="disposeManagedResources">If true, managed resources should be
      /// disposed of in addition to unmanaged resources.</param>
      protected virtual void Dispose(bool disposeManagedResources)
      {
         if (disposeManagedResources)
         {
            // Dispose all ComObjects
            DisposeCollector?.Dispose();
            DisposeCollector = null;
         }
      }

      /// <summary>
      /// Adds a disposable object to the list of the objects to dispose.
      /// </summary>
      /// <param name="toDisposeArg">To dispose.</param>
      protected internal T ToDispose<T>(T toDisposeArg)
      {
         if (!ReferenceEquals(toDisposeArg, null))
         {
            if (DisposeCollector == null)
               DisposeCollector = new DisposeCollector();
            return DisposeCollector.Collect(toDisposeArg);
         }
         return default(T);
      }

      /// <summary>
      /// Dispose a disposable object and set the reference to null. Removes this object from the ToDispose list.
      /// </summary>
      /// <param name="objectToDispose">Object to dispose.</param>
      protected internal void RemoveAndDispose<T>(ref T objectToDispose)
      {
         lock (this)
         {
            if (!ReferenceEquals(objectToDispose, null))
               DisposeCollector?.RemoveAndDispose(ref objectToDispose);
         }
      }

      /// <summary>
      /// Removes a disposable object to the list of the objects to dispose.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="toDisposeArg">To dispose.</param>
      protected internal void RemoveToDispose<T>(T toDisposeArg)
      {
         lock (this)
         {
            if (!ReferenceEquals(toDisposeArg, null))
               DisposeCollector?.Remove(toDisposeArg);
         }
      }
   }
}