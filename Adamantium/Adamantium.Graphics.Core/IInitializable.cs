namespace Adamantium.Graphics.Core
{
    public interface IInitializable
    {
        void Initialize();

        bool Initialized { get; }
        
        bool Initializing { get; }
    }
}