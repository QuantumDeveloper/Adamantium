namespace Adamantium.UI.Core;

public interface IDispatcherComponent
{
    void VerifyAccess();

    bool CheckAccess();
}