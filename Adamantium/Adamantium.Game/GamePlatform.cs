﻿using System;
using System.Collections.Generic;
using Adamantium.Core.Collections;
using Adamantium.Engine.Graphics;
using Adamantium.Imaging;
using Adamantium.UI;
using Adamantium.UI.Controls;
//using Control = System.Windows.Forms.Control;

namespace Adamantium.Engine
{
    /// <summary>
    /// Abstract class for different game platforms
    /// </summary>
    public abstract class GamePlatform : IGamePlatform, IDisposable
    {
        internal static int WindowId = 1;

        private AdamantiumCollection<GameWindow> windows;

        private object syncObject = new object();

        /// <summary>
        /// 
        /// </summary>
        public abstract string DefaultAppDirectory { get; }

        /// <summary>
        /// Main <see cref="GameWindow"/>
        /// </summary>
        public GameWindow MainWindow { get; protected set; }

        /// <summary>
        /// Current focued <see cref="GameWindow"/>
        /// </summary>
        public GameWindow ActiveWindow { get; private set; }

        /// <summary>
        /// Read only collection of <see cref="GameWindow"/>s
        /// </summary>
        public GameWindow[] Windows => windows.ToArray();

        protected GameBase gameBase;
        private List<GameWindow> windowsToAdd;
        private List<GameWindow> windowsToRemove;
        internal IGraphicsDeviceManager GraphicsDeviceManager { get; private set; }
        internal IGraphicsDeviceService GraphicsDeviceService { get; private set; }
        private System.Collections.Generic.Dictionary<GameContext, GameWindow> contextToWindow;

        private bool graphicsDeviceChanged;

        /// <summary>
        /// Constructs <see cref="GamePlatform"/> from <see cref="GameBase"/> instance
        /// </summary>
        /// <param name="gameBase"></param>
        protected GamePlatform(GameBase gameBase)
        {
            this.gameBase = gameBase;
            gameBase.Initialized += Initialized;

            windows = new AdamantiumCollection<GameWindow>();
            windowsToAdd = new List<GameWindow>();
            windowsToRemove = new List<GameWindow>();
            contextToWindow = new System.Collections.Generic.Dictionary<GameContext, GameWindow>();
        }

        private void Initialized(object sender, EventArgs e)
        {
            GraphicsDeviceManager = gameBase.Services.Get<IGraphicsDeviceManager>();
            GraphicsDeviceService = gameBase.Services.Get<IGraphicsDeviceService>();
            GraphicsDeviceService.DeviceChangeEnd += DeviceChangeEnd;
        }

        private void DeviceChangeEnd(object sender, EventArgs e)
        {
            graphicsDeviceChanged = true;
        }

        /// <summary>
        /// Creates <see cref="GamePlatform"/> from <see cref="GameBase"/>
        /// </summary>
        /// <param name="gameBase">instance of <see cref="GameBase"/></param>
        /// <returns>new <see cref="GamePlatform"/> instance</returns>
        public static GamePlatform Create(GameBase gameBase)
        {
            return new GamePlatformDesktop(gameBase);
        }

        /// <summary>
        /// Creates <see cref="GameWindow"/> from <see cref="GameContext"/>
        /// </summary>
        /// <param name="context">Context (Control) from which <see cref="GameWindow"/> will be created</param>
        /// <returns>new <see cref="GameWindow"/></returns>
        public virtual GameWindow CreateWindow(GameContext context)
        {
            if (context == null)
            {
                return null;
            }

            var wnd = GameWindow.New(context);
            windowsToAdd.Add(wnd);
            return wnd;
        }

        /// <summary>
        /// Switches drawing context from old control to new control. After this old control could be safely removed
        /// </summary>
        /// <param name="oldContext">Old control for drawing</param>
        /// <param name="newContext">New control for drawing</param>
        public void SwitchContext(GameContext oldContext, GameContext newContext)
        {
            GameWindow wnd = null;
            if (contextToWindow.TryGetValue(oldContext, out wnd))
            {
                wnd.SwitchContext(newContext);
            }
        }


        internal static GameContextType GetContextType(object context)
        {
            if (context is Control)
            {
                return GameContextType.WinForms;
            }
            if (context is RenderTargetPanel)
            {
                return GameContextType.RenderTargetPanel;
            }
            throw new NotSupportedException("this context type is not supported");
        }

        private void AddWindowsInternal()
        {
            if (windowsToAdd.Count > 0)
            {
                for (int i = 0; i < windowsToAdd.Count; i++)
                {
                    var wnd = windowsToAdd[i];
                    wnd.CreatePresenter(GraphicsDeviceService.GraphicsDevice);
                    wnd.SetPresentOptions();

                    SubscribeToEvents(wnd);

                    wnd.Closed += Wnd_Closed;

                    windows.Add(wnd);

                    if (windows.Count == 1)
                    {
                        MainWindow = wnd;
                    }
                    wnd.ClearState();
                    wnd.Show();
                    OnWindowCreated(new GameWindowEventArgs(wnd));
                }
                windowsToAdd.Clear();
            }
        }

