using System;
using System.Threading;
using Adamantium.Core;
using Adamantium.Engine.Managers;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.Game;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Input;
using Adamantium.Game.Core.Payloads;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Content;
using Keys = Adamantium.Game.Core.Input.Keys;

namespace Adamantium.Engine.EntityServices;

public class RenderingService : EntityService
{
    private readonly AutoResetEvent pauseEvent = new AutoResetEvent(false);
        
    public override bool IsUpdateService => false;
    public override bool IsRenderingService => true;
    public override EntityServiceType ServiceType => EntityServiceType.Render;
    protected IContentManager Content { get; }
    public GameOutput Window { get; }

    protected LightManager LightManager { get; }
    protected GameInputManager InputManager { get; }
    protected CameraManager CameraManager { get; }
    protected ToolsManager ToolsManager { get; }
        
    //protected SpriteBatch SpriteBatch;

    protected Camera ActiveCamera { get; set; }
    protected bool ShowDebugOutput { get; set; }

    public RenderingService(EntityWorld world, GameOutput window) : base(world)
    {
        GraphicsDeviceService = world.DependencyResolver.Resolve<IGraphicsDeviceService>();
        GraphicsDeviceService.DeviceChangeBegin += DeviceChangeBegin;
        GraphicsDeviceService.DeviceChangeEnd += DeviceChangeEnd;
        GraphicsDevice = window.GraphicsDevice;
        Content = world.DependencyResolver.Resolve<IContentManager>();
        Window = window;
        Window.ParametersChanging += Window_ParametersChanging;
        Window.ParametersChanged += Window_ParametersChanged;
        Window.StateChanged += StateChanged;
        Window.SizeChanged += WindowOnSizeChanged;
        LightManager = world.DependencyResolver.Resolve<LightManager>();
        InputManager = world.DependencyResolver.Resolve<GameInputManager>();
        CameraManager = EntityWorld.DependencyResolver.Resolve<CameraManager>();
        ToolsManager = EntityWorld.DependencyResolver.Resolve<ToolsManager>();
        //SpriteBatch = new SpriteBatch(GraphicsDevice, 80000);
    }

    private void WindowOnSizeChanged(GameOutputSizeChangedPayload obj)
    {
        //Window.UpdatePresenter();
    }

    private void StateChanged(WindowStatePayload obj)
    {
            
    }

    private void Window_ParametersChanged(GameOutputParametersPayload payload)
    {
        OnWindowParametersChanged(payload.Reason);
    }

    private void Window_ParametersChanging(GameOutputParametersPayload payload)
    {
        OnWindowParametersChanging(payload.Reason);
    }

    protected virtual void OnWindowParametersChanging(ChangeReason reason)
    { }

    protected virtual void OnWindowParametersChanged(ChangeReason reason)
    { }

    private void DeviceChangeBegin(object sender, EventArgs e)
    {
        OnDeviceChangeBegin();
    }

    private void DeviceChangeEnd(object sender, EventArgs e)
    {
        OnDeviceChangeEnd();
    }

    protected virtual void OnDeviceChangeBegin()
    {
        //SpriteBatch?.Dispose();
    }

    protected virtual void OnDeviceChangeEnd()
    {
        //SpriteBatch = new SpriteBatch(GraphicsDevice, 25000);
    }

    public virtual void CreateSystemResources()
    { }

    public override bool BeginDraw()
    {
        if (!Window.IsUpToDate())
        {
            return false;
        }

        // Render targets/depth are consumed by GraphicsDevice.BeginDraw() itself (it transitions them and begins
        // rendering on them), so they must be bound BEFORE it — otherwise the very first frame transitions a null
        // target (NRE). This mirrors the working UI path (WindowRenderService.BeginDraw). Viewports/scissors are
        // recorded into the command buffer, so they must stay AFTER BeginDraw() has started it.
        GraphicsDevice.SetRenderTargets(Window.Presenter.RenderTarget);
        GraphicsDevice.SetDepthBuffer(Window.Presenter.DepthBuffer);
        // Rasterization sample count must match the presenter's MSAA attachments, otherwise vkCmdSetRasterizationSamplesEXT
        // stays at 1 while the render target is multisampled -> undefined rasterization (model not drawn on NVIDIA).
        // The UI path does the same in WindowRenderService.BeginDraw.
        GraphicsDevice.MSAALevel = Window.Presenter.MSAALevel;
        // The game frame is an opaque scene presented into a UI panel that can be drawn semi-transparently. Mask
        // ALPHA writes so the cleared alpha (1.0) is preserved across all game draws -> the shared surface is fully
        // opaque -> the panel can scale it by its own Opacity (the model's own fragments would otherwise carry the
        // unsampled-texture alpha ~0 and the panel would only ever show a faint outline / can't be made translucent).
        GraphicsDevice.ColorComponentFlags = AdamantiumVulkan.Core.ColorComponentFlagBits.RBit |
                                             AdamantiumVulkan.Core.ColorComponentFlagBits.GBit |
                                             AdamantiumVulkan.Core.ColorComponentFlagBits.BBit;
        // The game scene is opaque (the model writes solid texels); render it with an opaque blend equation, not
        // whatever blend state the UI pass left set (an alpha/premultiplied blend collapsed the model to black).
        GraphicsDevice.ColorBlendEquation = Adamantium.Graphics.Core.ColorBlendEquations.Opaque;

        if (!GraphicsDevice.BeginDraw())
        {
            return false;
        }

        GraphicsDevice.SetViewports(Window.Viewport);
        GraphicsDevice.SetScissors(Window.Scissor);
        return true;
    }

    public override void Draw(AppTime gameTime)
    {
        if (!Window.IsVisible)
        {
            pauseEvent.WaitOne();
        }
            
        AppTime = gameTime;

        if (InputManager.IsKeyPressed(Keys.P))
        {
            ShowDebugOutput = !ShowDebugOutput;
        }

        ActiveCamera = CameraManager.GetActive(Window);
            
        Processor?.Draw(gameTime);
    }

    protected virtual void Debug() { }

    public override void EndDraw()
    {
        GraphicsDevice.EndDraw();
        Processor?.EndDraw();
    }

    public override void Submit()
    {
        GraphicsDevice.Submit();
    }

    public override void Present()
    {
        Window.DisplayContent();
    }

    public override void FrameEnded()
    {
        GraphicsDevice.FrameEnded();
    }
}