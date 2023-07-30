using System;
using Adamantium.Engine.Graphics;
using Adamantium.Game.Core;
using Adamantium.Mathematics;
using Adamantium.UI;
using Adamantium.UI.Controls;
using Adamantium.UI.Events;
using Adamantium.UI.Media;
using Adamantium.UI.Rendering;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.Game;

public class VirtualWindow : ContentControl, IVirtualWindow
{
    public Vector2 PointToClient(Vector2 point)
    {
        throw new NotImplementedException();
    }

    public Vector2 PointToScreen(Vector2 point)
    {
        throw new NotImplementedException();
    }

    public void Show()
    {
        Visibility = Visibility.Visible;
    }

    public void Close()
    {
        IsClosed = true;
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }

    public bool IsActive { get; }
    public IntPtr Handle { get; }
    public bool IsClosed { get; protected set; }
    public double ClientWidth { get; set; }
    public double ClientHeight { get; set; }
    public IntPtr SurfaceHandle { get; }
    public double Left { get; set; }
    public double Top { get; set; }
    public string Title { get; set; }
    public MSAALevel MSAALevel { get; set; }
    public WindowState State { get; set; }
    public IWindowRenderer DefaultRenderer { get; set; }
    public IWindowRenderer Renderer { get; set; }
    
    public GameOutput RootWindow { get; set; }
    public DrawingContext GetDrawingContext()
    {
        if (Renderer != null)
        {
            return Renderer.DrawingContext;
        }

        if (DefaultRenderer != null)
            return DefaultRenderer.DrawingContext;

        throw new ArgumentException("Window does not contain renderer and could not return DrawingContext");
    }

    public event SizeChangedEventHandler ClientSizeChanged;
    public event EventHandler<WindowClosingEventArgs> Closing;
    public event MSAALeveChangedHandler MSAALevelChanged;
    public event StateChangedHandler StateChanged;
    public event EventHandler<EventArgs> Closed;
    public event EventHandler<WindowRendererChangedEventArgs> RendererChanged;
    public event EventHandler<EventArgs> SourceInitialized;
    
}