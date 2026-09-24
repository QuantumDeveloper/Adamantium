using Adamantium.Core;
using Adamantium.Core.Collections;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.Game.Core.Events;
using Adamantium.Game.Core.Input;
using Adamantium.Game.Core.Payloads;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Serilog;

namespace Adamantium.Game.Core
{
    /// <summary>
    /// Abstract class for different game platforms
    /// </summary>
    public abstract class UniversePlatform : IUniversePlatform, IDisposable
    {
        private List<UniverseOutput> windowsToAdd;
        private List<UniverseOutput> windowsToRemove;
        private readonly IEventAggregator _eventAggregator;
        private Dictionary<OutputContext, UniverseOutput> contextToWindow;

        private bool graphicsDeviceChanged;
        
        internal static int WindowId = 1;

        private AdamantiumCollection<UniverseOutput> outputs;

        private Dictionary<UniverseOutput, UniverseOutputParametersPayload> _changedOutputs;

        private readonly GamepadHub gamepads = new GamepadHub();

        private object syncObject = new object();

        /// <summary>
        /// 
        /// </summary>
        public abstract string DefaultAppDirectory { get; }

        /// <summary>
        /// Main <see cref="UniverseOutput"/>
        /// </summary>
        public UniverseOutput MainWindow { get; protected set; }

        /// <summary>
        /// Read only collection of <see cref="UniverseOutput"/>s
        /// </summary>
        public IReadOnlyList<UniverseOutput> Outputs => outputs;

        public bool HasOutputs => outputs.Count > 0;

        protected IUniverse Universe { get; }
        
        internal IGraphicsDeviceService GraphicsDeviceService { get; private set; }
        
        /// <summary>
        /// Constructs <see cref="UniversePlatform"/> from <see cref="IUniverse"/> instance
        /// </summary>
        /// <param name="universe"></param>
        protected UniversePlatform(IUniverse universe)
        {
            Universe = universe;
            universe.Initialized += Initialized;

            _eventAggregator = universe.Container.Resolve<IEventAggregator>();
            _eventAggregator.GetEvent<UniverseOutputChangesRequestedEvent>()
                .Subscribe(OnUniverseOutputChangesRequested);

            _changedOutputs = new Dictionary<UniverseOutput, UniverseOutputParametersPayload>();
            outputs = new AdamantiumCollection<UniverseOutput>();
            windowsToAdd = new List<UniverseOutput>();
            windowsToRemove = new List<UniverseOutput>();
            contextToWindow = new Dictionary<OutputContext, UniverseOutput>();
        }

        private void OnUniverseOutputChangesRequested(UniverseOutputParametersPayload obj)
        {
            lock (syncObject)
            {
                _changedOutputs[obj.Output] = obj;
            }
        }

        private void Initialized(object sender, EventArgs e)
        {
            GraphicsDeviceService = Universe.Container.Resolve<IGraphicsDeviceService>();
            GraphicsDeviceService.DeviceChangeEnd += DeviceChangeEnd;
        }

        private void DeviceChangeEnd(object sender, EventArgs e)
        {
            graphicsDeviceChanged = true;
        }

        /// <summary>
        /// Creates <see cref="UniversePlatform"/> from <see cref="IUniverse"/>
        /// </summary>
        /// <param name="universe">instance of <see cref="IUniverse"/></param>
        /// <param name="resolver">instance of <see cref="IDependencyResolver"/></param>
        /// <returns>new <see cref="UniversePlatform"/> instance</returns>
        public static UniversePlatform Create(IUniverse universe, IDependencyResolver resolver)
        {
            switch (Configuration.Platform)
            {
                case Platform.Windows:
                    return new UniversePlatformWindows(universe, resolver);
                case Platform.OSX:
                    default:
                    throw new NotImplementedException("Current UniversePlatform is not implemented yet");
            }
        }

        /// <summary>
        /// Switches drawing context from old control to new control. After this old control could be safely removed
        /// </summary>
        /// <param name="oldContext">Old control for drawing</param>
        /// <param name="newContext">New control for drawing</param>
        public void SwitchContext(OutputContext oldContext, OutputContext newContext)
        {
            if (contextToWindow.Remove(oldContext, out var wnd))
            {
                wnd.SwitchContext(newContext);
                contextToWindow[newContext] = wnd;
            }
        }

