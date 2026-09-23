using System;
using System.Collections.Generic;
using Adamantium.Engine.Managers;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Input;
using Adamantium.Game.Core.Payloads;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Content;

namespace Adamantium.Engine.EntityServices;

public class RenderingProcessor : EntityProcessor<RenderingService>, IDisposable
{
    protected IGraphicsDeviceService GraphicsDeviceService;
    protected IGraphicsDevice GraphicsDevice { get; set; }
    protected EntityWorld EntityWorld { get; set; }

    protected IContentManager Content { get; set; }
    protected GameOutput Window { get; set; }

    protected GameInputManager InputManager => Window.Input;
    protected CameraManager CameraManager { get; set; }

    protected SpriteBatch SpriteBatch { get; set; }

    protected Camera ActiveCamera { get; set; }
    protected bool ShowDebugOutput { get; set; }

    public IReadOnlyList<Entity> Entities => EntityWorld.RootEntities;

    public RenderingProcessor()
    {
    }

    protected override void OnAttached()
    {
        Initialize();
    }

    protected override void OnDetached()
    {
        Dispose();
    }

    protected void Initialize()
    {
        EntityWorld = AssociatedService.EntityWorld;
        GraphicsDeviceService = AssociatedService.GraphicsDeviceService;
        GraphicsDevice = AssociatedService.GraphicsDevice;
        GraphicsDeviceService.DeviceChangeBegin += DeviceChangeBegin;
        GraphicsDeviceService.DeviceChangeEnd += DeviceChangeEnd;
        Content = EntityWorld.DependencyResolver.Resolve<IContentManager>();
        Window = AssociatedService.Window;
        // Window.ParametersChanging += Window_ParametersChanging;
        // Window.ParametersChanged += Window_ParametersChanged;
        //Window.StateChanged += StateChanged;
        Window.SizeChanged += WindowOnSizeChanged;
        CameraManager = EntityWorld.DependencyResolver.Resolve<CameraManager>();
        //SpriteBatch = new SpriteBatch(GraphicsDevice, 80000);
        LoadContent();
    }

    protected virtual void LoadContent()
    {
        
    }

    private void DeviceChangeEnd(object sender, EventArgs e)
    {
    }

    private void DeviceChangeBegin(object sender, EventArgs e)
    {
    }

    protected virtual void OnDeviceChangeBegin()
    {
        SpriteBatch?.Dispose();
    }

    public override void EndDraw()
    {
        Window.CopyOutput(GraphicsDevice);
    }

    protected virtual void OnDeviceChangeEnd()
    {
        SpriteBatch = new SpriteBatch(GraphicsDevice, 25000);
    }

    private void WindowOnSizeChanged(GameOutputSizeChangedPayload obj)
    {
    }

    public void Dispose()
    {
    }
}