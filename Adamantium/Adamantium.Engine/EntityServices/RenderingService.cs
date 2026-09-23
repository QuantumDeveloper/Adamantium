using System;
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
    public override bool IsUpdateService => false;
    public override bool IsRenderingService => true;
    public override EntityServiceType ServiceType => EntityServiceType.Render;

    // Present runs on its own phase, so a skipped BeginDraw does not skip it: without this a hidden output would keep
    // handing the panel the one frame it drew before its tab went away.
    public override bool CanDisplayContent => Window.IsVisible;
    protected IContentManager Content { get; }
    public GameOutput Window { get; }

    protected GameInputManager InputManager { get; }
    protected CameraManager CameraManager { get; }
        
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
        InputManager = world.DependencyResolver.Resolve<GameInputManager>();
        CameraManager = EntityWorld.DependencyResolver.Resolve<CameraManager>();
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
        // Nothing to draw into while the output is off screen, and the whole sequence (Draw/EndDraw/Submit) is skipped
        // by returning false here - which is also what keeps a hidden output from presenting a frame nobody asked for.
        if (!Window.IsVisible || !Window.IsUpToDate())
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
        GraphicsDevice.ColorComponentFlags = Adamantium.Vulkan.Core.ColorComponentFlagBits.RBit |
                                             Adamantium.Vulkan.Core.ColorComponentFlagBits.GBit |
                                             Adamantium.Vulkan.Core.ColorComponentFlagBits.BBit;
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
        AppTime = gameTime;

        if (InputManager.IsKeyPressed(Keys.P))
        {
            ShowDebugOutput = !ShowDebugOutput;
        }

        ActiveCamera = CameraManager.GetActive(Window);

        DrawProcessors(gameTime);
    }

    protected virtual void Debug() { }

    public override void EndDraw()
    {
        GraphicsDevice.EndDraw();
        EndDrawProcessors();
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