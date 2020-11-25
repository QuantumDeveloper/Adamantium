namespace Adamantium.UI.Threading
{
    public enum DispatcherPriority : int
    {
        /// <summary>
        ///     Operations at this priority are processed when the application
        ///     is idle.
        /// </summary>
        ApplicationIdle = 1,
        
        /// <summary>
        ///     Operations at this priority are processed at the same
        ///     priority as input.
        /// </summary>
        Input = 2,
 
        /// <summary>
        ///     Operations at this priority are processed when layout and render is
        ///     done but just before items at input priority are serviced. Specifically
        ///     this is used while firing the Loaded event
        /// </summary>
        Loaded = 3,
 
        /// <summary>
        ///     Operations at this priority are processed at the same
        ///     priority as rendering.
        /// </summary>
        Render = 4,
 
        /// <summary>
        ///     Operations at this priority are processed at the same
        ///     priority as data binding.
        /// </summary>
        DataBind = 5,
 
        /// <summary>
        ///     Operations at this priority are processed at normal priority.
        /// </summary>
        Normal = 6,
        
        MaxValue = 6
    }
}