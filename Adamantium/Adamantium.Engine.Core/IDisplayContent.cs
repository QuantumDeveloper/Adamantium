namespace Adamantium.Engine.Core
{
    /// <summary>
    /// An interface for a drawable component that is called by the <see cref="SystemManager.DisplayContent"/> to output rendered content to screen class.
    /// </summary>
    public interface IDisplayContent
    {
        bool CanDisplayContent { get; }
        void Present();
    }
}
