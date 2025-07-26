using System;
using Adamantium.Game.Core;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Events;
using Adamantium.UI.Rendering;

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

    public void AttachContextAndInitialize(IUIContext context)
    {
        UIContext = context;
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
    public IUIContext UIContext { get; private set; }
    public IntPtr SurfaceHandle { get; }
    public double Left { get; set; }
    public double Top { get; set; }
    public string Title { get; set; }
    public MSAALevel MSAALevel { get; set; }
    public WindowState State { get; set; }
    public IWindowRenderer DefaultRenderer { get; set; }
    public IWindowRenderer Renderer { get; set; }
    
    public GameOutput RootWindow { get; set; }

    public bool ShouldDisplayWindow { get; }

    public IDrawingContext GetDrawingContext()
    {
        if (Renderer != null)
        {
            return Renderer.DrawingContext;
        }

        if (DefaultRenderer != null)
            return DefaultRenderer.DrawingContext;

        throw new ArgumentException("Window does not contain renderer and could not return DrawingContext");
    }

    public Vector2 ScreenToClient(Vector2 p)
    {
        throw new NotImplementedException();
    }

    public Vector2 ClientToScreen(Vector2 p)
    {
        throw new NotImplementedException();
    }

    public event SizeChangedEventHandler ClientSizeChanged;
    public event EventHandler<WindowClosingEventArgs> Closing;
    public event MSAALeveChangedHandler MSAALevelChanged;
    public event StateChangedHandler StateChanged;
    public event EventHandler<EventArgs> Closed;
    public event EventHandler<WindowRendererChangedEventArgs> RendererChanged;
    public event EventHandler<EventArgs> SourceInitialized;
    
}