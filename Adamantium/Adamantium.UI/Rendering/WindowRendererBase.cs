using System.Diagnostics;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Graphics.Effects;
using Adamantium.UI.Controls;
using Adamantium.UI.Media;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Rendering;

public abstract class WindowRendererBase : IWindowRenderer
{
    protected Viewport Viewport { get; set; }
    protected Rect2D Scissor { get; set; }
    protected Rect2D ClipRect { get; set; }
    protected Matrix4x4F ProjectionMatrix { get; set; }
    protected GraphicsDevice GraphicsDevice { get; set; }
    protected DrawingContext DrawingContext { get; set; }
    protected PresentationParameters Parameters { get; set; }
    
    protected Effect UiEffect { get; set; }
    
    protected WindowRendererBase(GraphicsDevice device)
    {
        Viewport = new Viewport();
        Scissor = new Rect2D();
        ClipRect = new Rect2D();
        ClipRect.Offset = new Offset2D();
        ClipRect.Extent = new Extent2D();
        GraphicsDevice = device;
        DrawingContext = new DrawingContext(GraphicsDevice);
    }
    
    protected IWindow Window { get; set; }

    public bool IsRendererUpToDate { get; protected set; }
    
    public void CalculateProjectionMatrix()
    {
        ProjectionMatrix = Matrix4x4F.OrthoOffCenter(
            0, 
            (float)Window.ClientWidth, 
            0, 
            (float)Window.ClientHeight,
            0.01f,
            100000f);
    }

    protected virtual void UnsubscribeFromEvents()
    {
        
    }

    protected virtual void SubscribeToEvents()
    {
        
    }
    
    public virtual void SetWindow(IWindow window)
    {
        if (window == null) return;
            
        UnsubscribeFromEvents();
        Window = window;
        FillParameters();
        SubscribeToEvents();
        InitializeWindowResources();
    }

    private void FillParameters()
    {
        Parameters = new PresentationParameters(
            PresenterType.Swapchain,
            (uint)Window.ClientWidth,
            (uint)Window.ClientHeight,
            Window.SurfaceHandle,
            Window.MSAALevel
        )
        {
            HInstanceHandle = Process.GetCurrentProcess().Handle
        };
    }

    protected virtual void InitializeWindowResources()
    {
        
    }

    public abstract void Render(AppTime appTime);
    

    public void ResizePresenter(PresentationParameters parameters)
    {
        GraphicsDevice.ResizePresenter(parameters);
        IsRendererUpToDate = true;
    }
    
    public void Dispose()
    {
        
    }
}