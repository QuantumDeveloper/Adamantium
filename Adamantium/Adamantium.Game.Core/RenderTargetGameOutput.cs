using Adamantium.Core.Events;
using Adamantium.Game.Core.Events;
using Adamantium.Game.Core.Payloads;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Imaging;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;
using Rectangle = Adamantium.Mathematics.Rectangle;

namespace Adamantium.Game.Core;

/// <summary>
/// Represents a <see cref="GameOutput"/> that presents into a <see cref="RenderTargetPanel"/>. The engine renders
/// its frame, copies it into an exportable <see cref="SharedSurface"/>, and hands the panel that surface's
/// descriptor so the panel imports it zero-copy and samples it during compositing. The surface is re-created when
/// the panel resizes and handed off again.
/// </summary>
public class RenderTargetGameOutput : AdamantiumGameOutputBase
{
    private RenderTargetPanel nativeWindow;
    private SharedSurface _sharedSurface;
    // The surface replaced by the last resize, kept alive one extra frame then disposed (see DrainRetiredSurface).
    private SharedSurface _retiredSurface;
    private SurfaceFormat _format = SurfaceFormat.B8G8R8A8.UNorm;
    private uint _surfaceWidth;
    private uint _surfaceHeight;
    private ulong _lastProduced;

    internal RenderTargetGameOutput(IEventAggregator eventAggregator, GameContext context) : base(eventAggregator)
    {
        Initialize(context);
    }

    internal RenderTargetGameOutput(
        IEventAggregator eventAggregator,
        GameContext context,
        SurfaceFormat pixelFormat,
        DepthFormat depthFormat,
        MSAALevel msaaLevel) : base(eventAggregator)
    {
        _format = pixelFormat;
        Initialize(context, pixelFormat, depthFormat, msaaLevel);
    }

    protected override void InitializeInternal(GameContext context)
    {
        if (GameContext.Context is not RenderTargetPanel)
        {
            throw new ArgumentException($"{nameof(context.Context)} should be of type RenderTargetPanel");
        }

        GameContext = context;
        nativeWindow = (RenderTargetPanel)GameContext.Context;
        InputComponent = nativeWindow;
        nativeWindow.SizeChanged += NativeWindowOnSizeChanged;
        nativeWindow.GotFocus += NativeWindow_GotFocus;
        nativeWindow.LostFocus += NativeWindow_LostFocus;
        Description = new GameWindowDescription(PresenterType.RenderTarget);

        Width = (uint)nativeWindow.ActualWidth;
        Height = (uint)nativeWindow.ActualHeight;
        ClientBounds = new Rectangle(0, 0, (int)Description.Width, (int)Description.Height);
        UpdateViewportAndScissor((uint)ClientBounds.Width, (uint)ClientBounds.Height);
        base.InitializeInternal(context);
    }

    public override void CopyOutput(IGraphicsDevice mainDevice)
    {
        DrainRetiredSurface(mainDevice);
        EnsureSharedSurface(mainDevice);
        if (_sharedSurface == null) return;

        var rt = Presenter as RenderTargetGraphicsPresenter;
        if (rt?.ResolveTexture == null) return;

        // Backpressure (single shared buffer): don't overwrite a frame the consumer hasn't read yet. CPU check only
        // (never a GPU wait), so the producer queue can't stall if the consumer pauses.
        if (_sharedSurface.ConsumeValue < _lastProduced) return;

        // Record resolve->shared into the game's CURRENT command buffer (after EndRendering, before Submit) and
        // signal Produce=N on this frame's Submit. Same queue keeps the copy ordered after the resolve.
        mainDevice.RecordSharedSurfaceCopy(rt.ResolveTexture, _sharedSurface);
        _lastProduced++;
        mainDevice.AddSignalSemaphore(_sharedSurface.ProduceSemaphore, _lastProduced);
    }

