using System;

namespace Adamantium.Core
{
    public abstract class DisposableBase : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public event EventHandler Disposing;

        public event EventHandler Disposed;

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected abstract void Dispose(bool disposeManaged);

        ~DisposableBase()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            DisposeInternal(true);
        }

        private void DisposeInternal(bool disposeManaged)
        {
            if (!IsDisposed)
            {
                Disposing?.Invoke(this, EventArgs.Empty);

                Dispose(disposeManaged);
                GC.SuppressFinalize(this);

                IsDisposed = true;

                Disposed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
