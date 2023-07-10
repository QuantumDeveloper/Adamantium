using System;
using Adamantium.Core.Events;
using Adamantium.Engine.Graphics;
using Adamantium.Game.Core.Events;
using Adamantium.Game.Core.Payloads;
using Adamantium.UI;
using Adamantium.UI.Controls;
using Adamantium.UI.Input;
using Adamantium.UI.RoutedEvents;
using Rectangle = Adamantium.Mathematics.Rectangle;

namespace Adamantium.Game.Core
{
    public class AdamantiumGameOutput : AdamantiumGameOutputBase
    {
        private IWindow window;

        public AdamantiumGameOutput(IEventAggregator eventAggregator, IWindow window) : base(eventAggregator)
        {
            Initialize(new GameContext(window));
        }

        public AdamantiumGameOutput(IEventAggregator eventAggregator, GameContext gameContext) : base(eventAggregator)
        {
            Initialize(gameContext);
        }

        public override bool IsActive => window.IsActive;

        public override WindowState State
        {
            get => window.State;
            set
            {
                window.State = value;
            }
        }

        internal override bool CanHandle(GameContext gameContext)
        {
            return gameContext.ContextType == GameContextType.Window && window != null;
        }

        protected override void InitializeInternal(GameContext context)
        {
            GameContext = context;
            window = GameContext.Context as IWindow ?? throw new ArgumentException($"{nameof(context.Context)} should be of type {nameof(IWindow)}");
            InputComponent = window as IInputComponent;
            window.ClientSizeChanged += WindowOnClientSizeChanged;
            window.StateChanged += WindowOnStateChanged;

            Description = new GameWindowDescription(PresenterType.Swapchain);
            Width = (uint)window.ClientWidth;
            Height = (uint)window.ClientHeight;
            Handle = window.Handle;
            ClientBounds = new Rectangle(0, 0, (int)Description.Width, (int)Description.Height);
            UpdateViewportAndScissor((uint)ClientBounds.Width, (uint)ClientBounds.Height);
            
            base.InitializeInternal(context);
        }

        private void WindowOnStateChanged(object sender, StateChangedEventArgs e)
        {
            State = window.State;
            StateChanged?.Invoke(new WindowStatePayload(State));
        }

        private void WindowOnClientSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Width = (uint)window.ClientWidth;
            Height = (uint)window.ClientHeight;
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