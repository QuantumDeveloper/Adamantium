namespace Adamantium.UI.Core.Commands;

public interface ICommand
{
    public bool CanExecute();
    
    public void Execute();
}