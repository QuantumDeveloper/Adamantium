using System;

namespace Adamantium.Engine.Core
{
    public interface IEnable
    {
        bool IsEnabled { get; set; }

        event EventHandler<StateEventArgs> EnabledChanged;
    }
}
