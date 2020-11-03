using System;

namespace Adamantium.Engine.Core
{
    /// <summary>
    /// Describes service that could be initialized, started and stopped
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// Gets value indicating is service is currently running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets value indicating is service is currently paused
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Calling this method will start service
        /// </summary>
        void Run();

        /// <summary>
        /// Calling this method will start service on certain context
        /// </summary>
        /// <param name="context"></param>
        void Run(object context);

        /// <summary>
        /// Calling this method will stop running service
        /// </summary>
        void ShutDown();

        /// <summary>
        /// Calling this method will pause running service
        /// </summary>
        void Pause();

        /// <summary>
        /// Calling this method will resume running service
        /// </summary>
        void Resume();

        /// <summary>
        /// Fires when service is started
        /// </summary>
        event EventHandler<EventArgs> Started;

        /// <summary>
        /// Fires when service is shutting down before <see cref="Stopped"/>
        /// </summary>
        event EventHandler<EventArgs> ShuttingDown;

        /// <summary>
        /// Fires when service is stopped
        /// </summary>
        event EventHandler<EventArgs> Stopped;

        /// <summary>
        /// Occurs when service is paused 
        /// </summary>
        event EventHandler Paused;

        /// <summary>
        /// Occurs when service resumed
        /// </summary>
        event EventHandler Resumed;

        /// <summary>
        /// Fires just after service was initialized
        /// </summary>
        event EventHandler<EventArgs> Initialized;

        /// <summary>
        /// Fires when service is ready to work
        /// </summary>
        event EventHandler<EventArgs> ContentLoading;

        /// <summary>
        /// Fires when service is going to unload all resources
        /// </summary>
        event EventHandler<EventArgs> ContentUnloading;
    }
}
