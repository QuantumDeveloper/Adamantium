using System;
using System.Collections.Generic;
using System.Diagnostics;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Graphics.Effects;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Media;
using Adamantium.UI.RoutedEvents;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Rendering;

internal class WindowRenderer : IWindowRenderer
{
    private IWindow window;

    public bool IsWindowResized { get; private set; }
    private Viewport viewport;
    private Rect2D scissor;
    private Matrix4x4F projectionMatrix;
    private GraphicsDevice graphicsDevice;
    private DrawingContext context;
    private Effect uiEffect;

    private Rect2D clipRect;

    public PresentationParameters Parameters;
        
    public WindowRenderer(GraphicsDevice device)
    {
        viewport = new Viewport();
        scissor = new Rect2D();
        clipRect = new Rect2D();
        clipRect.Offset = new Offset2D();
        clipRect.Extent = new Extent2D();
        graphicsDevice = device;
        context = new DrawingContext(graphicsDevice);
    }
        
    private void OnClientSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWindowResources();
    }

    private void UpdateWindowResources()
    {
        IsWindowResized = true;
        InitializeWindowResources();
    }

    private void InitializeWindowResources()
    {
        var width = (uint)window.ClientWidth;
        var height = (uint)window.ClientHeight;
            
        viewport.Width = width;
        viewport.Height = height;

        scissor.Extent = new Extent2D();
        scissor.Extent.Width = width;
        scissor.Extent.Height = height;
        scissor.Offset = new Offset2D();


        Parameters.Width = width;
        Parameters.Height = height;
        CalculateProjectionMatrix();
    }
        
    public void ResizePresenter(PresentationParameters parameters)
    {
        graphicsDevice.ResizePresenter(parameters);
        IsWindowResized = false;
    }

    private void CalculateProjectionMatrix()
    {
        projectionMatrix = Matrix4x4F.OrthoOffCenter(
            0, 
            (float)window.ClientWidth, 
            0, 
            (float)window.ClientHeight,
            0.01f,
            100000f);
    }

    private void UnsubscribeFromEvents()
    {
        if (window != null)
        {
            window.ClientSizeChanged -= OnClientSizeChanged;
            window.MSAALevelChanged -= OnMSAALevelChanged;
        }
    }

    private void SubscribeToEvents()
    {
        window.ClientSizeChanged += OnClientSizeChanged;
        window.MSAALevelChanged += OnMSAALevelChanged;
    }

    private void OnMSAALevelChanged(object sender, MSAALevelChangedEventArgs e)
    {
        UpdateWindowResources();
    }

    public void SetWindow(IWindow wnd)
    {
        if (wnd == null) return;
            
        UnsubscribeFromEvents();
        window = wnd;
            
        SubscribeToEvents();
        InitializeWindowResources();
    }

    public void Render()
    {
        graphicsDevice.SetViewports(viewport);
        ProcessVisualTree();
    }

    private void ProcessVisualTree()
    {
        var queue = new Queue<IUIComponent>();
        queue.Enqueue(window);
        while (queue.Count > 0)
        {
            var component = queue.Dequeue();
                
            RenderVisualTree(component);

            foreach (var visual in component.LogicalChildren)
            {
                queue.Enqueue(visual as IUIComponent);
            }
        }
    }

    private void RenderVisualTree(IUIComponent component)
    {
        var queue = new Queue<IUIComponent>();
        queue.Enqueue(component);
        while (queue.Count > 0)
        {
            var control = queue.Dequeue();
                
            RenderControl(control);

            foreach (var visual in control.VisualChildren)
            {
                queue.Enqueue(visual);
            }
        }
    }

    private void RenderControl(IUIComponent component)
    {
        if (component.Visibility != Visibility.Visible) return;

        component.Render(context);

        if (!context.GetPresentationForComponent(component, out var presentation)) return;

        // if (component.ClipToBounds)
        // {
        //     clipRect.Offset.X = (int)component.ClipRectangle.X;
        //     clipRect.Offset.Y = (int)component.ClipRectangle.Y;
        //     clipRect.Extent.Width = (uint)component.ClipRectangle.Width;
        //     clipRect.Extent.Height = (uint)component.ClipRectangle.Height;
        //
        //     graphicsDevice.SetScissors(clipRect);
        // }
        // else
        {
            graphicsDevice.SetScissors(scissor);
        }

        foreach (var item in presentation.Items)
        {
            item.GeometryRenderer?.Draw(context.GraphicsDevice, component, projectionMatrix);
            item.StrokeRenderer?.Draw(context.GraphicsDevice, component, projectionMatrix);
        }
    }
}