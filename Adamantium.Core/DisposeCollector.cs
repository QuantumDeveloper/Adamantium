using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Core
{
    public class DisposeCollector : DisposableBase
    {
        private List<object> disposables;

        public DisposeCollector()
        {
            disposables = new List<object>();
        }

        public int Count => disposables.Count;

        public T Collect<T>(T toDispose)
        {
            if (!(toDispose is IDisposable || toDispose is IntPtr))
                throw new ArgumentException("Argument must be IDisposable or IntPtr");

            // Check memory alignment
            if (toDispose is IntPtr)
            {
                var memoryPtr = (IntPtr)(object)toDispose;
                if (!Utilities.IsMemoryAligned(memoryPtr))
                    throw new ArgumentException("Memory pointer is invalid. Memory must have been allocated with Utilties.AllocateMemory");
            }

            if (!Equals(toDispose, default(T)))
            {
                if (disposables == null)
                    disposables = new List<object>();

                if (!disposables.Contains(toDispose))
                {
                    disposables.Add(toDispose);
                }
            }
            return toDispose;
        }

        public void RemoveAndDispose<T>(ref T objectToDispose)
        {
            if (Remove(objectToDispose))
            {
                if (objectToDispose is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                else
                {
                    var obj = (object)objectToDispose;
                    var ptr = (IntPtr)obj;
                    Utilities.FreeMemory(ptr);
                }
            }
        }

        public bool Remove<T>(T toDisposeArg)
        {
            return disposables.Remove(toDisposeArg);
        }

        public void DisposeAndClear(bool disposeManagedResources = true)
        {
            if (disposables == null)
                return;

            for (int i = disposables.Count - 1; i >= 0; i--)
            {
                var objectToDispose = disposables[i];

                if (objectToDispose is IDisposable disposable)
                {
                    if (disposeManagedResources)
                    {
                        disposable.Dispose();
                    }
                }
                else
                {
                    Utilities.FreeMemory((IntPtr)objectToDispose);
                }

                disposables.RemoveAt(i);
            }
            disposables.Clear();
        }

        protected override void Dispose(bool disposeManaged)
        {
            DisposeAndClear(disposeManaged);
        }
    }
}