        private void Wnd_Closed(object sender, EventArgs e)
        {
            windowsToRemove.Add((GameWindow)sender);
        }

        internal void RemoveWindowsInternal()
        {
            if (windowsToRemove.Count > 0)
            {
                for (int i = 0; i < windowsToRemove.Count; i++)
                {
                    var wnd = windowsToRemove[i];
                    windows.Remove(wnd);
                    UnsubscribeFromEvents(wnd);

                    WindowId--;
                    OnWindowRemoved(new GameWindowEventArgs(wnd));
                }
                windowsToRemove.Clear();
            }
        }

        private void SubscribeToEvents(GameWindow wnd)
        {
            wnd.Activated += Window_Activated;
            wnd.Deactivated += Window_Deactivated;
            wnd.BoundsChanged += Window_BoundsChanged;
            wnd.MouseDown += Wnd_MouseDown;
            wnd.MouseUp += Wnd_MouseUp;
            wnd.MouseWheel += Wnd_MouseWheel;
            wnd.MouseDelta += Wnd_MouseDelta;
            wnd.KeyDown += Wnd_KeyDown;
            wnd.KeyUp += Wnd_KeyUp;
        }

        private void UnsubscribeFromEvents(GameWindow wnd)
        {
            wnd.Activated -= Window_Activated;
            wnd.Deactivated -= Window_Deactivated;
            wnd.BoundsChanged -= Window_BoundsChanged;
            wnd.MouseDown -= Wnd_MouseDown;
            wnd.MouseUp -= Wnd_MouseUp;
            wnd.MouseWheel -= Wnd_MouseWheel;
            wnd.MouseDelta -= Wnd_MouseDelta;
            wnd.KeyDown -= Wnd_KeyDown;
            wnd.KeyUp -= Wnd_KeyUp;
        }

