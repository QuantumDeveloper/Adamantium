using System;
using System.Collections.Generic;
using System.Threading;
using Adamantium.Core.Collections;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.Engine.Graphics;
using Adamantium.Game.Core.Events;
using Adamantium.Game.Core.Input;
using Adamantium.Game.Core.Payloads;
using Adamantium.Imaging;
using Adamantium.UI;
using Adamantium.UI.Controls;

namespace Adamantium.Game.Core
{
    /// <summary>
    /// Abstract class for different game platforms
    /// </summary>
    public abstract class GamePlatform : IGamePlatform, IDisposable
    {
        private List<GameOutput> windowsToAdd;
        private List<GameOutput> windowsToRemove;
        private IEventAggregator eventAggregator;
        private Dictionary<GameContext, GameOutput> contextToWindow;

        private bool graphicsDeviceChanged;
        
        internal static int WindowId = 1;

        private AdamantiumCollection<GameOutput> outputs;

        private object syncObject = new object();

        /// <summary>
        /// 
        /// </summary>
        public abstract string DefaultAppDirectory { get; }

        /// <summary>
        /// Main <see cref="GameOutput"/>
        /// </summary>
        public GameOutput MainWindow { get; protected set; }

        /// <summary>
        /// Current focused <see cref="GameOutput"/>
        /// </summary>
        public GameOutput ActiveWindow { get; private set; }

        /// <summary>
        /// Read only collection of <see cref="GameOutput"/>s
        /// </summary>
        public IReadOnlyList<GameOutput> Outputs => outputs;

        public bool HasOutputs => outputs.Count > 0;

        protected IGame Game { get; }
        
        internal IGraphicsDeviceService GraphicsDeviceService { get; private set; }
        
        /// <summary>
        /// Constructs <see cref="GamePlatform"/> from <see cref="IGame"/> instance
        /// </summary>
        /// <param name="game"></param>
        protected GamePlatform(IGame game)
        {
            Game = game;
            game.Initialized += Initialized;
            eventAggregator = game.Resolver.Resolve<IEventAggregator>();

            outputs = new AdamantiumCollection<GameOutput>();
            windowsToAdd = new List<GameOutput>();
            windowsToRemove = new List<GameOutput>();
            contextToWindow = new Dictionary<GameContext, GameOutput>();
        }

        private void Initialized(object sender, EventArgs e)
        {
            GraphicsDeviceService = Game.Resolver.Resolve<IGraphicsDeviceService>();
            GraphicsDeviceService.DeviceChangeEnd += DeviceChangeEnd;
        }

        private void DeviceChangeEnd(object sender, EventArgs e)
        {
            graphicsDeviceChanged = true;
        }

        /// <summary>
        /// Creates <see cref="GamePlatform"/> from <see cref="IGame"/>
        /// </summary>
        /// <param name="game">instance of <see cref="IGame"/></param>
        /// <param name="resolver">instance of <see cref="IDependencyResolver"/></param>
        /// <returns>new <see cref="GamePlatform"/> instance</returns>
        public static GamePlatform Create(IGame game, IDependencyResolver resolver)
        {
            switch (Configuration.Platform)
            {
                case Platform.Windows:
                    return new GamePlatformWindows(game, resolver);
                case Platform.OSX:
                    default:
                    throw new NotImplementedException("Current GamePlatform is not implemented yet");
            }
        }

        /// <summary>
        /// Switches drawing context from old control to new control. After this old control could be safely removed
        /// </summary>
        /// <param name="oldContext">Old control for drawing</param>
        /// <param name="newContext">New control for drawing</param>
        public void SwitchContext(GameContext oldContext, GameContext newContext)
        {
            if (contextToWindow.TryGetValue(oldContext, out var wnd))
            {
                wnd.SwitchContext(newContext);
            }
        }

        internal static GameContextType GetContextType(object context)
        {
            if (context is IWindow)
            {
                return GameContextType.Window;
            }
            if (context is RenderTargetPanel)
            {
                return GameContextType.RenderTargetPanel;
            }
            throw new NotSupportedException("this context type currently is not supported");
        }

