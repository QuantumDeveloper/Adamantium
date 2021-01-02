using System;
using Adamantium.Engine.Graphics;
using Adamantium.Imaging;

namespace Adamantium.Game
{
    public interface IGamePlatform
    {
        String DefaultAppDirectory { get; }

        GameOutput MainWindow { get; }

        GameOutput ActiveWindow { get; }

        GameOutput[] Outputs { get; }

        /// <summary>
        /// Creates <see cref="GameOutput"/> with <see cref="AdamantiumGameOutput"/> inside
        /// </summary>
        /// <param name="width">Window width</param>
        /// <param name="height">Window height</param>
        /// <returns>new <see cref="GameOutput"/></returns>
        GameOutput CreateOutput(uint width = 1280, uint height = 720);

        /// <summary>
        /// Creates <see cref="GameOutput"/> from <see cref="GameContext"/>
        /// </summary>
        /// <param name="context">Context (Control) from which <see cref="GameOutput"/> will be created</param>
        /// <returns>new <see cref="GameOutput"/></returns>
        GameOutput CreateOutput(GameContext context);

        /// <summary>
        /// Creates <see cref="GameOutput"/> from <see cref="object"/>
        /// </summary>
        /// <param name="context">Context (Control) from which <see cref="GameOutput"/> will be created</param>
        /// <returns>new <see cref="GameOutput"/></returns>
        GameOutput CreateOutput(Object context);

        /// <summary>
        /// Create new game window from context (if no windows has been created already using this context) and add it to the list of game windows
        /// </summary>
        /// <param name="context">Window, in which DX xontent will be rendered</param>
        /// <param name="surfaceFormat">Surface format</param>
        /// <param name="depthFormat">Depth buffer format</param>
        /// <param name="msaaLevel">MSAA level</param>
        GameOutput CreateOutput(Object context, SurfaceFormat surfaceFormat, DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, MSAALevel msaaLevel = MSAALevel.None);

        /// <summary>
        /// Switches drawing context from old control to new control. After this old control could be safely removed
        /// </summary>
        /// <param name="oldContext">Old control for drawing</param>
        /// <param name="newContext">New control for drawing</param>
        void SwitchContext(GameContext oldContext, GameContext newContext);

        /// <summary>
        /// Adds <see cref="GameOutput"/> to the windows collection
        /// </summary>
        /// <param name="window">window to add to the windows collection</param>
        void AddOutput(GameOutput window);
        
        /// <summary>
        /// Removes <see cref="GameOutput"/> from <see cref="GameOutput"/>
        /// </summary>
        /// <param name="context">Context (Control) by which <see cref="GameOutput"/> will be removed</param>
        void RemoveOutput(GameContext context);

        /// <summary>
        /// Remove <see cref="GameOutput"/>
        /// </summary>
        /// <param name="context">UI Control for which <see cref="GameOutput"/> will be removed</param>
        void RemoveOutput(Object context);

        /// <summary>
        /// Occurs when Game context switches to another control
        /// </summary>
        event EventHandler<GameWindowEventArgs> WindowActivated;

        /// <summary>
        /// Occurs when Game context got focus
        /// </summary>
        event EventHandler<GameWindowEventArgs> WindowDeactivated;

        /// <summary>
        /// Occurs when new Game context added to the list
        /// </summary>
        event EventHandler<GameWindowEventArgs> WindowCreated;


        /// <summary>
        /// Occurs when one of the Game contexts removed from the list
        /// </summary>
        event EventHandler<GameWindowEventArgs> WindowRemoved;

        /// <summary>
        /// Occurs when one of the Game contexts removed from the list
        /// </summary>
        event EventHandler<GameWindowParametersEventArgs> WindowParametersChanging;

        /// <summary>
        /// Occurs when one of the Game contexts removed from the list
        /// </summary>
        event EventHandler<GameWindowParametersEventArgs> WindowParametersChanged;

        /// <summary>
        /// Occurs when Game window client size changed
        /// </summary>
        event EventHandler<GameWindowSizeChangedEventArgs> WindowSizeChanged;

        /// <summary>
        /// Occurs when Game window position or client size changed
        /// </summary>
        event EventHandler<GameWindowBoundsChangedEventArgs> WindowBoundsChanged;

        /// <summary>
        /// Occurs when key was pressed
        /// </summary>
        event EventHandler<KeyboardInputEventArgs> KeyDown;

        /// <summary>
        /// Occurs when key was released
        /// </summary>
        event EventHandler<KeyboardInputEventArgs> KeyUp;

        /// <summary>
        /// Occurs when mouse button was pressed
        /// </summary>
        event EventHandler<MouseInputEventArgs> MouseDown;

        /// <summary>
        /// Occurs when mouse button was pressed
        /// </summary>
        event EventHandler<MouseInputEventArgs> MouseWheel;

        /// <summary>
        /// Occurs when mouse button was released
        /// </summary>
        event EventHandler<MouseInputEventArgs> MouseUp;

        /// <summary>
        /// Occurs when physical mouse position changed
        /// </summary>
        event EventHandler<MouseInputEventArgs> MouseDelta;
    }
}
