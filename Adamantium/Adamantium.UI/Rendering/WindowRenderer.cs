using System.Collections.Generic;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Media;
using AdamantiumVulkan.Core;

namespace Adamantium.UI
{
    internal class WindowRenderer : IWindowRenderer
    {
        private IWindow window;
        
        private bool isWindowResized;
        private Viewport viewport;
        private Rect2D scissor;
        private Matrix4x4F projectionMatrix;
        private GraphicsDevice graphicsDevice;
        private DrawingContext context;
        
        public WindowRenderer(GraphicsDevice device)
        {
            graphicsDevice = device;
            context = new DrawingContext(graphicsDevice);
        }
        
        private void Window_ClientSizeChanged(object sender, SizeChangedEventArgs e)
        {
            isWindowResized = true;
            InitializeWindowResources((uint)e.NewSize.Width, (uint)e.NewSize.Height);
        }

        private void InitializeWindowResources(uint width, uint height)
        {
            viewport.Width = width;
            viewport.Width = height;
            
            scissor.Extent = new Extent2D();
            scissor.Extent.Width = width;
            scissor.Extent.Height = height;
            scissor.Offset = new Offset2D();
            
            CalculateProjectionMatrix();
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
                window.ClientSizeChanged -= Window_ClientSizeChanged;
            }
        }

        private void SubscribeToEvents()
        {
            window.ClientSizeChanged += Window_ClientSizeChanged;
        }

        public void SetWindow(IWindow wnd)
        {
            if (wnd == null) return;
            
            UnsubscribeFromEvents();

            window = wnd;
            
            SubscribeToEvents();
            InitializeWindowResources((uint)wnd.ClientWidth, (uint)wnd.ClientHeight);
        }

        public void Render()
        {
            graphicsDevice.SetViewports(viewport);
            graphicsDevice.SetScissors(scissor);
        }

        private void ProcessVisualTree()
        {
            var queue = new Queue<IUIComponent>();
            queue.Enqueue(window);
            while (queue.Count > 0)
            {
                var component = queue.Dequeue();
                
                RenderControl(component);

                foreach (var visual in component.VisualChildren)
                {
                    queue.Enqueue(visual);
                }
            }
        }

        private void RenderControl(IUIComponent component)
        {
            if (component.Visibility != Visibility.Visible) return;

            if (!component.IsGeometryValid)
            {
                component.Render(context);
            }

            if (!context.VisualPresentations.TryGetValue(component, out var presentation)) return;
            
            
        }
    }
}