        private void AddWindowsInternal()
        {
            if (windowsToAdd.Count > 0)
            {
                for (int i = 0; i < windowsToAdd.Count; i++)
                {
                    var wnd = windowsToAdd[i];
                    var device = GraphicsDeviceService.CreateRenderDevice(wnd.Description);
                    wnd.SetGraphicsDevice(device);
                    SubscribeToEvents(wnd);
                    wnd.Closed += Wnd_Closed;

                    outputs.Add(wnd);

                    if (outputs.Count == 1)
                    {
                        MainWindow = wnd;
                    }
                    wnd.ClearState();
                    wnd.Show();
                    OnWindowCreated(wnd);
                }
                windowsToAdd.Clear();
            }
        }

        private void Wnd_Closed(object sender, EventArgs e)
        {
            windowsToRemove.Add((GameOutput)sender);
        }

        internal void RemoveWindowsInternal()
        {
            if (windowsToRemove.Count > 0)
            {
                for (int i = 0; i < windowsToRemove.Count; i++)
                {
                    var wnd = windowsToRemove[i];
                    outputs.Remove(wnd);
                    UnsubscribeFromEvents(wnd);

                    WindowId--;
                    OnOutputRemoved(wnd);
                }
                windowsToRemove.Clear();
            }
        }

        private void SubscribeToEvents(GameOutput wnd)
        {
            wnd.Activated += Window_Activated;
            wnd.Deactivated += Window_Deactivated;
            wnd.MouseInput += OnMouseInput;
            wnd.KeyInput += OnKeyInput;
        }

        private void UnsubscribeFromEvents(GameOutput wnd)
        {
            wnd.Activated -= Window_Activated;
            wnd.Deactivated -= Window_Deactivated;
            wnd.MouseInput += OnMouseInput;
            wnd.KeyInput += OnKeyInput;
        }

        /// <summary>
        /// Called after EndScene to update all devices and resources to avoid resizing issues and black screens
        /// </summary>
        public void MakePreparationsForNextFrame()
        {
            RemoveWindowsInternal();

            lock (syncObject)
            {
                AddWindowsInternal();
                foreach (var wnd in outputs)
                {
                    if (graphicsDeviceChanged || wnd.UpdateRequested)
                    {
                        OnWindowParametersChanging(wnd, ChangeReason.FullUpdate);
                        var device = GraphicsDeviceService.UpdateDevice(wnd.GraphicsDevice.DeviceId, wnd.Description);
                        wnd.SetGraphicsDevice(device);
                        OnWindowParametersChanged(wnd, ChangeReason.FullUpdate);
                    }
                    else if (wnd.ResizeRequested)
                    {
                        OnWindowParametersChanging(wnd, ChangeReason.Resize);
                        wnd.UpdatePresenter();
                        OnWindowParametersChanged(wnd, ChangeReason.Resize);
                        OnWindowSizeChanged(wnd);
                    }
                    else
                    {
                        wnd.SetPresentOptions();
                    }
                }
                graphicsDeviceChanged = false;
            }
        }

        private void OnWindowParametersChanging(GameOutput window, ChangeReason reason)
        {
            window.OnWindowParametersChanging(reason);

            eventAggregator.GetEvent<GameOutputParametersChangingEvent>().Publish(new GameOutputParametersPayload(window, reason));
        }

        private void OnWindowParametersChanged(GameOutput window, ChangeReason reason)
        {
            window.OnWindowParametersChanged(reason);

            eventAggregator.GetEvent<GameOutputParametersChangedEvent>().Publish(new GameOutputParametersPayload(window, reason));
        }

        private void Window_Deactivated(GameOutput output)
        {
            OnDeactivated(output);
            ActiveWindow = null;
        }

        private void Window_Activated(GameOutput output)
        {
            ActiveWindow = output;
            OnActivated(ActiveWindow);
        }

        public void RemoveOutput(GameContext context)
        {
            if (contextToWindow.ContainsKey(context))
            {
                var window = contextToWindow[context];
                contextToWindow.Remove(context);
                outputs.Remove(window);
            }
        }

        private void OnActivated(GameOutput output)
        {
            eventAggregator.GetEvent<GameOutputActivatedEvent>().Publish(output);
        }

