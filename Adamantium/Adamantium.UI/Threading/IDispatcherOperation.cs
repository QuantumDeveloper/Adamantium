using System;
using System.Threading.Tasks;
using Adamantium.UI.Core.Dispatcher;

namespace Adamantium.UI.Threading;

public interface IDispatcherOperation
{
    Delegate Action { get; }
        
    DispatcherPriority Priority { get; }

    void Run();

    Task Task { get; }
}