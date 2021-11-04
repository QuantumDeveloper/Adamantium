using System;
using System.Collections.Generic;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Graphics.Effects;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Media;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Rendering
{
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
        
        public WindowRenderer(GraphicsDevice device)
        {
            viewport = new Viewport();
            scissor = new Rect2D();
            graphicsDevice = device;
            context = new DrawingContext(graphicsDevice);
        }
        
        private void Window_ClientSizeChanged(object sender, SizeChangedEventArgs e)
        {
            lock (this)
            {
                IsWindowResized = true;
            }
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
            
            CalculateProjectionMatrix();
        }
        
        public void ResizePresenter(PresentationParameters parameters)
        {
            graphicsDevice.ResizePresenter(
                (uint)window.ClientWidth, 
                (uint)window.ClientHeight, 
                parameters.BuffersCount,
                parameters.ImageFormat,
                parameters.DepthFormat);
            lock (this)
            {
                IsWindowResized = false;
            }
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
            InitializeWindowResources();
        }

        public void Render()
        {
            graphicsDevice.SetViewports(viewport);
            graphicsDevice.SetScissors(scissor);
            
            ProcessVisualTree();
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

            if (!context.GetPresentationForComponent(component, out var presentation)) return;
            
            foreach (var item in presentation.Items)
            {
                item.GeometryRenderer?.Draw(context.GraphicsDevice, component, projectionMatrix);
                item.StrokeRenderer?.Draw(context.GraphicsDevice, component, projectionMatrix);
            }
        }
    }
}