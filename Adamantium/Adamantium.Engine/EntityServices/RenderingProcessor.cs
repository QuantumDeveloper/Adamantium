using System;
using System.Collections.Generic;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Managers;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Input;
using Adamantium.Game.Core.Payloads;

namespace Adamantium.Engine.EntityServices;

public class RenderingProcessor : EntityProcessor<RenderingService>, IDisposable
{
    protected IGraphicsDeviceService GraphicsDeviceService;
    protected GraphicsDevice GraphicsDevice { get; set; }
    protected EntityWorld EntityWorld { get; set; }

    protected IContentManager Content { get; set; }
    protected GameOutput Window { get; set; }

    protected LightManager LightManager { get; set; }
    protected GameInputManager InputManager { get; set; }
    protected CameraManager CameraManager { get; set; }
    protected ToolsManager ToolsManager { get; set; }

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
        LightManager = EntityWorld.DependencyResolver.Resolve<LightManager>();
        InputManager = EntityWorld.DependencyResolver.Resolve<GameInputManager>();
        CameraManager = EntityWorld.DependencyResolver.Resolve<CameraManager>();
        ToolsManager = EntityWorld.DependencyResolver.Resolve<ToolsManager>();
        SpriteBatch = new SpriteBatch(GraphicsDevice, 80000);
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