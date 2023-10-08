using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Rendering;

internal class ForwardWindowRenderer : WindowRendererBase
{
    public ForwardWindowRenderer(GraphicsDevice device) : base(device)
    {
        
    }
        
    private void OnClientSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWindowResources();
    }

    private void UpdateWindowResources()
    {
        IsRendererUpToDate = false;
        InitializeWindowResources();
    }

    protected override void InitializeWindowResources()
    {
        var width = (uint)Window.ClientWidth;
        var height = (uint)Window.ClientHeight;
            
        Viewport.Width = width;
        Viewport.Height = height;

        Scissor.Extent = new Extent2D();
        Scissor.Extent.Width = width;
        Scissor.Extent.Height = height;
        Scissor.Offset = new Offset2D();
        
        Parameters.Width = width;
        Parameters.Height = height;
        CalculateProjectionMatrix();
    }

    protected override void UnsubscribeFromEvents()
    {
        if (Window != null)
        {
            Window.ClientSizeChanged -= OnClientSizeChanged;
            Window.MSAALevelChanged -= OnMSAALevelChanged;
        }
    }

    protected override void SubscribeToEvents()
    {
        Window.ClientSizeChanged += OnClientSizeChanged;
        Window.MSAALevelChanged += OnMSAALevelChanged;
    }

    private void OnMSAALevelChanged(object sender, MSAALevelChangedEventArgs e)
    {
        UpdateWindowResources();
    }

    public override void Render(AppTime appTime)
    {
        if (Window == null) return;
        
        GraphicsDevice.SetViewports(Viewport);
        ProcessVisualTree();
    }

    private void ProcessVisualTree()
    {
        var queue = new Queue<IUIComponent>();
        queue.Enqueue(Window);
        while (queue.Count > 0)
        {
            var component = queue.Dequeue();
                
            RenderComponent(component);

            foreach (var visual in component.VisualChildren)
            {
                queue.Enqueue(visual);
            }
        }
    }

    private void RenderComponent(IUIComponent component)
    {
        if (component.Visibility != Visibility.Visible) return;

        component.Render(DrawingContext);

        if (!DrawingContext.GetContainerForComponent(component, out var renderContainer)) return;

        // if (component.ClipToBounds)
        // {
        //     var clipRect = new Rect2D();
        //     clipRect.Offset = new Offset2D();
        //     clipRect.Offset.X = (int)component.ClipRectangle.X;
        //     clipRect.Offset.Y = (int)component.ClipRectangle.Y;
        //     clipRect.Extent = new Extent2D();
        //     clipRect.Extent.Width = (uint)component.ClipRectangle.Width;
        //     clipRect.Extent.Height = (uint)component.ClipRectangle.Height;
        //
        //     GraphicsDevice.SetScissors(clipRect);
        // }
        // else
        {
            GraphicsDevice.SetScissors(Scissor);
        }
        
        renderContainer.Draw(GraphicsDevice, component, ProjectionMatrix);
    }
}