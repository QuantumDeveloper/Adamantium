using System;
using System.Collections;
using System.Collections.Generic;

namespace Adamantium.UI
{
   public class WindowCollection:ICollection
   {
      public WindowCollection()
      {
         windows = new List<Window>();
      }

      private List<Window> windows; 

      public IEnumerator GetEnumerator()
      {
         lock (windows)
         {
            return windows.GetEnumerator();
         }
      }

      public void CopyTo(Window[] array, int index)
      {
         lock (windows)
         {
            windows.CopyTo(array, index);
         }
      }

      internal void Add(Window window)
      {
         lock (windows)
         {
            if (!windows.Contains(window))
            {
               windows.Add(window);
               WindowAdded?.Invoke(this, new WindowEventArgs(window));
            }
         }
      }

      internal void Remove(Window window)
      {
         lock (windows)
         {
            if (windows.Contains(window))
            {
               windows.Remove(window);
               WindowRemoved?.Invoke(this, new WindowEventArgs(window));
            }
         }
      }

      public Window this[int index]
      {
         get
         {
            lock (windows)
            {
               return windows[index];
            }
         }
      }

      void ICollection.CopyTo(Array array, int index)
      {
         ((ICollection) windows).CopyTo(array, index);
      }

      public int Count => windows.Count;

      public object SyncRoot => windows;

      public bool IsSynchronized => true;

      internal event EventHandler<WindowEventArgs> WindowAdded;
      internal event EventHandler<WindowEventArgs> WindowRemoved;
   }

   public class WindowEventArgs : EventArgs
   {
      public Window Window { get; private set; }

      public WindowEventArgs(Window window)
      {
         Window = window;
      }
   }
}