        internal static OutputContextType GetContextType(object context)
        {
            if (context is IWindow)
            {
                return OutputContextType.Window;
            }
            if (context is RenderTargetPanel)
            {
                return OutputContextType.RenderTargetPanel;
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
                    
                    var device = GraphicsDeviceService.CreateRenderDevice();
                    wnd.SetGraphicsDevice(device);
                    wnd.Input = new InputWormhole(wnd, gamepads);
                    SubscribeToEvents(wnd);
                    
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
            windowsToRemove.Add((UniverseOutput)sender);
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

        private void SubscribeToEvents(UniverseOutput wnd)
        {
            wnd.Closed += Wnd_Closed;
        }

        private void UnsubscribeFromEvents(UniverseOutput wnd)
        {
            wnd.Closed -= Wnd_Closed;
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
                foreach (var wndObj in _changedOutputs)
                {
                    var reason = wndObj.Value.Reason;
                    var wnd = wndObj.Key;
                    if ((graphicsDeviceChanged || reason == ChangeReason.FullUpdate) && wndObj.Key.Type != GameWindowType.RenderTarget)
                    {
                        wnd.OnWindowParametersChanging(ChangeReason.FullUpdate);
                        var device = GraphicsDeviceService.MainGraphicsDevice.UpdateDevice(wnd.GraphicsDevice.DeviceId);
                        wnd.SetGraphicsDevice(device);
                        wnd.OnWindowParametersChanged(ChangeReason.FullUpdate);
                    }
                    else if (reason == ChangeReason.Resize)
                    {
                        wnd.OnWindowParametersChanging(ChangeReason.Resize);
                        Log.Logger.Debug("Update game output presenter");
                        wnd.UpdatePresenter();
                        wnd.OnWindowParametersChanged(ChangeReason.Resize);
                        OnWindowSizeChanged(wnd);
                    }
                    else
                    {
                        wnd.SetPresentOptions();
                    }
                }
                
                _changedOutputs.Clear();
                graphicsDeviceChanged = false;
            }
        }

        public void RemoveOutput(OutputContext context)
        {
            if (contextToWindow.Remove(context, out var window))
            {
                windowsToRemove.Add(window);
            }
        }

        private void OnWindowSizeChanged(UniverseOutput wnd)
        {
            wnd?.OnWindowSizeChanged();
        }

        /// <summary>
        /// Polls the gamepads once, then brings every output's input up to date for this frame.
        /// </summary>
        public void UpdateInput(AppTime gameTime)
        {
            gamepads.Update();
            for (int i = 0; i < outputs.Count; i++)
            {
                outputs[i].Input.Update(gameTime);
            }
        }

        private void OnWindowCreated(UniverseOutput output)
        {
            _eventAggregator.GetEvent<UniverseOutputCreatedEvent>().Publish(output);
        }

        private void OnOutputRemoved(UniverseOutput output)
        {
            _eventAggregator.GetEvent<UniverseOutputRemovedEvent>().Publish(output);
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
                contextToWindow.Clear();
            }
        }

        public abstract void Run(CancellationToken token);
        public UniverseOutput CreateOutput(uint width = 1280, uint height = 720)
        {
            var wnd = UniverseOutput.NewWindow(_eventAggregator, width, height);
            windowsToAdd.Add(wnd);
            return wnd;
        }

        /// <summary>
        /// Creates <see cref="UniverseOutput"/> from <see cref="OutputContext"/>
        /// </summary>
        /// <param name="context">Context (Control) from which <see cref="UniverseOutput"/> will be created</param>
        /// <returns>new <see cref="UniverseOutput"/></returns>
        public virtual UniverseOutput CreateOutput(OutputContext context)
        {
            if (context == null)
            {
                return null;
            }

            var wnd = UniverseOutput.New(_eventAggregator, context);
            contextToWindow.Add(context, wnd);
            windowsToAdd.Add(wnd);
            return wnd;
        }

        /// <summary>
        /// Creates <see cref="UniverseOutput"/> from <see cref="object"/>
        /// </summary>
        /// <param name="context">Context (Control) from which <see cref="UniverseOutput"/> will be created</param>
        /// <returns>new <see cref="UniverseOutput"/></returns>
        public UniverseOutput CreateOutput(object context)
        {
            var gameContext = new OutputContext(context);
            return CreateOutput(gameContext);
        }

        /// <summary>
        /// Create new game window from context (if no windows has been created already using this context) and add it to the list of game windows
        /// </summary>
        /// <param name="context">Window, in which Vulkan content will be rendered</param>
        /// <param name="surfaceFormat">Surface format</param>
        /// <param name="depthFormat">Depth buffer format</param>
        /// <param name="msaaLevel">MSAA level</param>
        public UniverseOutput CreateOutput( 
            object context,
            SurfaceFormat surfaceFormat, 
            DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, 
            MSAALevel msaaLevel = MSAALevel.None)
        {
            var gameContext = new OutputContext(context);
            if (!contextToWindow.ContainsKey(gameContext))
            {
                var wnd = UniverseOutput.New(_eventAggregator, gameContext, surfaceFormat, depthFormat, msaaLevel);
                contextToWindow.Add(gameContext, wnd);
                windowsToAdd.Add(wnd);
                return wnd;
            }
            return contextToWindow[gameContext];
        }

        /// <summary>
        /// Adds <see cref="UniverseOutput"/> to the windows collection
        /// </summary>
        /// <param name="window">window to add to the windows collection</param>
        public void AddOutput(UniverseOutput window)
        {
            if (outputs.Contains(window)) return;

            if (!contextToWindow.ContainsKey(window.OutputContext) && !windowsToAdd.Contains(window))
            {
                contextToWindow.Add(window.OutputContext, window);
                windowsToAdd.Add(window);
            }
        }

        /// <summary>
        /// Remove <see cref="UniverseOutput"/>
        /// </summary>
        /// <param name="context">UI Control for which <see cref="UniverseOutput"/> will be removed</param>
        public void RemoveOutput(object context)
        {
            var gameContext = new OutputContext(context);
            RemoveOutput(gameContext);
        }
    }
}
