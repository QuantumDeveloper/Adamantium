using System;
using System.Collections.Generic;
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
    protected UniverseOutput Window { get; set; }

    protected InputWormhole InputManager => Window.Input;

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
        Content = EntityWorld.Satellites.Get<IContentManager>();
        Window = AssociatedService.Window;
        Window.SizeChanged += WindowOnSizeChanged;
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

    private void WindowOnSizeChanged(UniverseOutputSizeChangedPayload obj)
    {
    }

    public void Dispose()
    {
    }
}