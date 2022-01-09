using Adamantium.UI.Controls;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Adamantium.UI;

public class WindowCollection:ICollection
{
   public WindowCollection()
   {
      windows = new List<IWindow>();
   }

   private List<IWindow> windows; 

   public IEnumerator GetEnumerator()
   {
      lock (windows)
      {
         return windows.GetEnumerator();
      }
   }

   public void CopyTo(IWindow[] array, int index)
   {
      lock (windows)
      {
         windows.CopyTo(array, index);
      }
   }

   public void Add(IWindow window)
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

   public void Remove(IWindow window)
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

   public IWindow this[int index]
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
   public IWindow Window { get; private set; }

   public WindowEventArgs(IWindow window)
   {
      Window = window;
   }
}