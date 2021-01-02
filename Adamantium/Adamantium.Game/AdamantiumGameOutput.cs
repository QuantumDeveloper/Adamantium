using System;
using Adamantium.Engine.Graphics;
using Adamantium.Imaging;
using Adamantium.UI;
using Adamantium.UI.Controls;
using Rectangle = Adamantium.Mathematics.Rectangle;

namespace Adamantium.Game
{
    public class AdamantiumGameOutput : AdamantiumGameOutputBase
    {
        protected override GameWindowDescription Description { get; set; }

        private IWindow window;

        public AdamantiumGameOutput(uint width = 1280, uint height = 720)
        {
            var wnd = Window.New();
            wnd.Width = width;
            wnd.Height = height;
            Initialize(new GameContext(wnd));
        }

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

        internal override bool CanHandle(GameContext gameContext)
        {
            return gameContext.ContextType == GameContextType.Window && window != null;
        }

        internal override void Resize(int width, int height)
        {
            window.Width = width;
            window.Height = height;
        }
        
        protected override void InitializeInternal(GameContext context)
        {
            if (GameContext.Context == null || !(GameContext.Context is IWindow))
            {
                throw new ArgumentException($"{nameof(context.Context)} should be of type RenderTargetPanel");
            }
            
            GameContext = context;
            window = (IWindow)GameContext.Context;
            UIComponent = window as FrameworkComponent;
            window.SizeChanged += WindowOnSizeChanged;
            //window.GotFocus += NativeWindow_GotFocus;
            //window.LostFocus += NativeWindow_LostFocus;
            Description = new GameWindowDescription(PresenterType.Swapchain);
            Width = (uint)window.Width;
            Height = (uint)window.Height;
            Handle = window.Handle;
            ClientBounds = new Rectangle(0, 0, (int)Description.Width, (int)Description.Height);
        }

        private void WindowOnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            
        }

        internal override void SwitchContext(GameContext context)
        {
            if (!CanHandle(context)) return;
            
            
            Initialize(context);
        }

        internal override GraphicsPresenter CreatePresenter(GraphicsDevice device)
        {
            return new SwapChainGraphicsPresenter(device, Description);
        }

        public override void Show()
        {
            base.Show();
            window.Show();
        }

        public override void Close()
        {
            base.Close();
            window.Close();
        }
    }
}