using System;
using Adamantium.Core.Events;
using Adamantium.Multiverse;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Imaging;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core.RoutedEvents;
using Rectangle = Adamantium.Mathematics.Rectangle;

namespace Adamantium.UI.Universes;

/// <summary>
/// An output that presents into a <see cref="RenderTargetPanel"/>: the frame is copied into an exportable
/// <see cref="SharedSurface"/>, which the panel imports zero-copy. Re-created when the panel resizes.
/// </summary>
public class RenderTargetUniverseOutput : UIUniverseOutput
{
    private RenderTargetPanel nativeWindow;
    private SharedSurface _sharedSurface;
    // Replaced by the last resize, kept one extra frame.
    private SharedSurface _retiredSurface;
    private SurfaceFormat _format = SurfaceFormat.B8G8R8A8.UNorm;
    private uint _surfaceWidth;
    private uint _surfaceHeight;
    private ulong _lastProduced;

    internal RenderTargetUniverseOutput(IEventAggregator eventAggregator, OutputContext context) : base(eventAggregator)
    {
        Initialize(context);
    }

    internal RenderTargetUniverseOutput(
        IEventAggregator eventAggregator,
        OutputContext context,
        SurfaceFormat pixelFormat,
        DepthFormat depthFormat,
        MSAALevel msaaLevel) : base(eventAggregator)
    {
        _format = pixelFormat;
        Initialize(context, pixelFormat, depthFormat, msaaLevel);
    }

    protected override void InitializeInternal(OutputContext context)
    {
        if (OutputContext.Context is not RenderTargetPanel)
        {
            throw new ArgumentException($"{nameof(context.Context)} should be of type RenderTargetPanel");
        }

        OutputContext = context;
        nativeWindow = (RenderTargetPanel)OutputContext.Context;
        InputComponent = nativeWindow;
        nativeWindow.SizeChanged += NativeWindowOnSizeChanged;
        Description = new UniverseOutputDescription(PresenterType.RenderTarget);

        PixelsPerPoint = HostScale;
        Width = InPixels(nativeWindow.ActualWidth);
        Height = InPixels(nativeWindow.ActualHeight);
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

        // Not over a frame the consumer has not read yet; a CPU check, so a paused consumer cannot stall the producer.
        if (_sharedSurface.ConsumeValue < _lastProduced) return;

        // Same queue as the resolve, so the copy is ordered after it.
        mainDevice.RecordSharedSurfaceCopy(rt.ResolveTexture, _sharedSurface);
        _lastProduced++;
        mainDevice.AddSignalSemaphore(_sharedSurface.ProduceSemaphore, _lastProduced);
    }

    private void EnsureSharedSurface(IGraphicsDevice device)
    {
        // The presenter's size, not the panel's: until the frame applies a new size the two differ, and the copy must fit.
        var width = Width;
        var height = Height;
        if (width == 0 || height == 0) return;
        if (_sharedSurface != null && _surfaceWidth == width && _surfaceHeight == height) return;

        // Retired, not destroyed: the consumer's pending submit may already wait on its semaphores, and destroying them
        // mid-frame lost the device.
        _retiredSurface = _sharedSurface;
        _sharedSurface = SharedSurface.CreateExportable(device, width, height, _format);
        _surfaceWidth = width;
        _surfaceHeight = height;
        _lastProduced = 0;
        nativeWindow.SetSource(_sharedSurface.Descriptor);
    }

    // The wait-idle proves the submit that referenced its semaphores has run. The panel's import is its own.
    private void DrainRetiredSurface(IGraphicsDevice device)
    {
        if (_retiredSurface == null) return;
        device.DeviceWaitIdle();
        _retiredSurface.Dispose();
        _retiredSurface = null;
    }

    protected override void Dispose(bool disposeManagedResources)
    {
        // The producer's surfaces only: the panel's import is its own and outlives this output.
        nativeWindow.SizeChanged -= NativeWindowOnSizeChanged;
        _sharedSurface?.Dispose();
        _sharedSurface = null;
        _retiredSurface?.Dispose();
        _retiredSurface = null;
        base.Dispose(disposeManagedResources);
    }

    protected override void ReleaseDeviceResources()
    {
        ReleaseSurface();
        base.ReleaseDeviceResources();
    }

    private void ReleaseSurface()
    {
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
        // SizeChanged bubbles up from children, so the size is read from the panel itself, not from e.NewSize.
        var width = InPixels(nativeWindow.ActualWidth);
        var height = InPixels(nativeWindow.ActualHeight);
        if (width == Width && height == Height)
        {
            return;
        }

        RequestResize(width, height);
    }

    protected override void OnHostScaleChanged()
    {
        PixelsPerPoint = HostScale;
        RequestResize(InPixels(nativeWindow.ActualWidth), InPixels(nativeWindow.ActualHeight));
    }

    private uint InPixels(double points)
    {
        return (uint)Math.Round(points * PixelsPerPoint);
    }

    public override UniverseOutputDescription Description { get; protected set; }

    /// <summary>
    /// Underlying control for rendering
    /// </summary>
    public override object NativeWindow => nativeWindow;

    protected override bool CanHandle(OutputContext context)
    {
        return context.Context is RenderTargetPanel && nativeWindow != null;
    }

    protected override void SwitchContext(OutputContext context)
    {
        if (!CanHandle(context)) return;

        nativeWindow.SizeChanged -= NativeWindowOnSizeChanged;
        ReleaseSurface();
        Initialize(context);
    }
}
