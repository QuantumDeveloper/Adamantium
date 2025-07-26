using System;
using System.Threading;

namespace Adamantium.UI.Platforms;

public interface IApplicationPlatform
{
    void Run(CancellationToken token);
        
    bool IsOnUIThread { get; }

    void Signal();

    event Action Signaled;
}