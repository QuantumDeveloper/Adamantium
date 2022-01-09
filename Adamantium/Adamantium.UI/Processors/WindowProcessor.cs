using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Rendering;
using Adamantium.UI.RoutedEvents;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Processors;

public class WindowProcessor : UIProcessor
{
    //private Mutex mutex = new Mutex(false, "adamantiumMutex");
        
    private IGameTime _gameTime;
    private IWindow window;
    private PresentationParameters parameters;
    private GraphicsDevice graphicsDevice;
    private IWindowRenderer windowRenderer;
    private AutoResetEvent _pauseEvent;
        
    public WindowProcessor(EntityWorld world, IWindow window, MainGraphicsDevice mainDevice)
        : base(world)
    {
        this.window = window;
        this.window.SizeChanged += WindowOnSizeChanged;
        this.window.StateChanged += WindowOnStateChanged;
        CreateResources(mainDevice);
        _pauseEvent = new AutoResetEvent(false);
    }

    private void WindowOnStateChanged(object sender, StateChangedEventArgs e)
    {
        if (window.State is WindowState.Maximized or WindowState.Normal)
        {
            _pauseEvent.Set();
            //_pauseEvent.Reset();
        }

    }

    private void WindowOnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // if (window.State is WindowState.Maximized or WindowState.Normal)
        // {
        //     _pauseEvent.Set();
        // }
    }

    private void CreateResources(MainGraphicsDevice mainDevice)
    {
        parameters = new PresentationParameters(
            PresenterType.Swapchain,
            (uint)window.ClientWidth,
            (uint)window.ClientHeight,
            window.SurfaceHandle,
            window.MSAALevel
        )
        {
            HInstanceHandle = Process.GetCurrentProcess().Handle
        };
            
        graphicsDevice = mainDevice.CreateRenderDevice(@parameters);
        graphicsDevice.ClearColor = Colors.White;
        graphicsDevice.AddDynamicStates(DynamicState.Viewport, DynamicState.Scissor);

        windowRenderer = new WindowRenderer(graphicsDevice);
        var renderer = (WindowRenderer)windowRenderer;
        renderer.Parameters = parameters;
        windowRenderer.SetWindow(window);
    }

    public override void UnloadContent()
    {
    }
        
    public override void Update(IGameTime gameTime)
    {
        window.Update();
    }

    public override bool BeginDraw()
    {
        return IsVisible;
    }

    public override void Draw(IGameTime gameTime)
    {
        if (window.State == WindowState.Minimized)
        {
            _pauseEvent.WaitOne();
            //_pauseEvent.WaitOne();
        }
            
        _gameTime = gameTime;
        base.Draw(gameTime);
            
        if (windowRenderer == null) return;
            
        if (graphicsDevice.BeginDraw(1, 0))
        {
            windowRenderer.Render();
        }
    }
        
    public override void EndDraw()
    {
        base.EndDraw();
        graphicsDevice.EndDraw();
        parameters.Width = (uint)window.ClientWidth;
        parameters.Height = (uint)window.ClientHeight;
            
        graphicsDevice.Present(parameters);
        if (windowRenderer.IsWindowResized)
        {
            windowRenderer.ResizePresenter(parameters);
        }
    }
}