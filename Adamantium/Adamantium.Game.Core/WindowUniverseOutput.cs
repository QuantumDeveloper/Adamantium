using Adamantium.Core.Events;
using Adamantium.Game.Core.Payloads;
using Adamantium.Graphics;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Mathematics;
using Adamantium.UI;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;
using Rectangle = Adamantium.Mathematics.Rectangle;

namespace Adamantium.Game.Core
{
    public class WindowUniverseOutput : UIUniverseOutput
    {
        private IWindow window;

        public WindowUniverseOutput(IEventAggregator eventAggregator, IWindow window) : base(eventAggregator)
        {
            Initialize(new OutputContext(window));
        }

        public WindowUniverseOutput(IEventAggregator eventAggregator, OutputContext gameContext) : base(eventAggregator)
        {
            Initialize(gameContext);
        }

        public override bool IsKeyboardFocused => window.IsActive;

        public override bool IsPointerOver => InputComponent.IsMouseOver;

        internal override bool CanHandle(OutputContext gameContext)
        {
            return gameContext.ContextType == OutputContextType.Window && window != null;
        }

        protected override void InitializeInternal(OutputContext context)
        {
            OutputContext = context;
            window = OutputContext.Context as IWindow ?? throw new ArgumentException($"{nameof(context.Context)} should be of type {nameof(IWindow)}");
            InputComponent = window as IInputComponent;
            window.ClientSizeChanged += WindowOnClientSizeChanged;
            window.StateChanged += WindowOnStateChanged;

            Description = new UniverseOutputDescription(PresenterType.Swapchain);
            Width = (uint)window.ClientWidth;
            Height = (uint)window.ClientHeight;
            Handle = window.Handle;
            ClientBounds = new Rectangle(0, 0, (int)Description.Width, (int)Description.Height);
            UpdateViewportAndScissor((uint)ClientBounds.Width, (uint)ClientBounds.Height);
            
            base.InitializeInternal(context);
        }

        private void WindowOnStateChanged(object sender, StateChangedEventArgs e)
        {
            StateChanged?.Invoke(State);
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

        internal override void SwitchContext(OutputContext context)
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