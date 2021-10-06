using System;
using System.Collections.Generic;
using System.Diagnostics;
using Adamantium.Engine.Graphics;
using Adamantium.Game.GameInput;
using Adamantium.Imaging;
using Adamantium.UI;
using Adamantium.UI.Controls;
using Adamantium.UI.Input;
using Rectangle = Adamantium.Mathematics.Rectangle;

namespace Adamantium.Game
{
    /// <summary>
    /// Represents <see cref="GameOutput"/> based on <see cref="WindowBase"/> 
    /// </summary>
    public class RenderTargetGameOutput : AdamantiumGameOutputBase
    {
        private RenderTargetPanel nativeWindow;

        internal RenderTargetGameOutput(GameContext context)
        {
            Initialize(context);
        }

        internal RenderTargetGameOutput(
            GameContext context, 
            SurfaceFormat pixelFormat, 
            DepthFormat depthFormat, 
            MSAALevel msaaLevel)
        {
            Initialize(context, pixelFormat, depthFormat, msaaLevel);
        }

        protected override void InitializeInternal(GameContext context)
        {
            if (GameContext.Context == null || !(GameContext.Context is RenderTargetPanel))
            {
                throw new ArgumentException($"{nameof(context.Context)} should be of type RenderTargetPanel");
            }
            
            GameContext = context;
            nativeWindow = (RenderTargetPanel)GameContext.Context;
            UIComponent = nativeWindow;
            nativeWindow.RenderTargetChanged += NativeWindow_RenderTargetChanged;
            nativeWindow.GotFocus += NativeWindow_GotFocus;
            nativeWindow.LostFocus += NativeWindow_LostFocus;
            Description = new GameWindowDescription(PresenterType.RenderTarget);
            Width = (uint)nativeWindow.ActualWidth;
            Height = (uint)nativeWindow.ActualHeight;
            Handle = nativeWindow.Handle;
            ClientBounds = new Rectangle(0, 0, (int)Description.Width, (int)Description.Height);
            UpdateViewportAndScissor((uint)ClientBounds.Width, (uint)ClientBounds.Height);
        }

        private void NativeWindow_RenderTargetChanged(object sender, RenderTargetEventArgs e)
        {
            Handle = e.Handle;
            ClientBounds = new Rectangle(0, 0, e.Width, e.Height);
            Width = (uint)nativeWindow.ActualWidth;
            Height = (uint)nativeWindow.ActualHeight;
            Debug.WriteLine("RenderTarget window size = " + Description.Width + " " + Description.Height);
        }

        private void NativeWindow_LostFocus(object sender, RoutedEventArgs e)
        {
            OnDeactivated();
        }

        private void NativeWindow_GotFocus(object sender, RoutedEventArgs e)
        {
            OnActivated();
        }

        public override GameWindowDescription Description { get; protected set; }

        /// <summary>
        /// Underlying control for rendering
        /// </summary>
        public override object NativeWindow => nativeWindow;

        public override bool IsActive => UIComponent.IsFocused;

        internal override bool CanHandle(GameContext gameContext)
        {
            return gameContext.ContextType == GameContextType.RenderTargetPanel && nativeWindow != null;
        }

        internal override void SwitchContext(GameContext context)
        {
            if (!CanHandle(context)) return;
            
            nativeWindow.RenderTargetChanged -= NativeWindow_RenderTargetChanged;
            nativeWindow.GotFocus -= NativeWindow_GotFocus;
            nativeWindow.LostFocus -= NativeWindow_LostFocus;
            Initialize(context);
        }
    }
}