        /// <summary>
        /// Called after EndScene to update all devices and resources to avoid resizing issues and black screens
        /// </summary>
        internal void MakePreparationsForNextFrame()
        {
            RemoveWindowsInternal();

            GraphicsDeviceManager.PrepareForNextFrame();

            lock (syncObject)
            {
                AddWindowsInternal();
                foreach (GameWindow wnd in windows)
                {
                    if (graphicsDeviceChanged || wnd.UpdateRequested)
                    {
                        OnWindowParametersChanging(wnd, ChangeReason.FullUpdate);
                        wnd.DisposePresenter();
                        wnd.CreatePresenter(GraphicsDeviceService.GraphicsDevice);
                        wnd.ClearState();
                        wnd.SetPresentOptions();
                        OnWindowParametersChanged(wnd, ChangeReason.FullUpdate);
                    }
                    else if (wnd.ResizeRequested)
                    {
                        OnWindowParametersChanging(wnd, ChangeReason.Resize);
                        wnd.ResizePresenter();
                        wnd.ClearState();
                        wnd.SetPresentOptions();
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

        private void OnWindowParametersChanging(GameWindow window, ChangeReason reason)
        {
            window.OnWindowParametersChanging(reason);

            WindowParametersChanging?.Invoke(this, new GameWindowParametersEventArgs(window, reason));
        }

        private void OnWindowParametersChanged(GameWindow window, ChangeReason reason)
        {
            window.OnWindowParametersChanged(reason);

            WindowParametersChanged?.Invoke(this, new GameWindowParametersEventArgs(window, reason));
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            OnDeactivated((GameWindow)sender);
            ActiveWindow = null;
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            ActiveWindow = (GameWindow)sender;
            OnActivated(ActiveWindow);
        }

        private void Window_BoundsChanged(object sender, GameWindowBoundsChangedEventArgs e)
        {
            OnWindowBoundsChanged(e);
        }

        public void RemoveWindow(GameContext context)
        {
            if (contextToWindow.ContainsKey(context))
            {
                var window = contextToWindow[context];
                contextToWindow.Remove(context);
                windows.Remove(window);
            }
        }

        private void OnActivated(GameWindow wnd)
        {
            WindowActivated?.Invoke(this, new GameWindowEventArgs(wnd));
        }

        private void OnDeactivated(GameWindow wnd)
        {
            WindowDeactivated?.Invoke(this, new GameWindowEventArgs(wnd));
        }

        private void OnWindowSizeChanged(GameWindow wnd)
        {
            wnd.OnWindowSizeChanged();

            WindowSizeChanged?.Invoke(this,
               new GameWindowSizeChangedEventArgs(wnd, new Size(wnd.Width, wnd.Height)));
        }


        private void OnWindowBoundsChanged(GameWindowBoundsChangedEventArgs args)
        {
            WindowBoundsChanged?.Invoke(this, args);
        }

        private void Wnd_KeyUp(object sender, KeyboardInputEventArgs e)
        {
            KeyUp?.Invoke(this, e);
        }

        private void Wnd_KeyDown(object sender, KeyboardInputEventArgs e)
        {
            KeyDown?.Invoke(this, e);
        }

        private void Wnd_MouseUp(object sender, MouseInputEventArgs e)
        {
            MouseUp?.Invoke(this, e);
        }

        private void Wnd_MouseDown(object sender, MouseInputEventArgs e)
        {
            MouseDown?.Invoke(this, e);
        }

        private void Wnd_MouseWheel(object sender, MouseInputEventArgs e)
        {
            MouseWheel?.Invoke(this, e);
        }

        private void OnWindowCreated(GameWindowEventArgs e)
        {
            WindowCreated?.Invoke(this, e);
        }

        private void OnWindowRemoved(GameWindowEventArgs e)
        {
            WindowRemoved?.Invoke(this, e);
        }

        private void Wnd_MouseDelta(object sender, MouseInputEventArgs e)
        {
            MouseDelta?.Invoke(this, e);
        }

        /// <summary>
        /// Occurs when new Game context added to the list
        /// </summary>
        public event EventHandler<GameWindowEventArgs> WindowCreated;

        /// <summary>
        /// Occurs when one of the Game contexts removed from the list
        /// </summary>
        public event EventHandler<GameWindowEventArgs> WindowRemoved;

        /// <summary>
        /// Occurs when Game context switches to another control
        /// </summary>
        public event EventHandler<GameWindowEventArgs> WindowActivated;

        /// <summary>
        /// Occurs when one of the Game contexts removed from the list
        /// </summary>
        public event EventHandler<GameWindowParametersEventArgs> WindowParametersChanging;

        /// <summary>
        /// Occurs when one of the Game contexts removed from the list
        /// </summary>
        public event EventHandler<GameWindowParametersEventArgs> WindowParametersChanged;

        /// <summary>
        /// Occurs when Game context got focus
        /// </summary>
        public event EventHandler<GameWindowEventArgs> WindowDeactivated;

        /// <summary>
        /// Occurs when Game window client size changed
        /// </summary>
        public event EventHandler<GameWindowSizeChangedEventArgs> WindowSizeChanged;

        /// <summary>
        /// Occurs when Game window position or client size changed
        /// </summary>
        public event EventHandler<GameWindowBoundsChangedEventArgs> WindowBoundsChanged;

        /// <summary>
        /// Occurs when key was pressed
        /// </summary>
        public event EventHandler<KeyboardInputEventArgs> KeyDown;

        /// <summary>
        /// Occurs when key was released
        /// </summary>
        public event EventHandler<KeyboardInputEventArgs> KeyUp;

        /// <summary>
        /// Occurs when mouse button was pressed
        /// </summary>
        public event EventHandler<MouseInputEventArgs> MouseDown;

        /// <summary>
        /// Occurs when mouse button was released
        /// </summary>
        public event EventHandler<MouseInputEventArgs> MouseUp;

        /// <summary>
        /// Occurs when physical mouse position changed
        /// </summary>
        public event EventHandler<MouseInputEventArgs> MouseDelta;

        /// <summary>
        /// Occurs when mouse button was pressed
        /// </summary>
        public event EventHandler<MouseInputEventArgs> MouseWheel;

        public void Dispose()
        {
            lock (this)
            {
                for (int i = 0; i < Windows.Length; i++)
                {
                    Windows[i].Dispose();
                }
                windows.Clear();
                MainWindow = null;
                ActiveWindow = null;
                contextToWindow.Clear();
            }
        }

        /// <summary>
        /// Creates <see cref="GameWindow"/> from <see cref="object"/>
        /// </summary>
        /// <param name="context">Context (Control) from which <see cref="GameWindow"/> will be created</param>
        /// <returns>new <see cref="GameWindow"/></returns>
        public GameWindow CreateWindow(object context)
        {
            var gameContext = new GameContext(context);
            return CreateWindow(gameContext);
        }

        /// <summary>
        /// Create new game window from context (if no windows has been created already using this context) and add it to the list of game windows
        /// </summary>
        /// <param name="context">Window, in which DX xontent will be rendered</param>
        /// <param name="surfaceFormat">Surface format</param>
        /// <param name="depthFormat">Depth buffer format</param>
        /// <param name="msaaLevel">MSAA level</param>
        public GameWindow CreateWindow(object context, SurfaceFormat surfaceFormat, DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, MSAALevel msaaLevel = MSAALevel.None)
        {
            var gameContext = new GameContext(context);
            if (!contextToWindow.ContainsKey(gameContext))
            {
                var wnd = GameWindow.New(gameContext, surfaceFormat, depthFormat, msaaLevel);
                contextToWindow.Add(gameContext, wnd);
                windowsToAdd.Add(wnd);
                return wnd;
            }
            return contextToWindow[gameContext];
        }

        /// <summary>
        /// Remove <see cref="GameWindow"/>
        /// </summary>
        /// <param name="context">UI Control for which <see cref="GameWindow"/> will be removed</param>
        public void RemoveWindow(object context)
        {
            var gameContext = new GameContext(context);
            RemoveWindow(gameContext);
        }
    }
}
