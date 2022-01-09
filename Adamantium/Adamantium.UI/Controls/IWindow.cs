using Adamantium.EntityFramework.ComponentsBasics;
using System;
using System.Collections.Generic;
using System.Text;
using Adamantium.Engine.Graphics;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls;

public interface IWindow : IRootVisualComponent, IFrameworkComponent
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
        
    WindowState State { get; set; }

    public void Render();

    public void Update();
        
    event SizeChangedEventHandler ClientSizeChanged;
    event EventHandler<WindowClosingEventArgs> Closing;
    event EventHandler<EventArgs> Closed;
    event MSAALeveChangedHandler MSAALevelChanged;
    event StateChangedHandler StateChanged;
}