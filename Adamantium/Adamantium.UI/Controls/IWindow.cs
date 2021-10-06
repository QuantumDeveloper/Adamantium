using Adamantium.EntityFramework.ComponentsBasics;
using System;
using System.Collections.Generic;
using System.Text;
using Adamantium.Engine.Graphics;

namespace Adamantium.UI.Controls
{
    public interface IWindow : IRootVisual, IFrameworkComponent
    {
        void Show();
        void Close();
        void Hide();
        
        bool IsActive { get; }

        IntPtr Handle { get; }
        bool IsClosed { get; }

        Double ClientWidth { get; set; }

        Double ClientHeight { get; set; }

        IntPtr SurfaceHandle { get; }
        
        double Left { get; set; }
        
        double Top { get; set; }
        
        string Title { get; set; }
        
        MSAALevel MSAALevel { get; set; }

        public void Render();
        
        event SizeChangedEventHandler ClientSizeChanged;
        event EventHandler<WindowClosingEventArgs> Closing;
        event EventHandler<EventArgs> Closed;
    }
}
