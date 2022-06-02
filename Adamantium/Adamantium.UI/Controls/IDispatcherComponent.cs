namespace Adamantium.UI.Controls;

public interface IDispatcherComponent
{
    void VerifyAccess();

    bool CheckAccess();
}