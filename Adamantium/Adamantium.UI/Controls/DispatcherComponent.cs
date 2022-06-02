using Adamantium.UI.Threading;

namespace Adamantium.UI.Controls;

public abstract class DispatcherComponent : IDispatcherComponent
{
    public void VerifyAccess() => Dispatcher.CurrentDispatcher.VerifyAccess();

    public bool CheckAccess() => Dispatcher.CurrentDispatcher.CheckAccess();
}