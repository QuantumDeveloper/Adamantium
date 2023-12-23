using System.Diagnostics;
using Adamantium.Core.Events;
using Adamantium.Engine.Graphics;
using Adamantium.Game.Core.Events;
using Adamantium.Game.Core.Payloads;
using Adamantium.Imaging;
using Adamantium.UI;
using Adamantium.UI.Controls;
using Adamantium.UI.Media.Imaging;
using Adamantium.UI.RoutedEvents;
using Serilog;
using Buffer = Adamantium.Engine.Graphics.Buffer;
using Rectangle = Adamantium.Mathematics.Rectangle;

namespace Adamantium.Game.Core
{
    /// <summary>
    /// Represents <see cref="GameOutput"/> based on <see cref="WindowBase"/> 
    /// </summary>
    public class RenderTargetGameOutput : AdamantiumGameOutputBase
    {
        private RenderTargetPanel nativeWindow;
        private Buffer _dstBuffer;
        private bool isExecuted = true;
        private bool _invalidateRender;
        private RenderTarget _destinationTexture;

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
            nativeWindow.RenderTargetCreatedOrUpdated += NativeWindowOnRenderTargetCreatedOrUpdated;
            nativeWindow.SizeChanged += NativeWindowOnSizeChanged;
            nativeWindow.GotFocus += NativeWindow_GotFocus;
            nativeWindow.LostFocus += NativeWindow_LostFocus;
            Description = new GameWindowDescription(PresenterType.RenderTarget);
            _destinationTexture = nativeWindow.RenderTarget?.Texture as RenderTarget;

            Width = (uint)nativeWindow.ActualWidth;
            Height = (uint)nativeWindow.ActualHeight;
            Handle = nativeWindow.Handle;
            ClientBounds = new Rectangle(0, 0, (int)Description.Width, (int)Description.Height);
            UpdateViewportAndScissor((uint)ClientBounds.Width, (uint)ClientBounds.Height);
            base.InitializeInternal(context);
        }

        public override void CopyOutput(GraphicsDevice mainDevice)
        {
            var rt = GraphicsDevice.Presenter as RenderTargetGraphicsPresenter;
            mainDevice.CopyImageFromPresenter(rt?.ResolveTexture, _destinationTexture);
            nativeWindow.CanPresent = true;
        }

        private void NativeWindowOnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RaiseSizeChangedEvent(new GameOutputSizeChangedPayload(this, e.NewSize));
        }

        private unsafe void NativeWindowOnRenderTargetCreatedOrUpdated(object sender, RenderTargetEventArgs e)
        {
            Handle = new IntPtr(e.RenderTarget.NativePointer);
            _destinationTexture = e.RenderTarget.Texture as RenderTarget;
            Log.Logger.Debug($"Updated render target with pointer: {new IntPtr(e.RenderTarget.NativePointer)}");
            ClientBounds = new Rectangle(0, 0, (int)e.Width, (int)e.Height);
            _destinationTexture = e.RenderTarget.Texture as RenderTarget;
            UpdateViewportAndScissor((uint)ClientBounds.Width, (uint)ClientBounds.Height);
            Width = (uint)nativeWindow.ActualWidth;
            Height = (uint)nativeWindow.ActualHeight;
            Debug.WriteLine("RenderTarget window size = " + Description.Width + " " + Description.Height);
            ResizeRequested = true;
            EventAggregator.GetEvent<GameOutputChangesRequestedEvent>().Publish(new GameOutputParametersPayload(this, Description, ChangeReason.Resize));
            _invalidateRender = true;
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
            
            nativeWindow.RenderTargetCreatedOrUpdated -= NativeWindowOnRenderTargetCreatedOrUpdated;
            nativeWindow.GotFocus -= NativeWindow_GotFocus;
            nativeWindow.LostFocus -= NativeWindow_LostFocus;
            Initialize(context);
        }
    }
}
