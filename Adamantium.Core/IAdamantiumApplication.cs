using System;

namespace Adamantium.Core
{
    /// <summary>
    /// An application of this engine: it owns a frame loop, and can be run on its own or pumped a frame at a time
    /// by somebody else's. Starts, pauses, resumes and shuts down, announcing each of those.
    /// </summary>
    public interface IAdamantiumApplication
    {
        /// <summary>
        /// Gets value indicating is application is currently running
        /// </summary>
        bool IsRunning { get; }

        bool IsInitialized { get; }

        /// <summary>
        /// Gets value indicating is application is currently paused
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Calling this method will start application
        /// </summary>
        void Run();

        /// <summary>
        /// Calling this method will start application on certain context
        /// </summary>
        /// <param name="context"></param>
        void Run(object context);

        void RunOnce(AppTime time);

        /// <summary>
        /// Calling this method will stop running application
        /// </summary>
        void ShutDown();

        /// <summary>
        /// Calling this method will pause running application
        /// </summary>
        void Pause();

        /// <summary>
        /// Calling this method will resume running application
        /// </summary>
        void Resume();

        /// <summary>
        /// Fires when application is started
        /// </summary>
        event EventHandler<EventArgs> Started;

        /// <summary>
        /// Fires when application is shutting down before <see cref="Stopped"/>
        /// </summary>
        event EventHandler<EventArgs> ShuttingDown;

        /// <summary>
        /// Fires when application is stopped
        /// </summary>
        event EventHandler<EventArgs> Stopped;

        /// <summary>
        /// Occurs when application is paused
        /// </summary>
        event EventHandler Paused;

        /// <summary>
        /// Occurs when application resumed
        /// </summary>
        event EventHandler Resumed;

        /// <summary>
        /// Fires when application is ready to work
        /// </summary>
        event EventHandler<EventArgs> ContentLoading;

        /// <summary>
        /// Fires when application is going to unload all resources
        /// </summary>
        event EventHandler<EventArgs> ContentUnloading;
    }
}