        private void OnDeactivated(GameOutput output)
        {
            eventAggregator.GetEvent<GameOutputDeactivatedEvent>().Publish(output);
        }

        private void OnWindowSizeChanged(GameOutput wnd)
        {
            wnd?.OnWindowSizeChanged();
            
            // eventAggregator.GetEvent<GameOutputSizeChanged>()
            //     .Publish(new GameOutputSizeChangedPayload(wnd, new Size(wnd.Width, wnd.Height)));
        }

        private void OnKeyInput(KeyboardInput input)
        {
            eventAggregator.GetEvent<KeyboardInputEvent>().Publish(input);
        }

        private void OnMouseInput(MouseInput input)
        {
            eventAggregator.GetEvent<MouseInputEvent>().Publish(input);
        }

        private void OnWindowCreated(GameOutput output)
        {
            eventAggregator.GetEvent<GameOutputCreatedEvent>().Publish(output);
        }

        private void OnOutputRemoved(GameOutput output)
        {
            eventAggregator.GetEvent<GameOutputRemovedEvent>().Publish(output);
        }

        public void Dispose()
        {
            lock (this)
            {
                for (int i = 0; i < Outputs.Count; i++)
                {
                    Outputs[i].Dispose();
                }
                outputs.Clear();
                MainWindow = null;
                ActiveWindow = null;
                contextToWindow.Clear();
            }
        }

        public abstract void Run(CancellationToken token);
        public GameOutput CreateOutput(uint width = 1280, uint height = 720)
        {
            var wnd = GameOutput.NewWindow(width, height);
            windowsToAdd.Add(wnd);
            return wnd;
        }

        /// <summary>
        /// Creates <see cref="GameOutput"/> from <see cref="GameContext"/>
        /// </summary>
        /// <param name="context">Context (Control) from which <see cref="GameOutput"/> will be created</param>
        /// <returns>new <see cref="GameOutput"/></returns>
        public virtual GameOutput CreateOutput(GameContext context)
        {
            if (context == null)
            {
                return null;
            }

            var wnd = GameOutput.New(context);
            windowsToAdd.Add(wnd);
            return wnd;
        }

        /// <summary>
        /// Creates <see cref="GameOutput"/> from <see cref="object"/>
        /// </summary>
        /// <param name="context">Context (Control) from which <see cref="GameOutput"/> will be created</param>
        /// <returns>new <see cref="GameOutput"/></returns>
        public GameOutput CreateOutput(object context)
        {
            var gameContext = new GameContext(context);
            return CreateOutput(gameContext);
        }

        /// <summary>
        /// Create new game window from context (if no windows has been created already using this context) and add it to the list of game windows
        /// </summary>
        /// <param name="context">Window, in which Vulkan content will be rendered</param>
        /// <param name="surfaceFormat">Surface format</param>
        /// <param name="depthFormat">Depth buffer format</param>
        /// <param name="msaaLevel">MSAA level</param>
        public GameOutput CreateOutput(object context, SurfaceFormat surfaceFormat, DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, MSAALevel msaaLevel = MSAALevel.None)
        {
            var gameContext = new GameContext(context);
            if (!contextToWindow.ContainsKey(gameContext))
            {
                var wnd = GameOutput.New(gameContext, surfaceFormat, depthFormat, msaaLevel);
                contextToWindow.Add(gameContext, wnd);
                windowsToAdd.Add(wnd);
                return wnd;
            }
            return contextToWindow[gameContext];
        }

        /// <summary>
        /// Adds <see cref="GameOutput"/> to the windows collection
        /// </summary>
        /// <param name="window">window to add to the windows collection</param>
        public void AddOutput(GameOutput window)
        {
            if (outputs.Contains(window)) return;

            if (!contextToWindow.ContainsKey(window.GameContext) && !windowsToAdd.Contains(window))
            {
                contextToWindow.Add(window.GameContext, window);
                windowsToAdd.Add(window);
            }
        }

        /// <summary>
        /// Remove <see cref="GameOutput"/>
        /// </summary>
        /// <param name="context">UI Control for which <see cref="GameOutput"/> will be removed</param>
        public void RemoveOutput(object context)
        {
            var gameContext = new GameContext(context);
            RemoveOutput(gameContext);
        }
    }
}
