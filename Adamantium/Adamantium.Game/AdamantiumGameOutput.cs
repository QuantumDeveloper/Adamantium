using System;
using Adamantium.Engine.Graphics;
using Adamantium.Game.GameInput;
using Adamantium.Imaging;
using Adamantium.UI;
using Adamantium.UI.Controls;
using Adamantium.UI.Input;
using Rectangle = Adamantium.Mathematics.Rectangle;

namespace Adamantium.Game
{
    public class AdamantiumGameOutput : AdamantiumGameOutputBase
    {
        private IWindow window;

        public AdamantiumGameOutput(
            IWindow window, 
            SurfaceFormat pixelFormat, 
            DepthFormat depthFormat, 
            MSAALevel msaaLevel)
        {
            Initialize(new GameContext(window));
        }

        public AdamantiumGameOutput(GameContext gameContext)
        {
            Initialize(gameContext);
        }

        public override bool IsActive => window.IsActive;

        internal override bool CanHandle(GameContext gameContext)
        {
            return gameContext.ContextType == GameContextType.Window && window != null;
        }

        protected override void InitializeInternal(GameContext context)
        {
            GameContext = context;
            window = GameContext.Context as IWindow ?? throw new ArgumentException($"{nameof(context.Context)} should be of type {nameof(IWindow)}");
            UiComponent = window as FrameworkComponent;
            window.ClientSizeChanged += WindowOnClientSizeChanged;
            
            Description = new GameWindowDescription(PresenterType.Swapchain);
            Width = (uint)window.Width;
            Height = (uint)window.Height;
            Handle = window.Handle;
            ClientBounds = new Rectangle(0, 0, (int)Description.Width, (int)Description.Height);
            UpdateViewportAndScissor((uint)ClientBounds.Width, (uint)ClientBounds.Height);
            
            base.InitializeInternal(context);
        }
        
        private void WindowOnClientSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Width = (uint)window.Width;
            Height = (uint)window.Height;
            Resize(Width, Height);
            ResizeRequested = true;
            window.Measure(new Size(Width, Height));
            window.Arrange(new Rect(window.DesiredSize));
        }

        internal override void SwitchContext(GameContext context)
        {
            if (!CanHandle(context)) return;
            
            Initialize(context);
        }

        public override void Show()
        {
            base.Show();
            window?.Show();
            Description.Handle = window.Handle;
        }

        public override void Close()
        {
            base.Close();
            window?.Close();
        }

        protected override void Dispose(bool disposeManagedResources)
        {
            base.Dispose(disposeManagedResources);
            Close();
        }
    }
}