    /// <summary>Creates (or re-creates on resize) the exportable surface and hands its descriptor to the panel.</summary>
    private void EnsureSharedSurface(IGraphicsDevice device)
    {
        var width = (uint)nativeWindow.ActualWidth;
        var height = (uint)nativeWindow.ActualHeight;
        if (width == 0 || height == 0) return;
        if (_sharedSurface != null && _surfaceWidth == width && _surfaceHeight == height) return;

        // Retire the current surface for one frame instead of destroying it now. The consumer samples it and has
        // likely already queued this surface's produce/consume semaphores into its pending submit (PreRender runs in
        // BeginDraw; the submit happens at the end of the draw phase). Destroying the semaphores/image mid-frame would
        // invalidate that submit (vkQueueSubmit Invalid VkSemaphore) and lose the device. The retired surface is freed
        // next frame in DrainRetiredSurface, after a wait-idle proves the submit ran. The consumer's IMPORT of it is a
        // separate object (its own VkImage/semaphores, OS-refcounted memory) owned by the render component that samples
        // it - freed independently there - so freeing this producer surface never touches the consumer's copy.
        // (DrainRetiredSurface ran at the top of this CopyOutput, so _retiredSurface is null here under normal resize.)
        _retiredSurface = _sharedSurface;
        _sharedSurface = SharedSurface.CreateExportable(device, width, height, _format);
        _surfaceWidth = width;
        _surfaceHeight = height;
        _lastProduced = 0;
        nativeWindow.SetSource(_sharedSurface.Descriptor);
    }

    /// <summary>
    /// Frees the PRODUCER surface retired by the previous resize, one frame later: the wait-idle here proves this
    /// device's submit that referenced its semaphores has completed. The consumer's import is a separate object owned
    /// by the render component that samples it (freed there, fence-gated), so it is not touched from here.
    /// </summary>
    private void DrainRetiredSurface(IGraphicsDevice device)
    {
        if (_retiredSurface == null) return;
        device.DeviceWaitIdle();
        _retiredSurface.Dispose();
        _retiredSurface = null;
    }

    private void ReleaseSurface()
    {
        // Full teardown (context switch): drop the panel's source (its import is freed by the render component when the
        // pipeline drops the panel's units). Free the producer's current AND retired surfaces too.
        nativeWindow?.ClearSource();
        _sharedSurface?.Dispose();
        _sharedSurface = null;
        _retiredSurface?.Dispose();
        _retiredSurface = null;
        _surfaceWidth = 0;
        _surfaceHeight = 0;
    }

    private void NativeWindowOnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // SizeChanged is a BUBBLING routed event, so a CHILD's resize (e.g. a Button growing inside the panel) reaches
        // this handler too. We must react ONLY to the panel's OWN size change: use the panel's actual size (NOT
        // e.NewSize, which is the originating element's), and bail when it hasn't changed. Otherwise a child resize set
        // the game's ClientBounds/viewport to the child's size and the game rendered into a child-sized region pinned to
        // the top-left - jumping there and "stretching" with the button. (The window resize triggered the panel's own
        // SizeChanged, which is why it restored the picture.)
        var width = (uint)nativeWindow.ActualWidth;
        var height = (uint)nativeWindow.ActualHeight;
        if (width == Width && height == Height) return;

        Width = width;
        Height = height;
        ClientBounds = new Rectangle(0, 0, (int)width, (int)height);
        UpdateViewportAndScissor(width, height);
        RaiseSizeChangedEvent(new GameOutputSizeChangedPayload(this, new Adamantium.Mathematics.Size(width, height)));
        ResizeRequested = true;
        EventAggregator.GetEvent<GameOutputChangesRequestedEvent>().Publish(new GameOutputParametersPayload(this, Description, ChangeReason.Resize));
        // The surface is re-created on the next CopyOutput (size mismatch) and re-handed to the control.
    }

    private void NativeWindow_GotFocus(object sender, RoutedEventArgs e)
    {
        OnActivated();
    }

    private void NativeWindow_LostFocus(object sender, RoutedEventArgs e)
    {
        OnDeactivated();
    }

    public override GameWindowDescription Description { get; protected set; }

    /// <summary>
    /// Underlying control for rendering
    /// </summary>
    public override object NativeWindow => nativeWindow;

    public override bool IsActive => InputComponent.IsFocused;

    public override WindowState State { get; set; }

    internal override bool CanHandle(GameContext gameContext)
    {
        return gameContext.ContextType == GameContextType.RenderTargetPanel && nativeWindow != null;
    }

    internal override void SwitchContext(GameContext context)
    {
        if (!CanHandle(context)) return;

        nativeWindow.SizeChanged -= NativeWindowOnSizeChanged;
        nativeWindow.GotFocus -= NativeWindow_GotFocus;
        nativeWindow.LostFocus -= NativeWindow_LostFocus;
        ReleaseSurface();
        Initialize(context);
    }
}
