namespace Adamantium.Game
{
    public enum GameMode
    {
        /// <summary>
        /// Game is working as standalone instance and should start game platforms during work
        /// </summary>
        Standalone,
        
        /// <summary>
        /// Game is working under another platform and should not start up game platforms
        /// </summary>
        Slave
    }
}